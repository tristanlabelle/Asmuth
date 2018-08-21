using System;
using System.Collections.Generic;
using System.Diagnostics;
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
	public readonly struct EffectiveAddress
	{
		#region Encoding struct
		public enum EncodingFlags : byte
		{
			None = 0,
			AddressSizeOverride = 1 << 0,
			BaseRegExtension = 1 << 1,
			IndexRegExtension = 1 << 2,
			Mask = 0b111
		}

		public readonly struct Encoding
		{
			// Matches EncodingFlags
			private enum FlagsEx : byte
			{
				None = 0,
				AddressSizeOverride = 1 << 0,
				BaseRegExtension = 1 << 1,
				IndexRegExtension = 1 << 2,
				SegmentOverride = 1 << 3,
				Sib = 1 << 4,
			}

			private readonly FlagsEx flagsEx;
			private readonly SegmentRegister segmentOverride; // If flag set
			private readonly ModRM modRM;
			private readonly Sib sib; // If flag set
			private readonly int displacement;

			public Encoding(ModRM modRM, Sib? sib = null, int displacement = 0)
			{
				this.flagsEx = sib.HasValue ? FlagsEx.Sib : FlagsEx.None;
				this.segmentOverride = default;
				this.modRM = modRM;
				this.sib = sib.GetValueOrDefault();
				this.displacement = displacement;
			}
			
			public Encoding(EncodingFlags flags, ModRM modRM, Sib? sib = null, int displacement = 0)
			{
				this.flagsEx = (FlagsEx)(byte)(flags & EncodingFlags.Mask);
				if (sib.HasValue) this.flagsEx |= FlagsEx.Sib;
				this.segmentOverride = default;
				this.modRM = modRM;
				this.sib = sib.GetValueOrDefault();
				this.displacement = displacement;
			}

			public Encoding(EncodingFlags flags, SegmentRegister? segmentOverride,
				ModRM modRM, Sib? sib = null, int displacement = 0)
			{
				this.flagsEx = (FlagsEx)(byte)(flags & EncodingFlags.Mask);
				if (sib.HasValue) this.flagsEx |= FlagsEx.Sib;
				if (segmentOverride.HasValue) this.flagsEx |= FlagsEx.SegmentOverride;
				this.segmentOverride = segmentOverride.GetValueOrDefault();
				this.modRM = modRM;
				this.sib = sib.GetValueOrDefault();
				this.displacement = displacement;
			}

			public Encoding(ImmutableLegacyPrefixList legacyPrefixes, Xex xex,
				ModRM modRM, Sib? sib = null, int displacement = 0)
			{
				this.flagsEx = FlagsEx.None;
				if (legacyPrefixes.SegmentOverride.HasValue) this.flagsEx |= FlagsEx.SegmentOverride;
				if (legacyPrefixes.HasAddressSizeOverride) this.flagsEx |= FlagsEx.AddressSizeOverride;
				if (xex.BaseRegExtension) this.flagsEx |= FlagsEx.BaseRegExtension;
				if (xex.IndexRegExtension) this.flagsEx |= FlagsEx.IndexRegExtension;
				if (sib.HasValue) this.flagsEx |= FlagsEx.Sib;
				this.segmentOverride = legacyPrefixes.SegmentOverride.GetValueOrDefault();
				this.modRM = modRM;
				this.sib = sib.GetValueOrDefault();
				this.displacement = displacement;
			}

			public Encoding(ImmutableLegacyPrefixList legacyPrefixes, Rex rex,
				ModRM modRM, Sib? sib = null, int displacement = 0)
				: this(legacyPrefixes, new Xex(rex), modRM, sib, displacement) { }

			public EncodingFlags Flags => (EncodingFlags)(byte)flagsEx & EncodingFlags.Mask;
			public SegmentRegister? SegmentOverride
				=> (flagsEx & FlagsEx.SegmentOverride) != 0 ? (SegmentRegister?)segmentOverride : null;
			public bool AddressSizeOverride => (flagsEx & FlagsEx.AddressSizeOverride) != 0;
			public bool BaseRegExtension => (flagsEx & FlagsEx.BaseRegExtension) != 0;
			public bool IndexRegExtension => (flagsEx & FlagsEx.IndexRegExtension) != 0;
			public ModRM ModRM => modRM;
			public Sib? Sib => (flagsEx & FlagsEx.Sib) != 0 ? (Sib?)sib : null;
			public int Displacement => displacement;
		}
		#endregion

		#region Flags enum
		[Flags]
		private enum Flags : ushort
		{
			// Always defined
			AddressSize_Shift = 0,
			AddressSize_32 = 0 << (int)AddressSize_Shift, // Default to 32
			AddressSize_16 = 1 << (int)AddressSize_Shift,
			AddressSize_64 = 2 << (int)AddressSize_Shift,
			AddressSize_Mask = 3 << (int)AddressSize_Shift,

			// Segment register
			Segment_Shift = AddressSize_Shift + 2,
			Segment_D = 0 << (int)Segment_Shift, // Default to data
			Segment_E = 1 << (int)Segment_Shift,
			Segment_C = 2 << (int)Segment_Shift,
			Segment_S = 3 << (int)Segment_Shift,
			Segment_F = 4 << (int)Segment_Shift,
			Segment_G = 5 << (int)Segment_Shift,
			Segment_Mask = 7 << (int)Segment_Shift,

			// Base register
			BaseReg_Shift = Segment_Shift + 3,
			BaseReg_None = 0 << (int)BaseReg_Shift, // Default to none
			BaseReg_R8 = 0x9 << (int)BaseReg_Shift,
			BaseReg_Rip = 0x12 << (int)BaseReg_Shift,
			BaseReg_Mask = 0x1F << (int)BaseReg_Shift,

			// Index register
			IndexReg_Shift = BaseReg_Shift + 5,
			IndexReg_None = 0, // Default to none
			IndexReg_Eax = (int)GprCode.Esp << (int)IndexReg_Shift,
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

		private static Flags BaseFlags(AddressSize size)
		{
			switch (size)
			{
				case AddressSize._16Bits: return Flags.AddressSize_16;
				case AddressSize._32Bits: return Flags.AddressSize_32;
				case AddressSize._64Bits: return Flags.AddressSize_64;
				default: throw new UnreachableException();
			}
		}

		private static Flags BaseFlags(AddressSize size, SegmentRegister segment)
		{
			var flags = BaseFlags(size);
			switch (segment)
			{
				case SegmentRegister.CS: flags |= Flags.Segment_C; break;
				case SegmentRegister.DS: flags |= Flags.Segment_D; break;
				case SegmentRegister.ES: flags |= Flags.Segment_E; break;
				case SegmentRegister.FS: flags |= Flags.Segment_F; break;
				case SegmentRegister.GS: flags |= Flags.Segment_G; break;
				case SegmentRegister.SS: flags |= Flags.Segment_S; break;
				default: throw new UnreachableException();
			}

			return flags;
		}
		#endregion

		#region Fields
		private readonly Flags flags;
		private readonly int displacement;
		#endregion

		#region Construction
		private EffectiveAddress(Flags flags, int displacement = 0)
		{
			this.flags = flags;
			this.displacement = displacement;
		}

		public static EffectiveAddress Absolute(AddressSize size, int address)
		{
			if (size == AddressSize._16Bits && (short)address != address)
				throw new ArgumentOutOfRangeException(nameof(address), "The address cannot be encoded with the given address size.");
			return new EffectiveAddress(BaseFlags(size) | Flags.BaseReg_None | Flags.IndexReg_None, address);
		}

		public static EffectiveAddress Absolute(AddressSize size, SegmentRegister segment, int address)
		{
			if (size == AddressSize._16Bits && (short)address != address)
				throw new ArgumentOutOfRangeException(nameof(address), "The address cannot be encoded with the given address size.");
			return new EffectiveAddress(BaseFlags(size, segment) | Flags.BaseReg_None | Flags.IndexReg_None, address);
		}

		public static EffectiveAddress Indirect(
			AddressSize addressSize, SegmentRegister? segment, 
			AddressBaseRegister? @base, GprCode? index = null, byte scale = 1, int displacement = 0)
		{
			if (scale != 1 && scale != 2 && scale != 4 && scale != 8 && (scale != 0 || !index.HasValue))
				throw new ArgumentOutOfRangeException(nameof(scale));
			if (@base == AddressBaseRegister.Rip && (addressSize == AddressSize._16Bits || index.HasValue))
				throw new ArgumentException("IP-relative addressing is incompatible with 16-bit addresses or indexed forms.");
			if (addressSize == AddressSize._16Bits)
			{
				if (@base.GetValueOrDefault() >= AddressBaseRegister.R8 || index.GetValueOrDefault() >= GprCode.R8)
					throw new ArgumentException("16-bit effective addresses cannot reference registers R8-R15.");
				if ((short)displacement != displacement)
					throw new ArgumentException("Displacement too big for 16-bit effective address.");
				if (@base.HasValue)
				{
					if (index.HasValue)
					{
						if (@base.Value != AddressBaseRegister.B && @base.Value != AddressBaseRegister.BP)
							throw new ArgumentException("Only B and BP are valid bases for 16-bit indexed effective addresses.");
						if (index.Value != GprCode.SI && index.Value != GprCode.DI)
							throw new ArgumentException("Only SI and DI are valid indices for 16-bit indexed effective addresses.");
					}
					else
					{
						if (@base != AddressBaseRegister.SI && @base != AddressBaseRegister.DI
							&& @base != AddressBaseRegister.BP && @base != AddressBaseRegister.B)
							throw new ArgumentException("Invalid base for 16-bit effective address.");
					}
				}
				else if (index.HasValue)
					throw new ArgumentException("16-bit indexed addressing only possible with a base register.");
			}
			else if (index == GprCode.Esp)
				throw new ArgumentException("ESP cannot be used as an effective addressing index.");

			// Segment defaults to D, or S if we are using a stack-pointing register
			if (!segment.HasValue)
			{
				segment = (@base == AddressBaseRegister.SP || @base == AddressBaseRegister.BP)
					? SegmentRegister.SS : SegmentRegister.DS;
			}

			var flags = BaseFlags(addressSize, segment.Value);

			if (@base.HasValue) flags |= (Flags)(((int)@base + 1) << (int)Flags.BaseReg_Shift);
			if (index.HasValue)
			{
				// Swap eax and esp (esp meaning "none")
				Debug.Assert(index.Value != GprCode.Esp);
				if (index.Value == GprCode.Eax) flags |= Flags.IndexReg_Eax;
				else flags |= (Flags)((int)index.Value << (int)Flags.IndexReg_Shift);
			}

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
			if (addressSize == AddressSize._16Bits)
				throw new ArgumentException("16-bit SIB cannot encode rip-relative effective addresses.", nameof(addressSize));
			return Indirect(addressSize, segment, AddressBaseRegister.Rip, displacement);
		}

		public static EffectiveAddress Indirect(Gpr @base, int displacement = 0)
		{
			if (@base.Size == IntegerSize.Byte)
				throw new ArgumentException("Byte register cannot be used as indirect bases.", nameof(@base));
			return Indirect(@base.Size.ToAddressSize(), null, @base.Code, null, 1, displacement);
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

			return Indirect(AddressSize._16Bits, segment, @base, index, 1, displacement);
		}

		public static EffectiveAddress FromEncoding(CodeSegmentType codeSegmentType, Encoding encoding)
		{
			if (encoding.ModRM.IsDirect)
				throw new ArgumentException("ModRM does not encode a memory operand.");

			if ((encoding.BaseRegExtension || encoding.IndexRegExtension) && codeSegmentType != CodeSegmentType._64Bits)
				throw new ArgumentException();

			var addressSize = codeSegmentType.GetEffectiveAddressSize(encoding.AddressSizeOverride);

			// Mod in { 0, 1, 2 }
			if (addressSize == AddressSize._16Bits)
			{
				if ((short)encoding.Displacement != encoding.Displacement)
					throw new ArgumentException("Displacement too big for 16-bit effective address.");
				if (encoding.ModRM.IsAbsoluteRM_16)
					return Absolute(addressSize, encoding.Displacement);

				int displacementSize = (int)encoding.ModRM.Mod;
				Debug.Assert(displacementSize != 0 || encoding.Displacement == 0);
				Debug.Assert(displacementSize != 1 || unchecked((sbyte)encoding.Displacement) == encoding.Displacement);
				return FromIndirect16Encoding(encoding.SegmentOverride,
					encoding.ModRM.RM, (short)encoding.Displacement);
			}
			else
			{
				if (encoding.ModRM.IsAbsoluteRM_32)
				{
					// Absolute in 32 bits, rip-relative in 64 bits
					return codeSegmentType.IsLongMode()
						? RipRelative(addressSize, encoding.SegmentOverride, encoding.Displacement)
						: Absolute(addressSize, encoding.Displacement);
				}

				var displacementSize = encoding.ModRM.GetDisplacementSize(addressSize, encoding.Sib.GetValueOrDefault());
				Debug.Assert(displacementSize.CanEncodeValue(encoding.Displacement));

				GprCode? baseReg = encoding.ModRM.RMGpr;

				if (baseReg != GprCode.Esp)
				{
					if (encoding.BaseRegExtension) baseReg += 8;
					return Indirect(addressSize, encoding.SegmentOverride,
						baseReg.Value, encoding.Displacement);
				}

				// Sib byte
				if (!encoding.Sib.HasValue) throw new ArgumentException();

				var sib = encoding.Sib.Value;
				baseReg = sib.GetBaseReg(encoding.ModRM);
				if (baseReg.HasValue && encoding.BaseRegExtension) baseReg += 8;

				var indexReg = sib.IndexReg;
				if (indexReg.HasValue && encoding.IndexRegExtension) indexReg += 8;
				
				return Indirect(addressSize, encoding.SegmentOverride,
					baseReg, indexReg, (byte)sib.Scale, encoding.Displacement);
			}
		}
		#endregion

		#region Properties
		public bool IsAbsolute => (flags & Flags.BaseReg_Mask) == Flags.BaseReg_None
			&& (flags & Flags.IndexReg_Mask) == Flags.IndexReg_None;
		public bool IsRipRelative => Base == AddressBaseRegister.Rip;

		public AddressSize AddressSize
		{
			get
			{
				switch (flags & Flags.AddressSize_Mask)
				{
					case Flags.AddressSize_16: return AddressSize._16Bits;
					case Flags.AddressSize_32: return AddressSize._32Bits;
					case Flags.AddressSize_64: return AddressSize._64Bits;
					default: throw new UnreachableException();
				}
			}
		}

		public SegmentRegister Segment
		{
			get
			{
				switch (flags & Flags.Segment_Mask)
				{
					case Flags.Segment_C: return SegmentRegister.CS;
					case Flags.Segment_D: return SegmentRegister.DS;
					case Flags.Segment_E: return SegmentRegister.ES;
					case Flags.Segment_F: return SegmentRegister.FS;
					case Flags.Segment_G: return SegmentRegister.GS;
					case Flags.Segment_S: return SegmentRegister.SS;
					default: throw new UnreachableException();
				}
			}
		}

		public bool RequiresSegmentOverride
		{
			get
			{
				var @base = Base;
				var defaultSegment = (@base == AddressBaseRegister.SP || @base == AddressBaseRegister.BP)
					? SegmentRegister.SS : SegmentRegister.DS;
				return Segment != defaultSegment;
			}
		}

		public AddressBaseRegister? Base
		{
			get
			{
				if ((flags & Flags.BaseReg_Mask) == Flags.BaseReg_None) return null;
				return (AddressBaseRegister)(GetFlagsField(Flags.BaseReg_Mask, Flags.BaseReg_Shift) - 1);
			}
		}

		public GprCode? BaseAsGprCode => BaseAsGpr?.Code;

		public Gpr? BaseAsGpr
		{
			get
			{
				var @base = Base;
				if (!@base.HasValue || @base == AddressBaseRegister.Rip) return null;
				return new Gpr((GprCode)@base, AddressSize.ToIntegerSize(), hasRex: false); // hasRex irrelevant for operand size >= word
			}
		}

		public bool HasIndex => (flags & Flags.IndexReg_Mask) != Flags.IndexReg_None;

		public GprCode? IndexAsGprCode
		{
			get
			{
				if (!HasIndex) return null;
				var gpr = (GprCode)GetFlagsField(Flags.IndexReg_Mask, Flags.IndexReg_Shift);
				if (gpr == GprCode.Esp) gpr = GprCode.Eax; // Due to hack to default to "none"
				return gpr;
			}
		}

		public Gpr? IndexAsGpr
		{
			get
			{
				var code = IndexAsGprCode;
				if (code == null) return null;
				return new Gpr(code.Value, AddressSize.ToIntegerSize(), hasRex: false); // hasRex irrelevant for operand size >= word
			}
		}

		public int Scale
		{
			get { return 1 << GetFlagsField(Flags.Scale_Mask, Flags.Scale_Shift); }
		}

		public int Displacement => displacement;

		public DisplacementSize MinimumDisplacementSize
		{
			get
			{
				var addressSize = AddressSize;
				if (unchecked((sbyte)displacement) != displacement || (flags & Flags.BaseReg_Mask) == Flags.BaseReg_None)
					return DisplacementSizeEnum.GetMaximum(addressSize);

				// Displacement fits in sbyte, base != none
				var @base = Base.Value;
				if (@base == AddressBaseRegister.BP) return DisplacementSize._8Bits;
				if (@base == AddressBaseRegister.Rip) return DisplacementSize._32Bits;
				return displacement == 0 ? DisplacementSize.None : DisplacementSize._8Bits;
			}
		}
		#endregion

		#region Methods
		public bool IsEncodableWithDefaultAddressSize(AddressSize defaultAddressSize)
		{
			var effectiveAddressSize = AddressSize;
			if (defaultAddressSize == AddressSize._16Bits && effectiveAddressSize == AddressSize._64Bits) return false;
			if (defaultAddressSize == AddressSize._64Bits) return effectiveAddressSize != AddressSize._16Bits;
			
			// default size = 16 or 32
			var baseFlags = flags & Flags.BaseReg_Mask;
			return (baseFlags < Flags.BaseReg_R8 || baseFlags == Flags.BaseReg_None)
				|| (flags & Flags.IndexReg_Mask) < Flags.IndexReg_R8;
		}

		public bool IsEncodableWithDisplacementSize(DisplacementSize size)
		{
			return size >= MinimumDisplacementSize
				&& (size == DisplacementSize._16Bits) == (AddressSize == AddressSize._16Bits);
		}

		public Encoding Encode(CodeSegmentType codeSegmentType, byte modReg, DisplacementSize displacementSize)
		{
			if (codeSegmentType.Supports(AddressSize, out bool addressSizeOverride))
				throw new ArgumentOutOfRangeException(nameof(codeSegmentType));

			if (modReg >= 8) throw new ArgumentOutOfRangeException(nameof(modReg));
			if (!IsEncodableWithDisplacementSize(displacementSize))
				throw new ArgumentOutOfRangeException(nameof(displacementSize));

			if (AddressSize == AddressSize._16Bits) throw new NotImplementedException();

			ModRMMod mod;
			switch (displacementSize)
			{
				case DisplacementSize.None: mod = ModRMMod.Indirect; break;
				case DisplacementSize._8Bits: mod = ModRMMod.IndirectDisp8; break;
				default: mod = ModRMMod.IndirectLongDisp; break;
			}

			var @base = Base;
			bool needsSib = false;
			if (AddressSize == AddressSize._64Bits)
			{
				// Same as 32-bit except that [disp32] encodes [rip + disp32]
				if (!@base.HasValue) needsSib = true;
				if (@base == AddressBaseRegister.Rip) @base = null;
			}

			if (@base == AddressBaseRegister.SP || (mod == 0 && @base == AddressBaseRegister.BP))
				needsSib = true;

			ModRM modRM;
			Sib? sib = null;
			if (needsSib)
			{
				if (@base == AddressBaseRegister.BP) throw new NotImplementedException();

				modRM = ModRM.WithSib(mod, modReg);
				sib = new Sib(
					scale: (SibScale)((int)(flags & Flags.Scale_Mask) >> (int)Flags.Scale_Shift),
					index: (IndexAsGprCode ?? GprCode.SP).GetLow3Bits(),
					@base: (BaseAsGprCode ?? GprCode.BP).GetLow3Bits());
			}
			else
			{
				modRM = new ModRM(mod, modReg, (byte)((byte)@base.Value & 0x7));
			}

			var encodingFlags = EncodingFlags.None;
			if (addressSizeOverride) encodingFlags |= EncodingFlags.AddressSizeOverride;
			if (BaseAsGprCode >= GprCode.R8) encodingFlags |= EncodingFlags.BaseRegExtension;
			if (IndexAsGprCode >= GprCode.R8) encodingFlags |= EncodingFlags.IndexRegExtension;
			return new Encoding(encodingFlags,
				RequiresSegmentOverride ? Segment : (SegmentRegister?)null,
				modRM, sib, Displacement);
		}

		public string ToString(bool vectorSib, ulong? rip = null)
		{
			// SS:[eax+eax*8+0x2000000000]
			var str = new StringBuilder(30);
			
			if (RequiresSegmentOverride)
			{
				str.Append(Segment.GetLetter());
				str.Append("S:");
			}

			str.Append('[');

			int displacementToPrint = Displacement;
			bool firstTerm = true;
			var @base = Base;
			if (@base.HasValue)
			{
				if (@base == AddressBaseRegister.Rip)
				{
					if (rip.HasValue)
					{
						ulong address = checked((ulong)((long)rip.Value + Displacement));
						AppendAddress(str, address);
						displacementToPrint = 0;
					}
					else str.Append("rip");
				}
				else str.Append(BaseAsGpr.Value.Name);
				firstTerm = false;
			}

			var index = IndexAsGpr;
			if (index.HasValue)
			{
				if (!firstTerm) str.Append('+');
				if (vectorSib) throw new NotImplementedException();
				str.Append(IndexAsGpr.Value.Name);
				if (Scale > 1)
				{
					str.Append('*');
					str.Append((char)('0' + Scale));
				}
				firstTerm = false;
			}

			if (firstTerm)
			{
				AppendAddress(str, checked((ulong)Displacement));
			}
			else if (displacementToPrint != 0)
			{
				if (displacementToPrint >= 0) str.Append('+');
				bool hex = displacementToPrint < -9 || displacementToPrint > 9;
				str.AppendFormat(CultureInfo.InvariantCulture, hex ? "0x{0:X}" : "{0:D}", displacementToPrint);
			}

			str.Append(']');

			return str.ToString();
		}

		private void AppendAddress(StringBuilder str, ulong address)
		{
			str.Append("0x");
			str.Append(address.ToString("X").PadLeft(AddressSize.InBytes() * 2, '0'));
		}

		public override string ToString() => ToString(vectorSib: false);

		private int GetFlagsField(Flags mask, Flags shift)
			=> (int)Bits.MaskAndShiftRight((uint)flags, (uint)mask, (int)shift);
		#endregion
	}
}
