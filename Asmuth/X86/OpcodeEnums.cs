using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86
{
	/// <summary>
	/// X86 opcodes of the form 00XXXYYY are for the arithmetic und logic unit,
	/// and the XXX bits encode the operation.
	/// </summary>
	public enum AluBinaryOpcode
	{
		Add = 0b000,
		Or = 0b001,
		Adc = 0b010,
		Sbb = 0b011,
		And = 0b100,
		Sub = 0b101,
		Xor = 0b110,
		Cmp = 0b111
	}

	public enum AluBinaryOperandMode
	{
		RM_R_8 = 0b000,
		RM_R = 0b001,
		R_RM_8 = 0b010,
		R_RM = 0b011,
		A_I_8 = 0b100,
		A_I = 0b101,
		// 0b110 and 0b111 are not ALU
	}

	public static class AluBinaryOpcodeEnum
	{
		public static bool TryDecode(byte opcode,
			out AluBinaryOpcode aluOpcode, out AluBinaryOperandMode operandMode)
		{
			if ((opcode & 0b11_000_000) != 0 || (opcode & 0b111) >= 0b110)
			{
				aluOpcode = default;
				operandMode = default;
				return false;
			}

			aluOpcode = (AluBinaryOpcode)((opcode & 0b00_111_000) >> 3);
			operandMode = (AluBinaryOperandMode)(opcode & 0b00_000_111);
			return true;
		}

		public static bool Is8Bits(this AluBinaryOperandMode mode) => ((byte)mode & 1) == 0;

		public static bool HasModRM(this AluBinaryOperandMode mode) => (byte)mode < 0b100;

		public static int GetImmediateSizeInBytes(this AluBinaryOperandMode mode, bool default32 = true)
		{
			if (mode == AluBinaryOperandMode.A_I_8) return 1;
			if (mode == AluBinaryOperandMode.A_I) return default32 ? 4 : 2;
			return 0;
		}

	}

	public enum ShiftRotateOpcode
	{
		Rol = 0b000,
		Ror = 0b001,
		Rcl = 0b010,
		Rcr = 0b011,
		Shl = 0b100, Sal = 0b100,
		Shr = 0b101,
		// 0b110 = ???
		Sar = 0b111
	}
}
