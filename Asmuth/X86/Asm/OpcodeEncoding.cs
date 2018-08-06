using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Asm
{
	/// <summary>
	/// The necessary information to determine if a series of byte
	/// corresponds to an instruction.
	/// </summary>
	public readonly struct OpcodeEncoding
	{
		public OpcodeEncodingFlags Flags { get; }
		public byte MainByte { get; }
		public byte ModRM { get; }
		public byte Imm8 { get; }

		public OpcodeEncoding(OpcodeEncodingFlags flags, byte mainByte, byte modRM, byte imm8)
		{
			if (flags.GetMap() == OpcodeMap.Default && flags.GetSimdPrefix().HasValue)
				throw new ArgumentException("Default opcode map implies no SIMD prefix.");
			if (flags.IsVectorXex() && !flags.GetSimdPrefix().HasValue)
				throw new ArgumentException("Vector XEX implies SIMD prefixes.");
			if (flags.IsVectorXex() && !flags.IsLongMode())
				throw new ArgumentException("Vector XEX implies long mode.");
			if (flags.IsEscapeXex() && (flags & OpcodeEncodingFlags.VexL_Mask) != OpcodeEncodingFlags.VexL_Ignored)
				throw new ArgumentException("Escape XEX implies ignored VEX.L.");
			if (flags.IsIA32Mode() && !flags.GetRexW().GetValueOrDefault())
				throw new ArgumentException("IA32 mode implies no REX.W.");
			if (flags.HasImm8Ext() && flags.GetImmediateSizeInBytes() != 1)
				throw new ArgumentException("imm8 opcode extension implies 8-bit immediate,");
			if (flags.HasVexIS4() && !flags.IsVex() && !flags.IsXop())
				throw new ArgumentException("/is4 implies VEX or XOP.");

			this.Flags = flags;
			this.MainByte = (byte)(mainByte & flags.GetMainByteMask());
			this.ModRM = modRM;
			this.Imm8 = flags.HasFixedImm8() ? imm8 : (byte)0;
		}

		public bool HasModRM => (Flags & OpcodeEncodingFlags.HasModRM) != 0;
		public SimdPrefix? SimdPrefix => Flags.GetSimdPrefix();
		public OpcodeMap Map => Flags.GetMap();
		public int ImmediateSizeInBytes => Flags.GetImmediateSizeInBytes();

		public bool IsMatchUpToMainByte(CodeSegmentType codeSegmentType,
			ImmutableLegacyPrefixList legacyPrefixes, Xex xex, byte opcode)
		{
			if (!Flags.AdmitsCodeSegmentType(codeSegmentType)) return false;
			if (!Flags.AdmitsXexType(xex.Type)) return false;
			if (!Flags.AdmitsVectorSize(xex.VectorSize)) return false;
			var integerSize = codeSegmentType.GetIntegerOperandSize(legacyPrefixes.HasOperandSizeOverride, xex.OperandSize64);
			if (!Flags.AdmitsIntegerSize(integerSize)) return false;
			var simdPrefix = Flags.GetSimdPrefix();
			if (simdPrefix.HasValue && legacyPrefixes.PotentialSimdPrefix != simdPrefix.Value) return false;
			if (xex.OpcodeMap != Flags.GetMap()) return false;
			if ((opcode & Flags.GetMainByteMask()) != MainByte) return false;
			return true;
		}

		public bool IsMatch(CodeSegmentType codeSegmentType,
			ImmutableLegacyPrefixList legacyPrefixes, Xex xex,
			byte opcode, ModRM? modRM, byte? imm8)
		{
			if (!IsMatchUpToMainByte(codeSegmentType, legacyPrefixes, xex, opcode)) return false;
			if (modRM.HasValue != ((Flags & OpcodeEncodingFlags.HasModRM) != 0)) return false;
			if (modRM.HasValue && !Flags.AdmitsModRM(modRM.Value, this.ModRM)) return false;
			if (imm8.HasValue != (Flags.GetImmediateSizeInBytes() == 1)) throw new ArgumentException();
			if (Flags.HasFixedImm8() && imm8.Value != this.Imm8) return false;
			return true;
		}

		public bool IsMatch(in Instruction instruction)
		{
			var imm8 = instruction.ImmediateSizeInBytes == 1 ? instruction.Immediate.GetByte(0) : (byte?)null;
			return IsMatch(instruction.CodeSegmentType,
				instruction.LegacyPrefixes, instruction.Xex, instruction.MainByte,
				instruction.ModRM, imm8);
		}

		public override string ToString()
		{
			var str = new StringBuilder(30);

			if (Flags.IsEscapeXex())
				AppendEscapeXex(str);
			else
				AppendVectorXex(str);

			// String tail: opcode byte and what follows: 0B /r ib

			// The opcode itself
			str.AppendFormat(CultureInfo.InvariantCulture, "{0:X2}", MainByte);
			if ((Flags & OpcodeEncodingFlags.MainByteHasEmbeddedReg) != 0)
				str.Append("+r");

			if ((Flags & OpcodeEncodingFlags.HasModRM) != 0)
			{
				switch (Flags & (OpcodeEncodingFlags.ModRM_Mask | OpcodeEncodingFlags.FixedModReg))
				{
					case OpcodeEncodingFlags.ModRM_Any:
						str.Append(" /r");
						break;

					case OpcodeEncodingFlags.ModRM_Any | OpcodeEncodingFlags.FixedModReg:
						str.Append(" /");
						str.Append((char)('0' + ((ModRM & 0b00111000) >> 3)));
						break;

					case OpcodeEncodingFlags.ModRM_Direct | OpcodeEncodingFlags.FixedModReg:
						str.AppendFormat(CultureInfo.InvariantCulture, " {0:X2}+r", ModRM);
						break;

					case OpcodeEncodingFlags.ModRM_Fixed | OpcodeEncodingFlags.FixedModReg:
						str.AppendFormat(CultureInfo.InvariantCulture, " {0:X2}", ModRM);
						break;

					default: throw new NotImplementedException();
				}
			}

			int immediateSizeInBytes = Flags.GetImmediateSizeInBytes();
			if (immediateSizeInBytes > 0)
			{
				str.AppendFormat(CultureInfo.InvariantCulture, " imm{0}", immediateSizeInBytes * 8);
			}

			return str.ToString();
		}

		private void AppendVectorXex(StringBuilder str)
		{
			// Vex/Xop/EVex: VEX.NDS.LIG.66.0F3A.WIG
			var xexType = Flags & OpcodeEncodingFlags.XexType_Mask;
			switch (xexType)
			{
				case OpcodeEncodingFlags.XexType_Vex: str.Append("VEX"); break;
				case OpcodeEncodingFlags.XexType_Xop: str.Append("XOP"); break;
				case OpcodeEncodingFlags.XexType_EVex: str.Append("EVEX"); break;
				default: throw new UnreachableException();
			}

			// TODO: Pretty print .NDS or similar

			switch (Flags & OpcodeEncodingFlags.VexL_Mask)
			{
				case OpcodeEncodingFlags.VexL_Ignored: str.Append(".LIG"); break;
				case OpcodeEncodingFlags.VexL_128: str.Append(".L0"); break;
				case OpcodeEncodingFlags.VexL_256: str.Append(".L1"); break;
				case OpcodeEncodingFlags.VexL_512: str.Append(".L2"); break;
				default: throw new NotImplementedException();
			}

			switch (Flags.GetSimdPrefix().Value)
			{
				case X86.SimdPrefix.None: break;
				case X86.SimdPrefix._66: str.Append(".66"); break;
				case X86.SimdPrefix._F2: str.Append(".F2"); break;
				case X86.SimdPrefix._F3: str.Append(".F3"); break;
				default: throw new UnreachableException();
			}

			switch (Flags.GetMap())
			{
				case OpcodeMap.Escape0F: str.Append(".0F"); break;
				case OpcodeMap.Escape0F38: str.Append(".0F38"); break;
				case OpcodeMap.Escape0F3A: str.Append(".0F3A"); break;
				case OpcodeMap.Xop8: str.Append(".M8"); break;
				case OpcodeMap.Xop9: str.Append(".M9"); break;
				default: throw new NotImplementedException();
			}

			switch (Flags & OpcodeEncodingFlags.RexW_Mask)
			{
				case OpcodeEncodingFlags.RexW_Ignored: str.Append(".WIG"); break;
				case OpcodeEncodingFlags.RexW_0: str.Append(".W0"); break;
				case OpcodeEncodingFlags.RexW_1: str.Append(".W1"); break;
				default: throw new UnreachableException();
			}

			str.Append(' ');
		}

		private void AppendEscapeXex(StringBuilder str)
		{
			// 66 REX.W 0F 38
			var simdPrefix = Flags.GetSimdPrefix();
			if (simdPrefix.HasValue)
			{
				switch (simdPrefix.Value)
				{
					case X86.SimdPrefix.None: str.Append("NP "); break;
					case X86.SimdPrefix._66: str.Append("66 "); break;
					case X86.SimdPrefix._F2: str.Append("F2 "); break;
					case X86.SimdPrefix._F3: str.Append("F3 "); break;
					default: throw new UnreachableException();
				}
			}

			if ((Flags & OpcodeEncodingFlags.RexW_Mask) == OpcodeEncodingFlags.RexW_1)
				str.Append("REX.W ");

			switch (Flags.GetMap())
			{
				case OpcodeMap.Default: break;
				case OpcodeMap.Escape0F: str.Append("0F "); break;
				case OpcodeMap.Escape0F38: str.Append("0F 38 "); break;
				case OpcodeMap.Escape0F3A: str.Append("0F 3A "); break;
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
	}
	
	[Flags]
	public enum OpcodeEncodingFlags : uint
	{
		// The types of code segments in which this opcode can be encoded
		CodeSegment_Shift = 0,
		CodeSegment_Any = 0 << (int)CodeSegment_Shift,
		CodeSegment_IA32 = 1 << (int)CodeSegment_Shift, // 16 or 32 bits
		CodeSegment_Long = 2 << (int)CodeSegment_Shift,
		CodeSegment_Mask = 3 << (int)CodeSegment_Shift,

		// The instruction's xex type
		XexType_Shift = CodeSegment_Shift + 2,
		XexType_Escapes_RexOpt = 0 << (int)XexType_Shift,
		XexType_Vex = 1 << (int)XexType_Shift,
		XexType_Xop = 2 << (int)XexType_Shift,
		XexType_EVex = 3 << (int)XexType_Shift,
		XexType_Mask = 3 << (int)XexType_Shift,

		// The instruction's operand sizes
		OperandSize_Shift = XexType_Shift + 2,
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
		SimdPrefix_Any = 0 << (int)SimdPrefix_Shift, // CMOVA 0F 47 /r
		SimdPrefix_None = 1 << (int)SimdPrefix_Shift, // ADDPS NP 0F 58 /r
		SimdPrefix_66 = 2 << (int)SimdPrefix_Shift, // ADDPD 66 0F 58 /r
		SimdPrefix_F2 = 3 << (int)SimdPrefix_Shift,
		SimdPrefix_F3 = 4 << (int)SimdPrefix_Shift,
		SimdPrefix_Mask = 7 << (int)SimdPrefix_Shift,

		// How the REX.W field is used
		RexW_Shift = SimdPrefix_Shift + 3,
		RexW_Ignored = 0 << (int)VexL_Shift,
		RexW_0 = 1 << (int)VexL_Shift,
		RexW_1 = 2 << (int)VexL_Shift,
		RexW_Mask = 3 << (int)VexL_Shift,

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
		MainByteHasEmbeddedReg_Shift = Map_Shift + 4,
		MainByteHasEmbeddedReg = 1 << (int)MainByteHasEmbeddedReg_Shift, // PUSH: 50+r

		HasModRM_Shift = MainByteHasEmbeddedReg_Shift + 1,
		HasModRM = 1 << (int)HasModRM_Shift,

		FixedModReg_Shift = HasModRM_Shift + 1,
		FixedModReg = 1 << (int)FixedModReg_Shift, // ADD: 81 /0

		ModRM_Shift = FixedModReg_Shift + 1,
		ModRM_Any = 0 << (int)ModRM_Shift,
		ModRM_Indirect = 1 << (int)ModRM_Shift, // PREFETCH: 0F18 M/1
		ModRM_Direct = 2 << (int)ModRM_Shift, // FADD: DC C0+i
		ModRM_Fixed = 3 << (int)ModRM_Shift, // FADDP: DE C1
		ModRM_Mask = 3 << (int)ModRM_Shift,

		// Opcode extension in imm8
		Imm8Ext_Shift = ModRM_Shift + 2,
		Imm8Ext_None = 0 << (int)Imm8Ext_Shift, 
		Imm8Ext_Fixed = 1 << (int)Imm8Ext_Shift, // CMPEQPS: 0FC2 /r 0
		Imm8Ext_VexIS4 = 2 << (int)Imm8Ext_Shift, // VBLENDVPS: VEX.NDS.128.66.0F3A.W0 4A /r /is4
		Imm8Ext_Mask = 3 << (int)Imm8Ext_Shift,

		// Immediate size
		ImmediateSize_Shift = Imm8Ext_Shift + 2,
		ImmediateSize_Mask = 0xF << (int)ImmediateSize_Shift,
	}

	public static class OpcodeEncodingFlagsEnum
	{
		public static bool IsIA32Mode(this OpcodeEncodingFlags flags)
			=> (flags & OpcodeEncodingFlags.CodeSegment_Mask) == OpcodeEncodingFlags.CodeSegment_IA32;

		public static bool IsLongMode(this OpcodeEncodingFlags flags)
			=> (flags & OpcodeEncodingFlags.CodeSegment_Mask) == OpcodeEncodingFlags.CodeSegment_Long;

		public static bool AdmitsCodeSegmentType(
			this OpcodeEncodingFlags flags, CodeSegmentType codeSegmentType)
		{
			switch (flags & OpcodeEncodingFlags.CodeSegment_Mask)
			{
				case OpcodeEncodingFlags.CodeSegment_Any: return true;
				case OpcodeEncodingFlags.CodeSegment_IA32: return codeSegmentType != CodeSegmentType._64Bits;
				case OpcodeEncodingFlags.CodeSegment_Long: return codeSegmentType == CodeSegmentType._64Bits;
				default: throw new ArgumentOutOfRangeException(nameof(flags));
			}
		}
		
		public static bool AdmitsXexType(this OpcodeEncodingFlags flags, XexType xexType)
		{
			switch (flags & OpcodeEncodingFlags.XexType_Mask)
			{
				case OpcodeEncodingFlags.XexType_Escapes_RexOpt: return xexType.AllowsEscapes();
				case OpcodeEncodingFlags.XexType_Vex: return xexType.IsVex();
				case OpcodeEncodingFlags.XexType_Xop: return xexType == XexType.Xop;
				case OpcodeEncodingFlags.XexType_EVex: return xexType == XexType.EVex;
				default: throw new ArgumentOutOfRangeException(nameof(flags));
			}
		}

		public static bool IsEscapeXex(this OpcodeEncodingFlags flags)
			=> (flags & OpcodeEncodingFlags.XexType_Mask) < OpcodeEncodingFlags.XexType_Vex;

		public static bool IsVectorXex(this OpcodeEncodingFlags flags)
			=> (flags & OpcodeEncodingFlags.XexType_Mask) >= OpcodeEncodingFlags.XexType_Vex;

		public static bool IsVex(this OpcodeEncodingFlags flags)
			=> (flags & OpcodeEncodingFlags.XexType_Mask) == OpcodeEncodingFlags.XexType_Vex;

		public static bool IsXop(this OpcodeEncodingFlags flags)
			=> (flags & OpcodeEncodingFlags.XexType_Vex) == OpcodeEncodingFlags.XexType_Xop;

		public static bool IsEVex(this OpcodeEncodingFlags flags)
			=> (flags & OpcodeEncodingFlags.XexType_Vex) == OpcodeEncodingFlags.XexType_EVex;

		public static bool AdmitsVectorSize(this OpcodeEncodingFlags flags, OperandSize size)
		{
			switch (flags & OpcodeEncodingFlags.VexL_Mask)
			{
				case OpcodeEncodingFlags.VexL_Ignored: return true;
				case OpcodeEncodingFlags.VexL_128: return size == OperandSize._128;
				case OpcodeEncodingFlags.VexL_256: return size == OperandSize._256;
				case OpcodeEncodingFlags.VexL_512: return size == OperandSize._512;
				default: throw new ArgumentOutOfRangeException(nameof(flags));
			}
		}

		public static bool AdmitsIntegerSize(this OpcodeEncodingFlags flags, OperandSize size)
		{
			switch (flags & OpcodeEncodingFlags.OperandSize_Mask)
			{
				case OpcodeEncodingFlags.OperandSize_Ignored: return true;
				case OpcodeEncodingFlags.OperandSize_Word: return size == OperandSize.Word;
				case OpcodeEncodingFlags.OperandSize_Dword: return size == OperandSize.Dword;
				default: throw new ArgumentOutOfRangeException(nameof(flags));
			}
		}

		public static SimdPrefix? GetSimdPrefix(this OpcodeEncodingFlags flags)
		{
			switch (flags & OpcodeEncodingFlags.SimdPrefix_Mask)
			{
				case OpcodeEncodingFlags.SimdPrefix_Any: return null;
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
				flags |= OpcodeEncodingFlags.SimdPrefix_Any;
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
			=> (flags & OpcodeEncodingFlags.MainByteHasEmbeddedReg) == 0 ? (byte)0xFF : (byte)0xF8;

		public static bool AdmitsModRM(this OpcodeEncodingFlags flags, ModRM modRM, byte reference)
		{
			if ((flags & OpcodeEncodingFlags.HasModRM) == 0) return false;
			if ((flags & OpcodeEncodingFlags.FixedModReg) != 0
				&& ((byte)modRM & 0b00111000) != (reference & 0b00111000))
				return false;

			switch (flags & OpcodeEncodingFlags.ModRM_Mask)
			{
				case OpcodeEncodingFlags.ModRM_Any: return true;
				case OpcodeEncodingFlags.ModRM_Indirect:
					return (modRM & ModRM.Mod_Mask) != ModRM.Mod_Direct;
				case OpcodeEncodingFlags.ModRM_Direct:
					return (modRM & ModRM.Mod_Mask) == ModRM.Mod_Direct;
				case OpcodeEncodingFlags.ModRM_Fixed:
					return ((byte)modRM & 0b11000111) == (reference & 0b11000111);
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
