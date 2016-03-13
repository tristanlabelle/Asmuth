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
				case AddressSize._16: return Flags.AddressSize_16;
				case AddressSize._32: return Flags.AddressSize_32;
				case AddressSize._64: return Flags.AddressSize_64;
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
			Contract.Requires(size > AddressSize._16 || (short)address == address);
			return new EffectiveAddress(BaseFlags(size) | Flags.BaseReg_None | Flags.IndexReg_None, address);
		}

		public static EffectiveAddress Absolute(AddressSize size, SegmentRegister segment, int address)
		{
			Contract.Requires(size > AddressSize._16 || (short)address == address);
			return new EffectiveAddress(BaseFlags(size, segment) | Flags.BaseReg_None | Flags.IndexReg_None, address);
		}

		public static EffectiveAddress Indirect(
			AddressSize addressSize, SegmentRegister? segment, 
			AddressBaseRegister? @base, GprCode? index = null, byte scale = 1, int displacement = 0)
		{
			Contract.Requires(scale == 1 || scale == 2 || scale == 4 || scale == 8
				|| (scale == 0 && !index.HasValue));
			Contract.Requires(@base != AddressBaseRegister.Rip || (addressSize != AddressSize._16 && index.HasValue));
			if (addressSize == AddressSize._16)
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

			if (@base.HasValue) flags |= (Flags)(((int)@base + 1) << (int)Flags.BaseReg_Shift);
			if (index.HasValue)
			{
				// Swap eax and esp (esp meaning "none")
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
			Contract.Requires(addressSize != AddressSize._16);
			return Indirect(addressSize, segment, AddressBaseRegister.Rip, displacement);
		}

		public static EffectiveAddress Indirect(Gpr @base, int displacement = 0)
		{
			Contract.Assert(@base.Size != OperandSize.Byte);
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

			return Indirect(AddressSize._16, segment, @base, index, 1, displacement);
		}

		public static EffectiveAddress FromEncoding(AddressSize defaultAddressSize, Encoding encoding)
		{
			if ((encoding.ModRM & ModRM.Mod_Mask) == ModRM.Mod_Direct)
				throw new ArgumentException("ModRM does not encode a memory operand.");

			var addressSize = defaultAddressSize.GetEffective(encoding.AddressSizeOverride);

			// Mod in { 0, 1, 2 }
			if (addressSize == AddressSize._16)
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
					return defaultAddressSize == AddressSize._64
						? RipRelative(addressSize, encoding.Segment, encoding.Displacement)
						: Absolute(addressSize, encoding.Displacement);
				}

				var displacementSize = encoding.ModRM.GetDisplacementSize(encoding.Sib.GetValueOrDefault(), addressSize);
				Contract.Assert(displacementSize.CanEncodeValue(encoding.Displacement));

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

		public static EffectiveAddress FromEncoding(CodeContext decodingMode, Encoding encoding)
			=> FromEncoding(decodingMode.GetDefaultAddressSize(), encoding);
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
					case Flags.AddressSize_16: return AddressSize._16;
					case Flags.AddressSize_32: return AddressSize._32;
					case Flags.AddressSize_64: return AddressSize._64;
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
				return Gpr.FromCode((GprCode)@base, AddressSize.ToOperandSize(), hasRex: false); // hasRex irrelevant for operand size >= word
			}
		}

		public GprCode? IndexAsGprCode
		{
			get
			{
				if ((flags & Flags.IndexReg_Mask) == Flags.IndexReg_None) return null;
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
				return Gpr.FromCode(code.Value, AddressSize.ToOperandSize(), hasRex: false); // hasRex irrelevant for operand size >= word
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
				if ((displacement & 0xFFFFFF00) != 0 || (flags & Flags.BaseReg_Mask) == Flags.BaseReg_None)
					return DisplacementSizeEnum.GetMaximum(addressSize);

				// Displacement fits in sbyte, base != none
				var @base = Base.Value;
				if (@base == AddressBaseRegister.BP) return DisplacementSize._8;
				if (@base == AddressBaseRegister.Rip) return DisplacementSize._32;
				return displacement == 0 ? DisplacementSize._0 : DisplacementSize._8;
			}
		}
		#endregion

		#region Methods
		public bool IsEncodableWithDefaultAddressSize(AddressSize defaultAddressSize)
		{
			var effectiveAddressSize = AddressSize;
			if (defaultAddressSize == AddressSize._16 && effectiveAddressSize == AddressSize._64) return false;
			if (defaultAddressSize == AddressSize._64) return effectiveAddressSize != AddressSize._16;
			
			// default size = 16 or 32
			var baseFlags = flags & Flags.BaseReg_Mask;
			return (baseFlags < Flags.BaseReg_R8 || baseFlags == Flags.BaseReg_None)
				|| (flags & Flags.IndexReg_Mask) < Flags.IndexReg_R8;
		}

		public bool IsEncodableWithDisplacementSize(DisplacementSize size)
		{
			return size >= MinimumDisplacementSize
				&& (size == DisplacementSize._16) == (AddressSize == AddressSize._16);
		}

		public Encoding Encode(AddressSize defaultAddressSize, byte modReg, DisplacementSize displacementSize)
		{
			if ((defaultAddressSize == AddressSize._16 && AddressSize == AddressSize._64)
				|| (defaultAddressSize == AddressSize._64 && AddressSize == AddressSize._16))
				throw new ArgumentException(nameof(defaultAddressSize));

			if (modReg >= 8) throw new ArgumentException(nameof(modReg));
			if (!IsEncodableWithDisplacementSize(displacementSize))
				throw new ArgumentException(nameof(displacementSize));

			var encoding = new Encoding()
			{
				Segment = RequiresSegmentOverride ? Segment : (SegmentRegister?)null,
				AddressSizeOverride = AddressSize != defaultAddressSize,
				BaseRegExtension = BaseAsGprCode >= GprCode.R8,
				IndexRegExtension = IndexAsGprCode >= GprCode.R8,
				Displacement = displacement
			};

			if (AddressSize == AddressSize._16) throw new NotImplementedException();

			byte mod;
			switch (displacementSize)
			{
				case DisplacementSize._0: mod = 0; break;
				case DisplacementSize._8: mod = 1; break;
				default: mod = 2; break;
			}

			var @base = Base;
			bool needsSib = false;
			if (AddressSize == AddressSize._64)
			{
				// Same as 32-bit except that [disp32] encodes [rip + disp32]
				if (!@base.HasValue) needsSib = true;
				if (@base == AddressBaseRegister.Rip) @base = null;
			}

			if (@base == AddressBaseRegister.SP || (mod == 0 && @base == AddressBaseRegister.BP))
				needsSib = true;

			if (needsSib)
			{
				if (@base == AddressBaseRegister.BP) throw new NotImplementedException();

				encoding.ModRM = ModRMEnum.FromComponents(mod, modReg, 0) | ModRM.RM_Sib;

				throw new NotImplementedException();
			}
			else
			{
				encoding.ModRM = ModRMEnum.FromComponents(mod, modReg, (byte)((byte)@base.Value & 0x7));
			}

			return encoding;
		}

		public string ToString(bool vectorSib)
		{
			// SS:[EAX+EAX*8+0x2000000000]
			var str = new StringBuilder(30);
			
			if (RequiresSegmentOverride)
			{
				str.Append(Segment.GetLetter());
				str.Append("S:");
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
				if (vectorSib) throw new NotImplementedException();
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

			return str.ToString();
		}

		public override string ToString() => ToString(vectorSib: false);

		private int GetFlagsField(Flags mask, Flags shift)
			=> (int)Bits.MaskAndShiftRight((uint)flags, (uint)mask, (int)shift);
		#endregion
	}
}
