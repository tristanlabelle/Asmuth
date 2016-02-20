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
		OperandSize_Irrelevant = 0 << (int)OperandSize_Shift,
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
		ModRM_None = 0 << (int)ModRM_Shift,	// NOP
		ModRM_Any = 1 << (int)ModRM_Shift,
		ModRM_FixedReg = 2 << (int)ModRM_Shift,
		ModRM_FixedModReg = 3 << (int)ModRM_Shift, // FADD ST(i), ST(0) : DC C0+i
		ModRM_Fixed = 4 << (int)ModRM_Shift,
		ModRM_Mask = 7 << (int)ModRM_Shift,

		// Immediate types
		FirstImmediateType_Shift = ModRM_Shift + 3,
		FirstImmediateType_None = (uint)ImmediateType.None << (int)FirstImmediateType_Shift,
		FirstImmediateType_OpcodeExtension = (uint)ImmediateType.OpcodeExtension << (int)FirstImmediateType_Shift,
		FirstImmediateType_Mask = 0x1F << (int)FirstImmediateType_Shift,

		SecondImmediateType_Shift = FirstImmediateType_Shift + 5,
		SecondImmediateType_None = (uint)ImmediateType.None << (int)SecondImmediateType_Shift,
		SecondImmediateType_Mask = 0x1F << (int)SecondImmediateType_Shift,

		ImmediateTypes_Mask = FirstImmediateType_Mask | SecondImmediateType_Mask,
	}

	public static class InstructionEncodingEnum
	{
		#region Getters
		[Pure]
		public static bool HasModRM(this InstructionEncoding encoding)
			=> (encoding & InstructionEncoding.ModRM_Mask) != InstructionEncoding.ModRM_None;

		[Pure]
		public static ImmediateType GetFirstImmediateType(this InstructionEncoding encoding)
			=> (ImmediateType)Bits.MaskAndShiftRight((uint)encoding,
				(uint)InstructionEncoding.FirstImmediateType_Mask, (int)InstructionEncoding.FirstImmediateType_Shift);

		[Pure]
		public static ImmediateType GetSecondImmediateType(this InstructionEncoding encoding)
			=> (ImmediateType)Bits.MaskAndShiftRight((uint)encoding,
				(uint)InstructionEncoding.SecondImmediateType_Mask, (int)InstructionEncoding.SecondImmediateType_Shift);

		[Pure]
		public static int GetImmediateCount(this InstructionEncoding encoding)
		{
			bool hasFirstImmediate = (encoding & InstructionEncoding.FirstImmediateType_Mask)
				!= InstructionEncoding.FirstImmediateType_None;
			if (!hasFirstImmediate) return 0;
			bool hasSecondImmediate = (encoding & InstructionEncoding.SecondImmediateType_Mask)
				!= InstructionEncoding.SecondImmediateType_None;
			return hasSecondImmediate ? 2 : 1;
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

			if ((encoding & InstructionEncoding.FirstImmediateType_Mask) == InstructionEncoding.FirstImmediateType_OpcodeExtension)
				mask |= Opcode.EVexIs4_Mask;

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
		public static InstructionEncoding WithFirstImmediateType(this InstructionEncoding encoding, ImmediateType type)
		{
			Contract.Requires(type == (type & (ImmediateType)0x1F));
			return (encoding & ~InstructionEncoding.FirstImmediateType_Mask)
				| (InstructionEncoding)((uint)type << (int)InstructionEncoding.FirstImmediateType_Shift);
		}

		[Pure]
		public static InstructionEncoding WithSecondImmediateType(this InstructionEncoding encoding, ImmediateType type)
		{
			Contract.Requires(type == (type & (ImmediateType)0x1F));
			return (encoding & ~InstructionEncoding.SecondImmediateType_Mask)
				| (InstructionEncoding)((uint)type << (int)InstructionEncoding.SecondImmediateType_Shift);
		}
		#endregion
	}
}
