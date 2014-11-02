using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	[Flags]
	public enum InstructionEncoding : ulong
	{
		// The xex type of the opcode
		XexType_Shift = 0,
		XexType_Legacy = 0 << (int)XexType_Shift,
		XexType_Vex = 1 << (int)XexType_Shift,
		XexType_Xop = 2 << (int)XexType_Shift,
		XexType_EVex = 3 << (int)XexType_Shift,
		XexType_Mask = 3 << (int)XexType_Shift,

		// The SIMD prefix (through a prefix or VEX/XOP/EVEX.pp)
		SimdPrefix_Shift = XexType_Shift + 2,
		SimdPrefix_None = 0 << (int)SimdPrefix_Shift,
		SimdPrefix_66 = 1 << (int)SimdPrefix_Shift,
		SimdPrefix_F2 = 2 << (int)SimdPrefix_Shift,
		SimdPrefix_F3 = 3 << (int)SimdPrefix_Shift,
		SimdPrefix_Mask = 3 << (int)SimdPrefix_Shift,

		// The opcode map (through prefixes, VEX/XOP/EVEX.mm[mmm])
		OpcodeMap_Shift = SimdPrefix_Shift + 2,
		OpcodeMap_OneByte = 0 << (int)OpcodeMap_Shift,
		OpcodeMap_0F = 1 << (int)OpcodeMap_Shift,
		OpcodeMap_0F38 = 2 << (int)OpcodeMap_Shift,
		OpcodeMap_0F3A = 3 << (int)OpcodeMap_Shift,
		OpcodeMap_Xop8 = 8 << (int)OpcodeMap_Shift,
		OpcodeMap_Xop9 = 9 << (int)OpcodeMap_Shift,
		OpcodeMap_Xop10 = 10 << (int)OpcodeMap_Shift,
		OpcodeMap_Mask = 0x1F << (int)OpcodeMap_Shift,

		// The instruction's possible operand size
		OperandSize_Shift = OpcodeMap_Shift + 5,
		OperandSize_Irrelevant = 0 << (int)OperandSize_Shift,
		OperandSize_Fixed8 = 1 << (int)OperandSize_Shift,
		OperandSize_Fixed16 = 2 << (int)OperandSize_Shift,
		OperandSize_Fixed32 = 3 << (int)OperandSize_Shift,
		OperandSize_Fixed64 = 4 << (int)OperandSize_Shift,
		OperandSize_16To64 = 5 << (int)OperandSize_Shift,
		OperandSize_32Or64 = 6 << (int)OperandSize_Shift,
		OperandSize_64Or16 = 7 << (int)OperandSize_Shift,
		OperandSize_Mask = 7 << (int)OperandSize_Shift,

		// What the REX.W field encodes
		RexW_Shift = OperandSize_Shift + 3,
		RexW_Ignored = 0 << (int)RexW_Shift,
		RexW_OperandSize = 1 << (int)RexW_Shift,
		RexW_0 = 2 << (int)RexW_Shift,
		RexW_1 = 3 << (int)RexW_Shift,
		RexW_Mask = 3 << (int)RexW_Shift,

		// What the VEX.L / EVEX.L'L fields encode
		VexL_Shift = RexW_Shift + 2,
		VexL_0 = 0 << (int)VexL_Shift,
		VexL_1 = 1 << (int)VexL_Shift,
		VexL_128 = VexL_0,
		VexL_256 = VexL_1,
		VexL_VectorLength_128Or256 = 2 << (int)VexL_Shift,
		VexL_VectorLength_128Or256Or512 = 3 << (int)VexL_Shift,
		VexL_Ignored = 4 << (int)VexL_Shift,
		VexL_RoundingControl = 5 << (int)VexL_Shift,
		VexL_Mask = 7 << (int)VexL_Shift,

		// Which bits encode the opcode itself
		OpcodeForm_Shift = VexL_Shift + 3,
		OpcodeForm_OneByte = 0 << (int)OpcodeForm_Shift,
		OpcodeForm_EmbeddedRegister = 1 << (int)OpcodeForm_Shift, // Low three bits of main byte specify a register
		OpcodeForm_EmbeddedCondition = 2 << (int)OpcodeForm_Shift, // Low four bits of main byte specify a condition code
		OpcodeForm_OneByte_WithModRM = 3 << (int)OpcodeForm_Shift,
        OpcodeForm_ExtendedByModReg = 4 << (int)OpcodeForm_Shift,
		OpcodeForm_ExtendedByModRM = 5 << (int)OpcodeForm_Shift,
		OpcodeForm_ExtendedBy3DNowImm8 = 6 << (int)OpcodeForm_Shift,
		OpcodeForm_Mask = 7 << (int)OpcodeForm_Shift,

		// The number of bytes of immediate value
		ImmediateSize_Shift = OpcodeForm_Shift + 3,
		ImmediateSize_0 = 0 << (int)ImmediateSize_Shift,
		ImmediateSize_8 = 1 << (int)ImmediateSize_Shift,
		ImmediateSize_16 = 2 << (int)ImmediateSize_Shift, // RET
		ImmediateSize_32 = 3 << (int)ImmediateSize_Shift,
		ImmediateSize_48 = 4 << (int)ImmediateSize_Shift,
		ImmediateSize_64 = 5 << (int)ImmediateSize_Shift,
		ImmediateSize_16Or32 = 6 << (int)ImmediateSize_Shift,
		ImmediateSize_16Or32Or64 = 7 << (int)ImmediateSize_Shift,
		ImmediateSize_32Or48 = 8 << (int)ImmediateSize_Shift, // CALL (9A)
		ImmediateSize_Mask = 0xF << (int)ImmediateSize_Shift,

		// The opcode main byte value
		OpcodeByte_Shift = ImmediateSize_Shift + 3,
		OpcodeByte_NoEmbeddedRegisterMask = 0xF8 << (int)OpcodeByte_Shift,
		OpcodeByte_NoEmbeddedConditionCodeMask = 0xF0 << (int)OpcodeByte_Shift,
		OpcodeByte_Mask = 0xFF << (int)OpcodeByte_Shift,

		// The opcode extra byte value (ModRM or 3DNow! imm8, not always used)
		OpcodeExtraByte_Shift = OpcodeByte_Shift + 8,
		OpcodeExtraByte_Mask = 0xFFUL << (int)OpcodeExtraByte_Shift,
	}

	public static class InstructionEncodingEnum
	{
		[Pure]
		public static byte GetOpcodeByte(this InstructionEncoding encoding)
			=> (byte)Bits.MaskAndShiftRight((ulong)encoding, (ulong)InstructionEncoding.OpcodeByte_Mask, (int)InstructionEncoding.OpcodeByte_Shift);

		[Pure]
		public static byte GetOpcodeExtraByte(this InstructionEncoding encoding)
			=> (byte)Bits.MaskAndShiftRight((ulong)encoding, (ulong)InstructionEncoding.OpcodeExtraByte_Mask, (int)InstructionEncoding.OpcodeExtraByte_Shift);

		[Pure]
		public static bool HasModRM(this InstructionEncoding encoding)
		{
			switch (encoding & InstructionEncoding.OpcodeForm_Mask)
			{
				case InstructionEncoding.OpcodeForm_OneByte_WithModRM:
				case InstructionEncoding.OpcodeForm_ExtendedByModReg:
				case InstructionEncoding.OpcodeForm_ExtendedByModRM:
					return true;

				default:
					return false;
			}
		}

		[Pure]
		public static string ToIntelStyleString(this InstructionEncoding encoding)
		{
			var str = new StringBuilder(30);

			var xexType = encoding & InstructionEncoding.XexType_Mask;
			if (xexType == InstructionEncoding.XexType_Legacy)
			{
				// Prefixes
				switch (encoding & InstructionEncoding.SimdPrefix_Mask)
				{
					case InstructionEncoding.SimdPrefix_66: str.Append("66 "); break;
					case InstructionEncoding.SimdPrefix_F2: str.Append("F2 "); break;
					case InstructionEncoding.SimdPrefix_F3: str.Append("F3 "); break;
				}

				if ((encoding & InstructionEncoding.RexW_Mask) == InstructionEncoding.RexW_1)
					str.Append("REX.W ");

				switch (encoding & InstructionEncoding.OpcodeMap_Mask)
				{
					case InstructionEncoding.OpcodeMap_0F: str.Append("0F "); break;
					case InstructionEncoding.OpcodeMap_0F38: str.Append("0F38 "); break;
					case InstructionEncoding.OpcodeMap_0F3A: str.Append("0F3A "); break;
				}

				// The opcode itself
				str.AppendFormat(CultureInfo.InvariantCulture, "X2", GetOpcodeByte(encoding));

				// Suffixes
				switch (encoding & InstructionEncoding.OpcodeForm_Mask)
				{
					case InstructionEncoding.OpcodeForm_EmbeddedRegister: str.Append("+r"); break;
					case InstructionEncoding.OpcodeForm_EmbeddedCondition: str.Append("+cc"); break;
					case InstructionEncoding.OpcodeForm_OneByte_WithModRM: str.Append(" /r"); break;
					case InstructionEncoding.OpcodeForm_ExtendedByModReg:
						str.Append('/');
						str.AppendFormat(CultureInfo.InvariantCulture, "D", GetOpcodeExtraByte(encoding) >> 3);
						break;
					case InstructionEncoding.OpcodeForm_ExtendedByModRM:
						str.AppendFormat(CultureInfo.InvariantCulture, "X2", GetOpcodeExtraByte(encoding));
						break;
				}

				throw new NotImplementedException();
			}
			else
			{
				throw new NotImplementedException();
			}

			return str.ToString();
		}
	}
}
