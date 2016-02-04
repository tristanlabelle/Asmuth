using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
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

	[StructLayout(LayoutKind.Sequential, Size = 8)]
	public struct EffectiveAddress
	{
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

			// Ignored with direct addressing
			BaseReg_Shift = Segment_Shift + 3,
			BaseReg_Rip = 0x11 << (int)BaseReg_Shift,
			BaseReg_None = 0x12 << (int)BaseReg_Shift,
			BaseReg_Mask = 0x1F << (int)BaseReg_Shift,

			DirectReg_Shift = BaseReg_Shift,
			DirectReg_Mask = 0xF << DirectReg_Shift,

			// Ignored with direct or rip-relative addressing
			IndexReg_Shift = BaseReg_Shift + 5,
			IndexReg_None = (int)GprCode.Esp << (int)IndexReg_Shift, // ESP cannot be used as an index
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

		public static EffectiveAddress Direct(AddressSize size, byte register)
		{
			Contract.Requires(register < 16);
			var flags = BaseFlags(size);
			flags |= (Flags)(register << (int)Flags.DirectReg_Shift);
			return new EffectiveAddress(BaseFlags(size));
		}

		public static EffectiveAddress Direct(AddressSize size, GprCode gprCode) => Direct(size, (byte)gprCode);

		public static EffectiveAddress Absolute(AddressSize size, int address)
		{
			Contract.Requires(size > Raw.AddressSize._16 || (short)address == address);
			return new EffectiveAddress(BaseFlags(size), address);
		}

		public static EffectiveAddress Absolute(AddressSize size, SegmentRegister segment, int address)
		{
			Contract.Requires(size > Raw.AddressSize._16 || (short)address == address);
			return new EffectiveAddress(BaseFlags(size, segment), address);
		}

		public static EffectiveAddress Indirect(
			AddressSize addressSize, SegmentRegister? segment, 
			AddressBaseRegister? @base, GprCode? index = null, byte scale = 1, int displacement = 0)
		{
			Contract.Requires(scale == 1 || scale == 2 || scale == 4 || scale == 8
				|| (scale == 0 && !index.HasValue));
			Contract.Requires(@base != AddressBaseRegister.Rip || (addressSize == Raw.AddressSize._64 && index.HasValue));
			if (addressSize == Raw.AddressSize._16)
			{
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
			flags |= (Flags)((int)@base << (int)Flags.BaseReg_Shift);
			flags |= (Flags)((int)index << (int)Flags.IndexReg_Shift);
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

		public static EffectiveAddress FromEncoding(AddressSize addressSize, SegmentRegister? segment, ModRM modRM, Sib sib, int displacement)
		{
			throw new NotImplementedException();
		}
		#endregion

		#region Properties
		public bool IsDirect => (flags & Flags.AddressSize_Mask) == Flags.AddressSize_Undefined;
		public bool IsIndirect => (flags & Flags.AddressSize_Mask) != Flags.AddressSize_Undefined;

		public byte? DirectReg
		{
			get
			{
				if (IsDirect) return null;
				return (byte)GetFlagsField(Flags.DirectReg_Mask, Flags.DirectReg_Shift);
			}
		}

		public GprCode? DirectGpr => (GprCode?)DirectReg;

		public AddressSize? AddressSize
		{
			get
			{
				if (IsDirect) return null;
				return (AddressSize)GetFlagsField(Flags.AddressSize_Mask, Flags.AddressSize_Shift);
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

		public bool IsDefaultSegment
		{
			get
			{
				var @base = Base;
				return @base != AddressBaseRegister.BP && @base != AddressBaseRegister.SP;
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

		public bool IsRipRelative => Base == AddressBaseRegister.Rip;

		public Gpr? BaseAsGpr
		{
			get
			{
				var @base = Base;
				if (!@base.HasValue || @base == AddressBaseRegister.Rip) return null;
				return Gpr.FromCode((GprCode)@base, AddressSize.Value.ToOperandSize(), hasRex: false); // hasRex irrelevant for operand size >= word
			}
		}

		public Gpr? Index
		{
			get
			{
				if (IsDirect || (flags & Flags.IndexReg_Mask) == Flags.IndexReg_None) return null;
				var code = (GprCode)GetFlagsField(Flags.IndexReg_Mask, Flags.IndexReg_None);
				return Gpr.FromCode(code, AddressSize.Value.ToOperandSize(), hasRex: false); // hasRex irrelevant for operand size >= word
			}
		}

		public int Scale
		{
			get
			{
				if (IsDirect || (flags & Flags.IndexReg_Mask) == Flags.IndexReg_None) return 0;
				return 1 << GetFlagsField(Flags.Scale_Mask, Flags.Scale_Shift);
			}
		}

		public int Displacement => displacement;
		#endregion

		#region Methods
		public void Encode(byte modReg, out Rex? rex, out ModRM modRM, out Sib? sib, out OperandSize? displacementSize)
		{
			var directGpr = DirectGpr;
			if (directGpr.HasValue)
			{
				rex = directGpr.Value.RequiresRexBit() ? (Rex.Reserved_Value | Rex.BaseRegExtension) : (Rex?)null;
				modRM = ModRMEnum.FromComponents(mod: 11, reg: modReg, rm: directGpr.Value.GetLow3Bits());
				sib = null;
				displacementSize = null;
			}
			else if (AddressSize == Raw.AddressSize._16)
			{
				rex = null;
				sib = null;
			}
			else
			{
			}
			throw new NotImplementedException();
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
				if (!IsDefaultSegment)
				{
					str.Append(Segment.Value);
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

				var index = Index;
				if (index.HasValue)
				{
					if (!firstTerm) str.Append('+');
					str.Append(BaseAsGpr.Value.Name);
					str.Append('*');
					str.Append((char)('0' + Scale));
					firstTerm = false;
				}

				if (displacement != 0 || firstTerm)
				{
					if (displacement >= 0 && !firstTerm) str.Append('+');
					str.AppendFormat(CultureInfo.InvariantCulture, "D", Displacement);
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
