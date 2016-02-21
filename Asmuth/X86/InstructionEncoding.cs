using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	/// <summary>
	/// Describes the encoding of an instruction in a way that complements a <see cref="Opcode"/> value.
	/// </summary>
	[Flags]
	public enum InstructionEncoding : uint
	{
		// The instruction's possible operand sizes
		OperandSize_Shift = 0,
		OperandSize_Ignored = 0 << (int)OperandSize_Shift,
		OperandSize_Fixed8 = 1 << (int)OperandSize_Shift,
		OperandSize_Fixed16 = 2 << (int)OperandSize_Shift,
		OperandSize_Fixed32 = 3 << (int)OperandSize_Shift,
		OperandSize_Fixed64 = 4 << (int)OperandSize_Shift,
		OperandSize_16Or32 = 5 << (int)OperandSize_Shift,
		OperandSize_32Or64 = 6 << (int)OperandSize_Shift,
		OperandSize_16Or32Or64 = 7 << (int)OperandSize_Shift,
		OperandSize_Mask = 7 << (int)OperandSize_Shift,

		// How the REX.W field is used
		RexW_Shift = OperandSize_Shift + 3,
		RexW_Ignored = 0 << (int)RexW_Shift,
		RexW_Fixed = 1 << (int)RexW_Shift,
		RexW_OperandSize = 2 << (int)RexW_Shift,
		RexW_Mask = 3 << (int)RexW_Shift,

		// How the VEX.L / EVEX.L'L fields are used
		VexL_Shift = RexW_Shift + 2,
		VexL_Ignored = 0 << (int)VexL_Shift,
		VexL_Fixed = 1 << (int)VexL_Shift,
		VexL_VectorLength_128Or256 = 2 << (int)VexL_Shift,
		VexL_VectorLength_128Or256Or512 = 3 << (int)VexL_Shift,
		VexL_RoundingControl = 4 << (int)VexL_Shift,
		VexL_Mask = 7 << (int)VexL_Shift,

		// How the opcode byte is used
		OpcodeFormat_Shift = VexL_Shift + 3,
		OpcodeFormat_FixedByte = 0 << (int)OpcodeFormat_Shift, // Full byte is fixed
		OpcodeFormat_EmbeddedRegister = 1 << (int)OpcodeFormat_Shift, // Low three bits of main byte specify a register
		OpcodeFormat_EmbeddedConditionCode = 2 << (int)OpcodeFormat_Shift, // Low four bits of main byte specify a condition code
		OpcodeFormat_Mask = 3 << (int)OpcodeFormat_Shift,

		// How the ModRM (and sib bytes) are used
		ModRM_Shift = OpcodeFormat_Shift + 2,
		ModRM_None = 0 << (int)ModRM_Shift, // NOP
		ModRM_Any = 1 << (int)ModRM_Shift,
		ModRM_FixedReg = 2 << (int)ModRM_Shift,
		ModRM_FixedModReg = 3 << (int)ModRM_Shift, // FADD ST(i), ST(0) : DC C0+i
		ModRM_Fixed = 4 << (int)ModRM_Shift,
		ModRM_Mask = 7 << (int)ModRM_Shift,

		// Immediate sizes
		FirstImmediateSize_Shift = ModRM_Shift + 3,
		FirstImmediateSize_Mask = 0x7 << (int)FirstImmediateSize_Shift,
		SecondImmediateSize_Shift = FirstImmediateSize_Shift + 3,
		SecondImmediateSize_Mask = 0x7 << (int)SecondImmediateSize_Shift,
		ImmediateSizes_Mask = FirstImmediateSize_Mask | SecondImmediateSize_Mask,
	}

	public static class InstructionEncodingEnum
	{
		#region Getters
		[Pure]
		public static bool HasModRM(this InstructionEncoding encoding)
			=> (encoding & InstructionEncoding.ModRM_Mask) != InstructionEncoding.ModRM_None;

		[Pure]
		public static ImmediateSize GetFirstImmediateSize(this InstructionEncoding encoding)
			=> (ImmediateSize)Bits.MaskAndShiftRight((uint)encoding,
				(uint)InstructionEncoding.FirstImmediateSize_Mask, (int)InstructionEncoding.FirstImmediateSize_Shift);

		[Pure]
		public static ImmediateSize GetSecondImmediateSize(this InstructionEncoding encoding)
			=> (ImmediateSize)Bits.MaskAndShiftRight((uint)encoding,
				(uint)InstructionEncoding.SecondImmediateSize_Mask, (int)InstructionEncoding.SecondImmediateSize_Shift);

		[Pure]
		public static int GetImmediateCount(this InstructionEncoding encoding)
		{
			if ((encoding & InstructionEncoding.FirstImmediateSize_Mask) == 0) return 0;
			if ((encoding & InstructionEncoding.SecondImmediateSize_Mask) == 0) return 1;
			return 2;
		}

		[Pure]
		public static int GetImmediatesSizeInBytes(this InstructionEncoding encoding,
			OperandSize operandSize, AddressSize addressSize)
		{
			Contract.Requires(operandSize >= OperandSize.Word && operandSize <= OperandSize.Qword);
			return GetFirstImmediateSize(encoding).InBytes(operandSize, addressSize)
				+ GetSecondImmediateSize(encoding).InBytes(operandSize, addressSize);
		}

		[Pure]
		public static byte GetOpcodeMainByteFixedMask(this InstructionEncoding encoding)
		{
			switch (encoding & InstructionEncoding.OpcodeFormat_Mask)
			{
				case InstructionEncoding.OpcodeFormat_FixedByte: return 0xFF;
				case InstructionEncoding.OpcodeFormat_EmbeddedRegister: return 0xF8;
				case InstructionEncoding.OpcodeFormat_EmbeddedConditionCode: return 0xF0;
				default: throw new UnreachableException();
			}
		}

		[Pure]
		public static Opcode GetOpcodeFixedMask(this InstructionEncoding encoding)
		{
			var mask = Opcode.SimdPrefix_Mask | Opcode.XexType_Mask | Opcode.Map_Mask;

			if ((encoding & InstructionEncoding.RexW_Mask) == InstructionEncoding.RexW_Fixed)
				mask |= Opcode.RexW;

			if ((encoding & InstructionEncoding.VexL_Mask) == InstructionEncoding.VexL_Fixed)
				mask |= Opcode.VexL_Mask;

			switch (encoding & InstructionEncoding.OpcodeFormat_Mask)
			{
				case InstructionEncoding.OpcodeFormat_FixedByte: mask |= Opcode.MainByte_Mask; break;
				case InstructionEncoding.OpcodeFormat_EmbeddedRegister: mask |= Opcode.MainByte_High5Mask; break;
				case InstructionEncoding.OpcodeFormat_EmbeddedConditionCode: mask |= Opcode.MainByte_High4Mask; break;
				default: throw new UnreachableException();
			}

			switch (encoding & InstructionEncoding.ModRM_Mask)
			{
				case InstructionEncoding.ModRM_None: break;
				case InstructionEncoding.ModRM_Any: break;
				case InstructionEncoding.ModRM_Fixed: mask |= Opcode.ModRM_Mask; break;
				case InstructionEncoding.ModRM_FixedReg: mask |= Opcode.ModRM_RegMask; break;
				case InstructionEncoding.ModRM_FixedModReg: mask |= Opcode.ModRM_ModRegMask; break;
				default: throw new UnreachableException();
			}

			return mask;
		}
		#endregion

		#region With***
		[Pure]
		public static InstructionEncoding WithOperandSize(this InstructionEncoding encoding, InstructionEncoding operandSize)
		{
			Contract.Requires((operandSize & ~InstructionEncoding.OperandSize_Mask) == 0);
			return (encoding & ~InstructionEncoding.OperandSize_Mask) | operandSize;
		}

		[Pure]
		public static InstructionEncoding WithOpcodeFormat(this InstructionEncoding encoding, InstructionEncoding format)
		{
			Contract.Requires((format & ~InstructionEncoding.OpcodeFormat_Mask) == 0);
			return (encoding & ~InstructionEncoding.OpcodeFormat_Mask) | format;
		}

		[Pure]
		public static InstructionEncoding WithModRM(this InstructionEncoding encoding, InstructionEncoding format)
		{
			Contract.Requires((format & ~InstructionEncoding.ModRM_Mask) == 0);
			return (encoding & ~InstructionEncoding.ModRM_Mask) | format;
		}

		[Pure]
		public static InstructionEncoding WithFirstImmediateSize(this InstructionEncoding encoding, ImmediateSize size)
		{
			return (encoding & ~InstructionEncoding.FirstImmediateSize_Mask)
				| (InstructionEncoding)((uint)size << (int)InstructionEncoding.FirstImmediateSize_Shift);
		}

		[Pure]
		public static InstructionEncoding WithSecondImmediateSize(this InstructionEncoding encoding, ImmediateSize size)
		{
			return (encoding & ~InstructionEncoding.SecondImmediateSize_Mask)
				| (InstructionEncoding)((uint)size << (int)InstructionEncoding.SecondImmediateSize_Shift);
		}

		[Pure]
		public static InstructionEncoding WithImmediateSizes(this InstructionEncoding encoding,
			ImmediateSize firstSize, ImmediateSize secondSize = ImmediateSize.Zero)
		{
			return (encoding & ~InstructionEncoding.ImmediateSizes_Mask)
				| (InstructionEncoding)((uint)firstSize << (int)InstructionEncoding.FirstImmediateSize_Shift)
				| (InstructionEncoding)((uint)secondSize << (int)InstructionEncoding.SecondImmediateSize_Shift);
		}
		#endregion
	}
}
