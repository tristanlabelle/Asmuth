﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	/// <summary>
	/// The necessary information to determine if a series of bytes
	/// corresponds to a opcode.
	/// </summary>
	public readonly partial struct OpcodeEncoding
	{
		public OpcodeEncodingFlags Flags { get; }
		public byte MainByte { get; }
		public ModRM ModRM { get; }
		public byte Imm8 { get; }

		public OpcodeEncoding(OpcodeEncodingFlags flags, byte mainByte,
			ModRM modRM = default, byte imm8 = 0)
		{
			// Flags consistency
			if (flags.GetAddressSize() == AddressSize._64Bits && flags.GetLongMode() != true)
				throw new ArgumentException("64-bit addresses imply long mode.");
			if (flags.GetAddressSize() == AddressSize._16Bits && flags.GetLongMode() != false)
				throw new ArgumentException("16-bit addresses imply IA32 mode.");
			if (flags.GetMap() == OpcodeMap.Default && flags.GetSimdPrefix().HasValue)
				throw new ArgumentException("Default opcode map implies no SIMD prefix.");
			if (flags.IsVectorXex() && !flags.GetSimdPrefix().HasValue)
				throw new ArgumentException("Vector XEX implies SIMD prefixes.");
			if (flags.IsEscapeXex() && (flags & OpcodeEncodingFlags.VexL_Mask) != OpcodeEncodingFlags.VexL_Ignored)
				throw new ArgumentException("Escape XEX implies ignored VEX.L.");
			if ((flags & OpcodeEncodingFlags.OperandSize_Mask) != 0 && (flags & OpcodeEncodingFlags.RexW_Mask) != OpcodeEncodingFlags.RexW_0)
				throw new ArgumentException("Explicit OperandSize implies zero REX.W.");
			if ((flags & OpcodeEncodingFlags.ModRM_FixedReg) != 0 && !flags.HasModRM())
				throw new ArgumentException("Fixed Mod.reg implies ModRM.");
			if ((flags & OpcodeEncodingFlags.ModRM_RM_Mask) != OpcodeEncodingFlags.ModRM_RM_Any && !flags.HasModRM())
				throw new ArgumentException("Fixed ModRM RM field implies ModRM.");
			if (flags.HasImm8Ext() && flags.GetImmediateSizeInBytes() != 1)
				throw new ArgumentException("imm8 opcode extension implies 8-bit immediate,");
			if (flags.HasVexIS4() && !flags.IsVex() && !flags.IsXop())
				throw new ArgumentException("/is4 implies VEX or XOP.");

			if (flags.HasModRM()) flags.EnsureReferenceModRM(modRM);

			this.Flags = flags;
			this.MainByte = (byte)(mainByte & flags.GetMainByteMask());
			this.ModRM = flags.HasModRM() ? modRM : default;
			this.Imm8 = flags.HasFixedImm8() ? imm8 : (byte)0;
		}

		public VexType? VexType => Flags.GetVexType();
		public byte MainByteMask => Flags.GetMainByteMask();
		public bool HasModRM => (Flags & OpcodeEncodingFlags.ModRM_Present) != 0;
		public bool HasFixedModReg => (Flags & OpcodeEncodingFlags.ModRM_FixedReg) != 0;
		public bool HasFixedImm8 => Flags.HasFixedImm8();
		public SimdPrefix? SimdPrefix => Flags.GetSimdPrefix();
		public OpcodeMap Map => Flags.GetMap();
		public int ImmediateSizeInBytes => Flags.GetImmediateSizeInBytes();

		public bool IsMatchUpToMainByte(CodeSegmentType codeSegmentType,
			ImmutableLegacyPrefixList legacyPrefixes, Xex xex, byte mainByte)
		{
			if (!Flags.AdmitsCodeSegmentType(codeSegmentType)) return false;
			if (!Flags.AdmitsAddressSize(codeSegmentType.GetEffectiveAddressSize(legacyPrefixes))) return false;
			if (!Flags.AdmitsXexType(xex.Type)) return false;
			if (!Flags.AdmitsVectorSize(xex.VectorSize)) return false;
			var integerSize = codeSegmentType.GetIntegerOperandSize(legacyPrefixes.HasOperandSizeOverride, xex.OperandSize64);
			if (!Flags.AdmitsIntegerSize(integerSize)) return false;

			var expectedSimdPrefix = Flags.GetSimdPrefix();
			if (expectedSimdPrefix.HasValue)
			{
				var actualSimdPrefix = xex.SimdPrefix ?? legacyPrefixes.PotentialSimdPrefix;
				if (actualSimdPrefix != expectedSimdPrefix.Value) return false;
			}

			if (xex.OpcodeMap != Flags.GetMap()) return false;
			if ((mainByte & Flags.GetMainByteMask()) != MainByte) return false;
			return true;
		}

		public bool AdmitsModRM(ModRM value)
			=> HasModRM && Flags.AdmitsModRM(value, reference: ModRM);

		public bool IsMatch(CodeSegmentType codeSegmentType,
			ImmutableLegacyPrefixList legacyPrefixes, Xex xex,
			byte mainByte, ModRM? modRM, byte? imm8)
		{
			if (!IsMatchUpToMainByte(codeSegmentType, legacyPrefixes, xex, mainByte)) return false;
			if (modRM.HasValue != HasModRM) return false;
			if (modRM.HasValue && !AdmitsModRM(modRM.Value)) return false;
			if (imm8.HasValue != (Flags.GetImmediateSizeInBytes() == 1)) throw new ArgumentException();
			if (Flags.HasFixedImm8() && imm8.Value != this.Imm8) return false;
			return true;
		}

		public bool IsMatch(in Instruction instruction)
		{
			var imm8 = instruction.ImmediateSizeInBytes == 1 ? instruction.ImmediateData.GetByte(0) : (byte?)null;
			return IsMatch(instruction.CodeSegmentType,
				instruction.LegacyPrefixes, instruction.Xex, instruction.MainOpcodeByte,
				instruction.ModRM, imm8);
		}

		#region ToString
		public override string ToString()
		{
			var str = new StringBuilder(30);

			str.Append('[');

			if (Flags.GetLongMode().HasValue)
				str.Append(Flags.GetLongMode().Value ? "x64 " : "ia32 ");

			if (Flags.GetAddressSize().HasValue)
				str.AppendFormat(CultureInfo.InvariantCulture, "a{0} ",
					Flags.GetAddressSize().Value.InBits());

			if ((Flags & OpcodeEncodingFlags.OperandSize_Mask) == OpcodeEncodingFlags.OperandSize_Word)
				str.Append("o16 ");
			else if ((Flags & OpcodeEncodingFlags.OperandSize_Mask) == OpcodeEncodingFlags.OperandSize_Dword)
				str.Append("o32 ");

			str.Length--; // Remove '[' or space
			if (str.Length > 0) str.Append("] ");

			if (Flags.IsEscapeXex())
				AppendEscapeXex(str);
			else
			{
				str.Append(Flags.ToVexEncoding().ToIntelStyleString());
				str.Append(' ');
			}

			// String tail: opcode byte and what follows: 0B /r ib

			// The opcode itself
			str.AppendFormat(CultureInfo.InvariantCulture, "{0:x2}", (byte)MainByte);
			if ((Flags & OpcodeEncodingFlags.HasMainByteReg) != 0)
				str.Append("+r");

			if ((Flags & OpcodeEncodingFlags.ModRM_Present) != 0)
			{
				str.Append(' ');

				bool fixedReg = (Flags & OpcodeEncodingFlags.ModRM_FixedReg) != 0;
				var modRMFlags = Flags & OpcodeEncodingFlags.ModRM_RM_Mask;
				if (fixedReg && Flags.HasModRMDirectMod())
				{
					str.AppendFormat(CultureInfo.InvariantCulture, "{0:x2}", (byte)ModRM);
					if (modRMFlags == OpcodeEncodingFlags.ModRM_RM_Direct)
						str.Append("+r");
				}
				else
				{
					if (modRMFlags == OpcodeEncodingFlags.ModRM_RM_Indirect)
						str.Append('m');
					str.Append('/');
					if (fixedReg) str.Append('r');
					else str.Append((char)('0' + ModRM.GetReg()));
				}
			}

			int immediateSizeInBytes = Flags.GetImmediateSizeInBytes();
			if (immediateSizeInBytes > 0)
			{
				str.AppendFormat(CultureInfo.InvariantCulture, " i{0}", immediateSizeInBytes * 8);
			}

			return str.ToString();
		}

		private void AppendEscapeXex(StringBuilder str)
		{
			// 66 REX.W 0F 38
			var simdPrefix = Flags.GetSimdPrefix();
			if (simdPrefix.HasValue)
			{
				switch (simdPrefix.Value)
				{
					case X86.SimdPrefix.None: str.Append("np "); break;
					case X86.SimdPrefix._66: str.Append("66 "); break;
					case X86.SimdPrefix._F2: str.Append("f2 "); break;
					case X86.SimdPrefix._F3: str.Append("f3 "); break;
					default: throw new UnreachableException();
				}
			}

			if ((Flags & OpcodeEncodingFlags.RexW_Mask) == OpcodeEncodingFlags.RexW_1)
				str.Append("rex.w ");

			switch (Flags.GetMap())
			{
				case OpcodeMap.Default: break;
				case OpcodeMap.Escape0F: str.Append("0f "); break;
				case OpcodeMap.Escape0F38: str.Append("0f 38 "); break;
				case OpcodeMap.Escape0F3A: str.Append("0f 3a "); break;
				default: throw new UnreachableException();
			}
		}

		private static void AppendImmediate(StringBuilder str, ImmediateSize size)
		{
			switch (size)
			{
				case ImmediateSize.Zero: break;
				case ImmediateSize.Fixed8: str.Append(" ib"); break;
				case ImmediateSize.Fixed16: str.Append(" iw"); break;
				case ImmediateSize.Fixed32: str.Append(" id"); break;
				case ImmediateSize.Fixed64: str.Append(" iq"); break;
				case ImmediateSize.Operand16Or32: str.Append(" iwd"); break;
				case ImmediateSize.Operand16Or32Or64: str.Append(" iwdq"); break;
				default: throw new ArgumentException(nameof(size));
			}
		}
		#endregion
	}
	
	[Flags]
	public enum OpcodeEncodingFlags : uint
	{
		// The types of code segments in which this opcode can be encoded
		LongMode_Shift = 0,
		LongMode_Any = 0 << (int)LongMode_Shift,
		LongMode_No = 1 << (int)LongMode_Shift, // 16 or 32 bits
		LongMode_Yes = 2 << (int)LongMode_Shift,
		LongMode_Mask = 3 << (int)LongMode_Shift,

		// The effective address size for this encoding.
		// Mhis distinguishes between MOV AX/EAX/RAX,moffs16/32/64
		AddressSize_Shift = LongMode_Shift + 2,
		AddressSize_Any = 0 << (int)AddressSize_Shift,
		AddressSize_16 = 1 << (int)AddressSize_Shift,
		AddressSize_32 = 2 << (int)AddressSize_Shift,
		AddressSize_64 = 3 << (int)AddressSize_Shift,
		AddressSize_Mask = 3 << (int)AddressSize_Shift,

		// The instruction's xex type
		VexType_Shift = AddressSize_Shift + 2,
		VexType_None = 0 << (int)VexType_Shift,
		VexType_Vex = 1 << (int)VexType_Shift,
		VexType_Xop = 2 << (int)VexType_Shift,
		VexType_EVex = 3 << (int)VexType_Shift,
		VexType_Mask = 3 << (int)VexType_Shift,

		// The instruction's operand sizes
		OperandSize_Shift = VexType_Shift + 2,
		OperandSize_Ignored = 0 << (int)OperandSize_Shift,
		OperandSize_Word = 1 << (int)OperandSize_Shift,
		OperandSize_Dword = 2 << (int)OperandSize_Shift,
		OperandSize_Mask = 3 << (int)OperandSize_Shift,

		// How the VEX.L / EVEX.L'L fields are used
		VexL_Shift = OperandSize_Shift + 2,
		VexL_Ignored = 0 << (int)VexL_Shift,
		VexL_128 = 1 << (int)VexL_Shift,
		VexL_256 = 2 << (int)VexL_Shift,
		VexL_512 = 3 << (int)VexL_Shift,
		VexL_Mask = 3 << (int)VexL_Shift,

		// SimdPrefix
		SimdPrefix_Shift = VexL_Shift + 2,
		SimdPrefix_Ignored = 0 << (int)SimdPrefix_Shift, // CMOVA 0F 47 /r
		SimdPrefix_None = 1 << (int)SimdPrefix_Shift, // ADDPS NP 0F 58 /r
		SimdPrefix_66 = 2 << (int)SimdPrefix_Shift, // ADDPD 66 0F 58 /r
		SimdPrefix_F3 = 3 << (int)SimdPrefix_Shift,
		SimdPrefix_F2 = 4 << (int)SimdPrefix_Shift,
		SimdPrefix_Mask = 7 << (int)SimdPrefix_Shift,

		// How the REX.W field is used
		RexW_Shift = SimdPrefix_Shift + 3,
		RexW_Ignored = 0 << (int)RexW_Shift,
		RexW_0 = 1 << (int)RexW_Shift,
		RexW_1 = 2 << (int)RexW_Shift,
		RexW_Mask = 3 << (int)RexW_Shift,

		// Opcode map, specified by escape bytes, in VEX, XOP or EVEX
		Map_Shift = RexW_Shift + 2,
		Map_Default = 0 << (int)Map_Shift,
		Map_0F = 1 << (int)Map_Shift,
		Map_0F38 = 2 << (int)Map_Shift,
		Map_0F3A = 3 << (int)Map_Shift,
		Map_Xop8 = 8 << (int)Map_Shift, // AMD XOP opcode map 8
		Map_Xop9 = 9 << (int)Map_Shift, // AMD XOP opcode map 9
		Map_Xop10 = 10 << (int)Map_Shift, // AMD XOP opcode map 10
		Map_Mask = 0xF << (int)Map_Shift,

		// How the opcode, ModRM and imm8 fields encode the opcode
		HasMainByteReg_Shift = Map_Shift + 4,
		HasMainByteReg = 1 << (int)HasMainByteReg_Shift, // PUSH: 50+r

		ModRM_Present_Shift = HasMainByteReg_Shift + 1,
		ModRM_Present = 1 << (int)ModRM_Present_Shift,

		ModRM_FixedReg_Shift = ModRM_Present_Shift + 1,
		ModRM_FixedReg = 1 << (int)ModRM_FixedReg_Shift, // ADD: 81 /0

		ModRM_RM_Shift = ModRM_FixedReg_Shift + 1,
		ModRM_RM_Any = 0 << (int)ModRM_RM_Shift,
		ModRM_RM_Indirect = 1 << (int)ModRM_RM_Shift, // PREFETCH: 0F18 M/1
		ModRM_RM_Direct = 2 << (int)ModRM_RM_Shift, // FADD: DC C0+i, implies mod = 11
		ModRM_RM_Fixed = 3 << (int)ModRM_RM_Shift, // FADDP: DE C1, implies mod = 11
		ModRM_RM_Mask = 3 << (int)ModRM_RM_Shift,

		// Opcode extension in imm8
		Imm8Ext_Shift = ModRM_RM_Shift + 2,
		Imm8Ext_None = 0 << (int)Imm8Ext_Shift, 
		Imm8Ext_Fixed = 1 << (int)Imm8Ext_Shift, // CMPEQPS: 0FC2 /r 0
		Imm8Ext_VexIS4 = 2 << (int)Imm8Ext_Shift, // VBLENDVPS: VEX.NDS.128.66.0F3A.W0 4A /r /is4
		Imm8Ext_Mask = 3 << (int)Imm8Ext_Shift,

		// Immediate size
		ImmediateSize_Shift = Imm8Ext_Shift + 2,
		ImmediateSize_8 = 1 << (int)ImmediateSize_Shift,
		ImmediateSize_16 = 2 << (int)ImmediateSize_Shift,
		ImmediateSize_32 = 4 << (int)ImmediateSize_Shift,
		ImmediateSize_64 = 8 << (int)ImmediateSize_Shift,
		ImmediateSize_Mask = 0xF << (int)ImmediateSize_Shift,
	}

	public static class OpcodeEncodingFlagsEnum
	{
		public static VexEncoding ToVexEncoding(this OpcodeEncodingFlags flags)
		{
			var builder = new VexEncoding.Builder();

			switch (flags & OpcodeEncodingFlags.VexType_Mask)
			{
				case OpcodeEncodingFlags.VexType_None: throw new ArgumentException();
				case OpcodeEncodingFlags.VexType_Vex: builder.Type = VexType.Vex; break;
				case OpcodeEncodingFlags.VexType_Xop: builder.Type = VexType.Xop; break;
				case OpcodeEncodingFlags.VexType_EVex: builder.Type = VexType.EVex; break;
				default: throw new ArgumentOutOfRangeException(nameof(flags));
			}

			builder.RegOperand = VexRegOperand.Invalid; // Lost in translation

			switch (flags & OpcodeEncodingFlags.VexL_Mask)
			{
				case OpcodeEncodingFlags.VexL_Ignored: builder.VectorSize = null; break;
				case OpcodeEncodingFlags.VexL_128: builder.VectorSize = SseVectorSize._128Bits; break;
				case OpcodeEncodingFlags.VexL_256: builder.VectorSize = SseVectorSize._256Bits; break;
				case OpcodeEncodingFlags.VexL_512: builder.VectorSize = SseVectorSize._512Bits; break;
				default: throw new ArgumentOutOfRangeException(nameof(flags));
			}

			builder.SimdPrefix = flags.GetSimdPrefix().Value;
			builder.OpcodeMap = flags.GetMap();
			builder.RexW = flags.GetRexW();

			return builder.Build();
		}

		public static bool? GetLongMode(this OpcodeEncodingFlags flags)
		{
			flags &= OpcodeEncodingFlags.LongMode_Mask;
			if (flags == OpcodeEncodingFlags.LongMode_Any) return null;
			return flags == OpcodeEncodingFlags.LongMode_Yes;
		}
		
		public static bool AdmitsCodeSegmentType(
			this OpcodeEncodingFlags flags, CodeSegmentType codeSegmentType)
		{
			switch (flags & OpcodeEncodingFlags.LongMode_Mask)
			{
				case OpcodeEncodingFlags.LongMode_Any: return true;
				case OpcodeEncodingFlags.LongMode_No: return codeSegmentType != CodeSegmentType._64Bits;
				case OpcodeEncodingFlags.LongMode_Yes: return codeSegmentType == CodeSegmentType._64Bits;
				default: throw new ArgumentOutOfRangeException(nameof(flags));
			}
		}

		public static bool AdmitsAddressSize(this OpcodeEncodingFlags flags, AddressSize size)
			=> GetAddressSize(flags).GetValueOrDefault(size) == size;

		public static AddressSize? GetAddressSize(this OpcodeEncodingFlags flags)
		{
			flags &= OpcodeEncodingFlags.AddressSize_Mask;
			if (flags == OpcodeEncodingFlags.AddressSize_Any) return null;
			return (AddressSize)((int)AddressSize._16Bits
				+ ((flags - OpcodeEncodingFlags.AddressSize_16) >> (int)OpcodeEncodingFlags.AddressSize_Shift));
		}

		public static VexType? GetVexType(this OpcodeEncodingFlags flags)
		{
			switch (flags & OpcodeEncodingFlags.VexType_Mask)
			{
				case OpcodeEncodingFlags.VexType_None: return null;
				case OpcodeEncodingFlags.VexType_Vex: return VexType.Vex;
				case OpcodeEncodingFlags.VexType_Xop: return VexType.Xop;
				case OpcodeEncodingFlags.VexType_EVex: return VexType.EVex;
				default: throw new ArgumentOutOfRangeException(nameof(flags));
			}
		}

		public static bool AdmitsXexType(this OpcodeEncodingFlags flags, XexType xexType)
		{
			switch (flags & OpcodeEncodingFlags.VexType_Mask)
			{
				case OpcodeEncodingFlags.VexType_None: return xexType.AllowsEscapes();
				case OpcodeEncodingFlags.VexType_Vex: return xexType.IsVex();
				case OpcodeEncodingFlags.VexType_Xop: return xexType == XexType.Xop;
				case OpcodeEncodingFlags.VexType_EVex: return xexType == XexType.EVex;
				default: throw new ArgumentOutOfRangeException(nameof(flags));
			}
		}

		public static bool IsEscapeXex(this OpcodeEncodingFlags flags)
			=> (flags & OpcodeEncodingFlags.VexType_Mask) < OpcodeEncodingFlags.VexType_Vex;

		public static bool IsVectorXex(this OpcodeEncodingFlags flags)
			=> (flags & OpcodeEncodingFlags.VexType_Mask) >= OpcodeEncodingFlags.VexType_Vex;

		public static bool IsVex(this OpcodeEncodingFlags flags)
			=> (flags & OpcodeEncodingFlags.VexType_Mask) == OpcodeEncodingFlags.VexType_Vex;

		public static bool IsXop(this OpcodeEncodingFlags flags)
			=> (flags & OpcodeEncodingFlags.VexType_Vex) == OpcodeEncodingFlags.VexType_Xop;

		public static bool IsEVex(this OpcodeEncodingFlags flags)
			=> (flags & OpcodeEncodingFlags.VexType_Vex) == OpcodeEncodingFlags.VexType_EVex;

		public static bool AdmitsVectorSize(this OpcodeEncodingFlags flags, SseVectorSize size)
		{
			switch (flags & OpcodeEncodingFlags.VexL_Mask)
			{
				case OpcodeEncodingFlags.VexL_Ignored: return true;
				case OpcodeEncodingFlags.VexL_128: return size == SseVectorSize._128Bits;
				case OpcodeEncodingFlags.VexL_256: return size == SseVectorSize._256Bits;
				case OpcodeEncodingFlags.VexL_512: return size == SseVectorSize._512Bits;
				default: throw new ArgumentOutOfRangeException(nameof(flags));
			}
		}

		public static bool AdmitsIntegerSize(this OpcodeEncodingFlags flags, IntegerSize size)
		{
			switch (flags & OpcodeEncodingFlags.OperandSize_Mask)
			{
				case OpcodeEncodingFlags.OperandSize_Ignored: return true;
				case OpcodeEncodingFlags.OperandSize_Word: return size == IntegerSize.Word;
				case OpcodeEncodingFlags.OperandSize_Dword: return size == IntegerSize.Dword;
				default: throw new ArgumentOutOfRangeException(nameof(flags));
			}
		}

		public static SimdPrefix? GetSimdPrefix(this OpcodeEncodingFlags flags)
		{
			switch (flags & OpcodeEncodingFlags.SimdPrefix_Mask)
			{
				case OpcodeEncodingFlags.SimdPrefix_Ignored: return null;
				case OpcodeEncodingFlags.SimdPrefix_None: return SimdPrefix.None;
				case OpcodeEncodingFlags.SimdPrefix_66: return SimdPrefix._66;
				case OpcodeEncodingFlags.SimdPrefix_F2: return SimdPrefix._F2;
				case OpcodeEncodingFlags.SimdPrefix_F3: return SimdPrefix._F3;
				default: throw new ArgumentOutOfRangeException(nameof(flags));
			}
		}

		public static OpcodeEncodingFlags WithSimdPrefix(this OpcodeEncodingFlags flags, SimdPrefix? simdPrefix)
		{
			flags &= ~OpcodeEncodingFlags.SimdPrefix_Mask;

			if (simdPrefix.HasValue)
			{
				switch (simdPrefix.Value)
				{
					case SimdPrefix.None: flags |= OpcodeEncodingFlags.SimdPrefix_None; break;
					case SimdPrefix._66: flags |= OpcodeEncodingFlags.SimdPrefix_66; break;
					case SimdPrefix._F2: flags |= OpcodeEncodingFlags.SimdPrefix_F2; break;
					case SimdPrefix._F3: flags |= OpcodeEncodingFlags.SimdPrefix_F3; break;
					default: throw new ArgumentOutOfRangeException(nameof(simdPrefix));
				}
			}
			else
			{
				flags |= OpcodeEncodingFlags.SimdPrefix_Ignored;
			}

			return flags;
		}

		public static bool? GetRexW(this OpcodeEncodingFlags flags)
		{
			switch (flags & OpcodeEncodingFlags.RexW_Mask)
			{
				case OpcodeEncodingFlags.RexW_Ignored: return null;
				case OpcodeEncodingFlags.RexW_0: return false;
				case OpcodeEncodingFlags.RexW_1: return true;
				default: throw new ArgumentOutOfRangeException(nameof(flags));
			}
		}

		public static OpcodeMap GetMap(this OpcodeEncodingFlags flags)
			=> (OpcodeMap)((uint)(flags & OpcodeEncodingFlags.Map_Mask) >> (int)OpcodeEncodingFlags.Map_Shift);

		public static OpcodeEncodingFlags WithMap(this OpcodeEncodingFlags flags, OpcodeMap map)
			=> With(flags, OpcodeEncodingFlags.Map_Mask, (int)OpcodeEncodingFlags.Map_Shift, (uint)map);

		public static byte GetMainByteMask(this OpcodeEncodingFlags flags)
			=> (flags & OpcodeEncodingFlags.HasMainByteReg) == 0 ? (byte)0xFF : (byte)0xF8;

		public static bool HasModRM(this OpcodeEncodingFlags flags)
			=> (flags & OpcodeEncodingFlags.ModRM_Present) != 0;

		public static bool HasAnyModRM(this OpcodeEncodingFlags flags)
			=> (flags & OpcodeEncodingFlags.ModRM_Present) != 0
			&& (flags & OpcodeEncodingFlags.ModRM_FixedReg) == 0
			&& (flags & OpcodeEncodingFlags.ModRM_RM_Mask) == OpcodeEncodingFlags.ModRM_RM_Any;

		public static bool HasModRMDirectMod(this OpcodeEncodingFlags flags)
			=> (flags & OpcodeEncodingFlags.ModRM_Present) != 0
			&& ((flags & OpcodeEncodingFlags.ModRM_RM_Mask) == OpcodeEncodingFlags.ModRM_RM_Direct
			|| (flags & OpcodeEncodingFlags.ModRM_RM_Mask) == OpcodeEncodingFlags.ModRM_RM_Fixed);

		public static void EnsureReferenceModRM(this OpcodeEncodingFlags flags, ModRM modRM)
		{
			if ((flags & OpcodeEncodingFlags.ModRM_FixedReg) == 0
				&& (modRM & ModRM.Reg_Mask) != ModRM.Reg_0)
				throw new ArgumentException();

			var rmFlags = flags & OpcodeEncodingFlags.ModRM_RM_Mask;
			if (rmFlags != OpcodeEncodingFlags.ModRM_RM_Fixed
				&& (modRM & ModRM.RM_Mask) != ModRM.RM_0)
				throw new ArgumentException();

			bool requiresDirectMod = rmFlags == OpcodeEncodingFlags.ModRM_RM_Direct
				|| rmFlags == OpcodeEncodingFlags.ModRM_RM_Fixed;

			if ((modRM & ModRM.Mod_Mask) != (requiresDirectMod ? ModRM.Mod_Direct : 0))
				throw new ArgumentException();
		}

		public static bool AdmitsModRM(this OpcodeEncodingFlags flags,
			ModRM value, ModRM reference)
		{
			if ((flags & OpcodeEncodingFlags.ModRM_Present) == 0) return false;
			if ((flags & OpcodeEncodingFlags.ModRM_FixedReg) != 0
				&& (value & ModRM.Reg_Mask) != (reference & ModRM.Reg_Mask))
				return false;

			switch (flags & OpcodeEncodingFlags.ModRM_RM_Mask)
			{
				case OpcodeEncodingFlags.ModRM_RM_Any: return true;
				case OpcodeEncodingFlags.ModRM_RM_Indirect:
					return (value & ModRM.Mod_Mask) != ModRM.Mod_Direct;
				case OpcodeEncodingFlags.ModRM_RM_Direct:
					return (value & ModRM.Mod_Mask) == ModRM.Mod_Direct;
				case OpcodeEncodingFlags.ModRM_RM_Fixed:
					return (value & ModRM.Mod_Mask) == ModRM.Mod_Direct
						&& (value & ModRM.RM_Mask) == (reference & ModRM.RM_Mask);
				default: throw new ArgumentOutOfRangeException(nameof(flags));
			}
		}

		public static bool HasImm8Ext(this OpcodeEncodingFlags flags)
			=> (flags & OpcodeEncodingFlags.Imm8Ext_Mask) != OpcodeEncodingFlags.Imm8Ext_None;

		public static bool HasFixedImm8(this OpcodeEncodingFlags flags)
			=> (flags & OpcodeEncodingFlags.Imm8Ext_Mask) == OpcodeEncodingFlags.Imm8Ext_Fixed;

		public static bool HasVexIS4(this OpcodeEncodingFlags flags)
			=> (flags & OpcodeEncodingFlags.Imm8Ext_Mask) == OpcodeEncodingFlags.Imm8Ext_VexIS4;

		public static int GetImmediateSizeInBytes(this OpcodeEncodingFlags flags)
			=> (int)(flags & OpcodeEncodingFlags.ImmediateSize_Mask) >> (int)OpcodeEncodingFlags.ImmediateSize_Shift;

		public static OpcodeEncodingFlags WithImmediateSizeInBytes(this OpcodeEncodingFlags flags, int value)
			=> With(flags, OpcodeEncodingFlags.ImmediateSize_Mask, (int)OpcodeEncodingFlags.ImmediateSize_Shift, checked((uint)value));
		
		private static OpcodeEncodingFlags With(this OpcodeEncodingFlags flags, OpcodeEncodingFlags mask, int shift, uint value)
			=> (flags & ~mask) | ((OpcodeEncodingFlags)(value << shift) & mask);
	}
}
