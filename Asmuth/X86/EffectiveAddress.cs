using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	[Flags]
	public enum AddressingModeFlags
	{
		Direct = 0, // "Address" = reg
		Indirect = 1, // Address = [disp]
		Indirect_Base = 2 | Indirect, // Address = [base + disp]
		Indirect_ScaledIndex = 4 | Indirect,  // Address = [index * scale + disp]
		Indirect_BaseAndScaledIndex = Indirect_Base | Indirect_ScaledIndex,  // Address = [base + index * scale + disp]
		Indirect_RipRelative = 8 | Indirect_Base, // Address = [rip + disp]
	}

	public enum AddressBaseRegister
	{
		A, C, D, B, SP, BP, SI, DI,
		// 64-bit mode only
		R8, R9, R10, R11, R12, R13, R14, R15,
		Rip
	}

	/// <summary>
	/// Encapsulates all data to compute an effective address, as can be specified using an r/m operand.
	/// </summary>
	[StructLayout(LayoutKind.Sequential, Size = 8)]
	public struct EffectiveAddress
	{
		#region Encoding struct
		public struct Encoding
		{
			public SegmentRegister? Segment { get; set; }
			public bool AddressSizeOverride { get; set; }
			public bool BaseRegExtension { get; set; }
			public bool IndexRegExtension { get; set; }
			public ModRM ModRM { get; set; }
			public Sib? Sib { get; set; }
			public int Displacement { get; set; }

			public Encoding(ModRM modRM, Sib? sib = null, int displacement = 0)
			{
				this.Segment = null;
				this.AddressSizeOverride = false;
				this.BaseRegExtension = false;
				this.IndexRegExtension = false;
				this.ModRM = modRM;
				this.Sib = sib;
				this.Displacement = displacement;
			}

			public Encoding(ImmutableLegacyPrefixList legacyPrefixes, Xex xex,
				ModRM modRM, Sib? sib = null, int displacement = 0)
			{
				this.Segment = legacyPrefixes.SegmentOverride;
				this.AddressSizeOverride = legacyPrefixes.HasAddressSizeOverride;
				this.BaseRegExtension = xex.BaseRegExtension;
				this.IndexRegExtension = xex.IndexRegExtension;
				this.ModRM = modRM;
				this.Sib = sib;
				this.Displacement = displacement;
			}

			public void SetLegacyPrefixes(ImmutableLegacyPrefixList prefixes)
			{
				Segment = prefixes.SegmentOverride;
				AddressSizeOverride = prefixes.HasAddressSizeOverride;
			}

			public void SetRex(Rex rex)
			{
				BaseRegExtension = (rex & Rex.BaseRegExtension) != 0;
				IndexRegExtension = (rex & Rex.IndexRegExtension) != 0;
			}

			public void SetXex(Xex xex)
			{
				BaseRegExtension = xex.BaseRegExtension;
				IndexRegExtension = xex.IndexRegExtension;
			}
		}
		#endregion

		#region Flags enum
		[Flags]
		private enum Flags : ushort
		{
			// Always defined
			AddressSize_Shift = 0,
			AddressSize_Undefined = 0 << (int)AddressSize_Shift, // Direct addressing
			AddressSize_16 = 1 << (int)AddressSize_Shift,
			AddressSize_32 = 2 << (int)AddressSize_Shift, // Default to 32
			AddressSize_64 = 3 << (int)AddressSize_Shift,
			AddressSize_Mask = 3 << (int)AddressSize_Shift,

			// Ignored with direct addressing
			Segment_Shift = AddressSize_Shift + 2,
			Segment_E = 0 << (int)Segment_Shift,
			Segment_C = 1 << (int)Segment_Shift,
			Segment_S = 2 << (int)Segment_Shift,
			Segment_D = 3 << (int)Segment_Shift,
			Segment_F = 4 << (int)Segment_Shift,
			Segment_G = 5 << (int)Segment_Shift,
			Segment_Mask = 7 << (int)Segment_Shift,

			// Base or direct register
			BaseReg_Shift = Segment_Shift + 3,
			BaseReg_R8 = 0x8 << (int)BaseReg_Shift,
			BaseReg_Rip = 0x11 << (int)BaseReg_Shift,
			BaseReg_None = 0x12 << (int)BaseReg_Shift,
			BaseReg_Mask = 0x1F << (int)BaseReg_Shift,

			DirectReg_Shift = BaseReg_Shift,
			DirectReg_Mask = 0xF << DirectReg_Shift,

			// Ignored with direct or rip-relative addressing
			IndexReg_Shift = BaseReg_Shift + 5,
			IndexReg_None = (int)GprCode.Esp << (int)IndexReg_Shift, // ESP cannot be used as an index
			IndexReg_R8 = 0x8 << (int)IndexReg_Shift,
			IndexReg_Mask = 0xF << (int)IndexReg_Shift,

			// Ignored if no index
			Scale_Shift = IndexReg_Shift + 4,
			Scale_1x = 0 << (int)Scale_Shift,
			Scale_2x = 1 << (int)Scale_Shift,
			Scale_4x = 2 << (int)Scale_Shift,
			Scale_8x = 3 << (int)Scale_Shift,
			Scale_Mask = 3 << (int)Scale_Shift,
		}

		private static Flags BaseFlags(AddressSize size, SegmentRegister segment)
		{
			return (Flags)(((int)size + 1) << (int)Flags.AddressSize_Shift)
				| (Flags)((int)segment << (int)Flags.Segment_Shift);
		}

		private static Flags BaseFlags(AddressSize size)
			=> (Flags)(((int)size + 1) << (int)Flags.AddressSize_Shift) | Flags.Segment_D; 
		#endregion

		#region Fields
		private readonly Flags flags;
		private readonly int displacement;
		#endregion

		#region Construction
		private EffectiveAddress(Flags flags, int displacement)
		{
			this.flags = flags;
			this.displacement = displacement;
		}

		private EffectiveAddress(Flags flags)
		{
			this.flags = flags;
			this.displacement = 0;
		}

		public static EffectiveAddress Direct(byte register)
		{
			Contract.Requires(register < 16);
			var flags = Flags.AddressSize_Undefined;
			flags |= (Flags)(register << (int)Flags.DirectReg_Shift);
			return new EffectiveAddress(flags);
		}

		public static EffectiveAddress Direct(GprCode gprCode) => Direct((byte)gprCode);

		public static EffectiveAddress Absolute(AddressSize size, int address)
		{
			Contract.Requires(size > X86.AddressSize._16 || (short)address == address);
			return new EffectiveAddress(BaseFlags(size) | Flags.BaseReg_None | Flags.IndexReg_None, address);
		}

		public static EffectiveAddress Absolute(AddressSize size, SegmentRegister segment, int address)
		{
			Contract.Requires(size > X86.AddressSize._16 || (short)address == address);
			return new EffectiveAddress(BaseFlags(size, segment) | Flags.BaseReg_None | Flags.IndexReg_None, address);
		}

		public static EffectiveAddress Indirect(
			AddressSize addressSize, SegmentRegister? segment, 
			AddressBaseRegister? @base, GprCode? index = null, byte scale = 1, int displacement = 0)
		{
			Contract.Requires(scale == 1 || scale == 2 || scale == 4 || scale == 8
				|| (scale == 0 && !index.HasValue));
			Contract.Requires(@base != AddressBaseRegister.Rip || (addressSize != X86.AddressSize._16 && index.HasValue));
			if (addressSize == X86.AddressSize._16)
			{
				Contract.Requires(!(@base >= AddressBaseRegister.R8));
				Contract.Requires(!(index >= GprCode.R8));
				Contract.Requires((short)displacement == displacement);
				if (@base.HasValue)
				{
					if (index.HasValue)
					{
						Contract.Requires(@base == AddressBaseRegister.B || @base == AddressBaseRegister.BP);
						Contract.Requires(index.Value == GprCode.SI || index.Value == GprCode.DI);
					}
					else
					{
						Contract.Requires(@base == AddressBaseRegister.SI || @base == AddressBaseRegister.DI
							|| @base == AddressBaseRegister.BP || @base == AddressBaseRegister.B);
					}
				}
				else
				{
					Contract.Requires(!index.HasValue);
				}
			}
			else
			{
				Contract.Requires(index != GprCode.Esp);
			}

			// Segment defaults to D, or S if we are using a stack-pointing register
			if (!segment.HasValue)
			{
				segment = (@base == AddressBaseRegister.SP || @base == AddressBaseRegister.BP)
					? SegmentRegister.SS : SegmentRegister.DS;
			}

			var flags = BaseFlags(addressSize, segment.Value);
			flags |= @base.HasValue ? (Flags)((int)@base << (int)Flags.BaseReg_Shift) : Flags.BaseReg_None;
			flags |= index.HasValue ? (Flags)((int)index << (int)Flags.IndexReg_Shift) : Flags.IndexReg_None;
			if (scale == 2) flags |= Flags.Scale_2x;
			else if (scale == 4) flags |= Flags.Scale_4x;
			else if (scale == 8) flags |= Flags.Scale_8x;
			return new EffectiveAddress(flags, displacement);
		}

		public static EffectiveAddress Indirect(
			AddressSize addressSize, SegmentRegister? segment,
			GprCode? @base, GprCode? index = null, byte scale = 1, int displacement = 0)
			=> Indirect(addressSize, segment, (AddressBaseRegister?)@base, index, scale, displacement);

		public static EffectiveAddress Indirect(
			AddressSize addressSize, SegmentRegister? segment,
			AddressBaseRegister @base, int displacement = 0)
			=> Indirect(addressSize, segment, @base, index: null, scale: 1, displacement: displacement);

		public static EffectiveAddress Indirect(
			AddressSize addressSize, SegmentRegister? segment,
			GprCode @base, int displacement = 0)
			=> Indirect(addressSize, segment, (AddressBaseRegister)@base, displacement);

		public static EffectiveAddress RipRelative(
			AddressSize addressSize, SegmentRegister? segment, int displacement)
		{
			Contract.Requires(addressSize != X86.AddressSize._16);
			return Indirect(addressSize, segment, AddressBaseRegister.Rip, displacement);
		}

		public static EffectiveAddress FromIndirect16Encoding(
			SegmentRegister? segment, byte rm, short displacement)
		{
			GprCode @base;
			GprCode? index = null;
			switch (rm)
			{
				case 0: @base = GprCode.BX; @index = GprCode.SI; break;
				case 1: @base = GprCode.BX; @index = GprCode.DI; break;
				case 2: @base = GprCode.BP; @index = GprCode.SI; break;
				case 3: @base = GprCode.BP; @index = GprCode.DI; break;
				case 4: @base = GprCode.SI; break;
				case 5: @base = GprCode.DI; break;
				case 6: @base = GprCode.BP; break;
				case 7: @base = GprCode.BX; break;
				default: throw new ArgumentOutOfRangeException(nameof(rm));
			}

			return Indirect(X86.AddressSize._16, segment, @base, index, 1, displacement);
		}

		public static EffectiveAddress FromEncoding(AddressSize defaultAddressSize, Encoding encoding)
		{
			if ((encoding.ModRM & ModRM.Mod_Mask) == ModRM.Mod_Direct)
				return Direct(encoding.ModRM.GetRM());

			var addressSize = defaultAddressSize.GetEffective(encoding.AddressSizeOverride);

			// Mod in { 0, 1, 2 }
			if (addressSize == X86.AddressSize._16)
			{
				Contract.Assert(unchecked((short)encoding.Displacement) == encoding.Displacement);
				if (encoding.ModRM.GetMod() == 0 && encoding.ModRM.GetRM() == 6)
					return Absolute(addressSize, encoding.Displacement);

				int displacementSize = encoding.ModRM.GetMod();
				Contract.Assert(displacementSize != 0 || encoding.Displacement == 0);
				Contract.Assert(displacementSize != 1 || unchecked((sbyte)encoding.Displacement) == encoding.Displacement);
				return FromIndirect16Encoding(encoding.Segment, encoding.ModRM.GetRM(), (short)encoding.Displacement);
			}
			else
			{
				if (encoding.ModRM.GetMod() == 0 && encoding.ModRM.GetRM() == 5)
				{
					return defaultAddressSize == X86.AddressSize._64
						? RipRelative(addressSize, encoding.Segment, encoding.Displacement)
						: Absolute(addressSize, encoding.Displacement);
				}

				int displacementSize = encoding.ModRM.GetDisplacementSizeInBytes(encoding.Sib.GetValueOrDefault(), addressSize);
				Contract.Assert(displacementSize != 0 || encoding.Displacement == 0);
				Contract.Assert(displacementSize != 1 || unchecked((sbyte)encoding.Displacement) == encoding.Displacement);
				Contract.Assert(displacementSize != 2);

				GprCode? baseReg = (GprCode)encoding.ModRM.GetRM();
				if (baseReg != GprCode.Esp)
					return Indirect(addressSize, encoding.Segment, baseReg.Value, encoding.Displacement);

				// Sib byte
				if (!encoding.Sib.HasValue) throw new ArgumentException();

				baseReg = encoding.Sib.Value.GetBaseReg(encoding.ModRM);
				var index = encoding.Sib.Value.GetIndexReg();
				int scale = encoding.Sib.Value.GetScale();
				return Indirect(addressSize, encoding.Segment, baseReg, index, (byte)scale, encoding.Displacement);
			}
		}

		public static EffectiveAddress FromEncoding(InstructionDecodingMode decodingMode, Encoding encoding)
			=> FromEncoding(decodingMode.GetDefaultAddressSize(), encoding);
		#endregion

		#region Properties
		public bool IsDirect => (flags & Flags.AddressSize_Mask) == Flags.AddressSize_Undefined;
		public bool IsInMemory => !IsDirect;
		public bool IsAbsolute => IsInMemory
			&& (flags & Flags.BaseReg_Mask) == Flags.BaseReg_None
			&& (flags & Flags.IndexReg_Mask) == Flags.IndexReg_None;
		public bool IsRipRelative => Base == AddressBaseRegister.Rip;

		public byte? DirectReg
		{
			get
			{
				if (!IsDirect) return null;
				return (byte)GetFlagsField(Flags.DirectReg_Mask, Flags.DirectReg_Shift);
			}
		}

		public GprCode? DirectGpr => (GprCode?)DirectReg;

		public AddressSize? AddressSize
		{
			get
			{
				if (IsDirect) return null;
				var field = GetFlagsField(Flags.AddressSize_Mask, Flags.AddressSize_Shift);
				// Shift since 0 = undefined
				return (AddressSize)(field - 1);
			}
		}

		public SegmentRegister? Segment
		{
			get
			{
				if (IsDirect) return null;
				return (SegmentRegister)GetFlagsField(Flags.Segment_Mask, Flags.Segment_Shift);
			}
		}

		public SegmentRegister? SegmentOverride
		{
			get
			{
				if (IsDirect) return null;
				var segment = (SegmentRegister)GetFlagsField(Flags.Segment_Mask, Flags.Segment_Shift);
				var @base = (AddressBaseRegister)GetFlagsField(Flags.BaseReg_Mask, Flags.BaseReg_Shift);
				bool isDefault = (@base == AddressBaseRegister.SP || @base == AddressBaseRegister.BP)
					? (segment == SegmentRegister.SS) : (segment == SegmentRegister.DS);
				return isDefault ? (SegmentRegister?)null : segment;
			}
		}

		public AddressBaseRegister? Base
		{
			get
			{
				if (IsDirect || (flags & Flags.BaseReg_Mask) == Flags.BaseReg_None) return null;
				return (AddressBaseRegister)GetFlagsField(Flags.BaseReg_Mask, Flags.BaseReg_Shift);
			}
		}

		public Gpr? BaseAsGpr
		{
			get
			{
				var @base = Base;
				if (!@base.HasValue || @base == AddressBaseRegister.Rip) return null;
				return Gpr.FromCode((GprCode)@base, AddressSize.Value.ToOperandSize(), hasRex: false); // hasRex irrelevant for operand size >= word
			}
		}

		public GprCode? Index
		{
			get
			{
				if (IsDirect || (flags & Flags.IndexReg_Mask) == Flags.IndexReg_None) return null;
				return (GprCode)GetFlagsField(Flags.IndexReg_Mask, Flags.IndexReg_Shift);
			}
		}

		public Gpr? IndexAsGpr
		{
			get
			{
				var code = Index;
				if (code == null) return null;
				return Gpr.FromCode(code.Value, AddressSize.Value.ToOperandSize(), hasRex: false); // hasRex irrelevant for operand size >= word
			}
		}

		public int Scale
		{
			get
			{
				if ((flags & Flags.IndexReg_Mask) == Flags.IndexReg_None) return 0;
				return 1 << GetFlagsField(Flags.Scale_Mask, Flags.Scale_Shift);
			}
		}

		public int Displacement => displacement;

		public OperandSize? MinimumDisplacementSize
		{
			get
			{
				if (displacement == 0) return null;
				if ((displacement & 0xFFFFFF00) == 0) return OperandSize.Byte;
				return AddressSize == X86.AddressSize._16 ? OperandSize.Word : OperandSize.Dword;
			}
		}
		#endregion

		#region Methods
		public bool IsEncodableWithDefaultAddressSize(AddressSize size)
		{
			if (IsDirect) return true;

			var effectiveAddressSize = AddressSize.Value;
			if (size == X86.AddressSize._16 && effectiveAddressSize == X86.AddressSize._64) return false;
			if (size == X86.AddressSize._64) return effectiveAddressSize != X86.AddressSize._16;
			
			// default size = 16 or 32
			var baseFlags = flags & Flags.BaseReg_Mask;
			return (baseFlags < Flags.BaseReg_R8 || baseFlags == Flags.BaseReg_None)
				|| (flags & Flags.IndexReg_Mask) < Flags.IndexReg_R8;
		}

		public bool IsEncodableWithDisplacementSize(OperandSize? size)
		{
			if (!size.HasValue) return displacement == 0;
			if (size.Value > OperandSize.Dword) return false;
			if (IsDirect) return false;
			if (size.Value == OperandSize.Byte) return true;
			return (size.Value == OperandSize.Word) == (AddressSize == X86.AddressSize._16);
		}

		public Encoding Encode(AddressSize defaultAddressSize, byte modReg, OperandSize? displacementSize)
		{
			if ((defaultAddressSize == X86.AddressSize._16 && AddressSize == X86.AddressSize._64)
				|| (defaultAddressSize == X86.AddressSize._64 && AddressSize == X86.AddressSize._16))
				throw new ArgumentException(nameof(defaultAddressSize));

			if (modReg >= 8) throw new ArgumentException(nameof(modReg));
			if (!IsEncodableWithDisplacementSize(displacementSize))
				throw new ArgumentException(nameof(displacementSize));

			var encoding = new Encoding();

			var directGpr = DirectGpr;
			if (directGpr.HasValue)
			{
				if (displacementSize.HasValue) throw new ArgumentException(nameof(displacementSize));

				encoding.BaseRegExtension = directGpr.Value.RequiresRexBit();
				encoding.ModRM = ModRMEnum.FromComponents(mod: 11, reg: modReg, rm: directGpr.Value.GetLow3Bits());
			}
			else
			{
				encoding.AddressSizeOverride = (AddressSize != defaultAddressSize);
				throw new NotImplementedException();
			}

			return encoding;
		}

		public string ToString(OperandSize operandSize, bool hasRex)
		{
			// SS:[EAX+EAX*8+0x2000000000]
			var str = new StringBuilder(30);

			var directGpr = DirectGpr;
			if (directGpr.HasValue)
			{
				str.Append(Gpr.FromCode(directGpr.Value, operandSize, hasRex).Name);
			}
			else
			{
				var segmentOverride = SegmentOverride;
				if (segmentOverride.HasValue)
				{
					str.Append(segmentOverride.Value);
					str.Append(":");
				}

				str.Append('[');

				bool firstTerm = true;
				var @base = Base;
				if (@base.HasValue)
				{
					if (@base == AddressBaseRegister.Rip) str.Append("RIP");
					else str.Append(BaseAsGpr.Value.Name);
					firstTerm = false;
				}

				var index = IndexAsGpr;
				if (index.HasValue)
				{
					if (!firstTerm) str.Append('+');
					str.Append(IndexAsGpr.Value.Name);
					if (Scale > 1)
					{
						str.Append('*');
						str.Append((char)('0' + Scale));
					}
					firstTerm = false;
				}

				if (displacement != 0 || firstTerm)
				{
					if (displacement >= 0 && !firstTerm) str.Append('+');
					str.AppendFormat(CultureInfo.InvariantCulture, "{0:D}", Displacement);
				}

				str.Append(']');
			}

			return str.ToString();
		}

		public override string ToString() => ToString(OperandSize.Dword, hasRex: false);

		private int GetFlagsField(Flags mask, Flags shift)
			=> (int)Bits.MaskAndShiftRight((uint)flags, (uint)mask, (int)shift);
		#endregion
	}
}
