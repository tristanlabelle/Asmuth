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
		Fixed32,
		Fixed64,
		Operand16Or32, // imm16/32
		// Operand32Or48, // ptr16:16/16:32
		Operand16Or32Or64, // MOV B8+r imm16/32/64
		Address16Or32, // rel16/l32
		// InstructionEncoding depends on this being 3 bits
	}

	public static class ImmediateSizeEnum
	{
		[Pure]
		public static bool IsFixed(this ImmediateSize size)
			=> size <= ImmediateSize.Fixed64;

		[Pure]
		public static int InBytes(this ImmediateSize size, OperandSize operandSize, AddressSize addressSize)
		{
			Contract.Requires(operandSize >= OperandSize.Word && operandSize <= OperandSize.Qword);
			switch (size)
			{
				case ImmediateSize.Zero: return 0;
				case ImmediateSize.Fixed8: return 1;
				case ImmediateSize.Fixed16: return 2;
				case ImmediateSize.Fixed32: return 4;
				case ImmediateSize.Fixed64: return 8;
				case ImmediateSize.Operand16Or32: return operandSize == OperandSize.Word ? 2 : 4;
				case ImmediateSize.Operand16Or32Or64: return operandSize.InBytes();
				case ImmediateSize.Address16Or32: return addressSize == AddressSize._16 ? 2 : 4;
				default: throw new ArgumentException(nameof(size));
			}
		}
	}
}
