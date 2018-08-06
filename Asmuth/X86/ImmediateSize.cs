using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	public enum ImmediateSize
	{
		Zero = 0,
		Fixed8,
		Fixed16,
		Fixed24, // ENTER: C8 imm16, imm8
		Fixed32,
		Fixed64,
		Operand16Or32, // imm16/32, rel16/32
		Operand16Or32Or64, // MOV: B8+r imm16/32/64
		Operand32Or48, // JMP: EA ptr16:16/16:32
	}

	public static class ImmediateSizeEnum
	{
		public static bool IsFixed(this ImmediateSize size)
			=> size <= ImmediateSize.Fixed64;
		public static int InBytes(this ImmediateSize size, OperandSize operandSize)
		{
			Contract.Requires(operandSize >= OperandSize.Word && operandSize <= OperandSize.Qword);
			switch (size)
			{
				case ImmediateSize.Zero: return 0;
				case ImmediateSize.Fixed8: return 1;
				case ImmediateSize.Fixed16: return 2;
				case ImmediateSize.Fixed24: return 3;
				case ImmediateSize.Fixed32: return 4;
				case ImmediateSize.Fixed64: return 8;
				case ImmediateSize.Operand16Or32: return operandSize == OperandSize.Word ? 2 : 4;
				case ImmediateSize.Operand16Or32Or64: return operandSize.InBytes();
				case ImmediateSize.Operand32Or48: return operandSize == OperandSize.Word ? 4 : 6;
				default: throw new ArgumentException(nameof(size));
			}
		}
	}
}
