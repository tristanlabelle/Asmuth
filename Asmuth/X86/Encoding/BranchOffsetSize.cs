using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Encoding
{
	public enum BranchOffsetSize : byte
	{
		Short,
		Long16,
		Long32,
		Long16Or32 // Based on operand size
	}

	public static class BranchOffsetSizeEnum
	{
		public static bool IsFixed(this BranchOffsetSize size) => size != BranchOffsetSize.Long16Or32;

		public static IntegerSize ToIntegerSize(this BranchOffsetSize size, IntegerSize operandSize)
		{
			switch (size)
			{
				case BranchOffsetSize.Short: return IntegerSize.Byte;
				case BranchOffsetSize.Long16: return IntegerSize.Word;
				case BranchOffsetSize.Long32: return IntegerSize.Dword;
				case BranchOffsetSize.Long16Or32:
					return operandSize == IntegerSize.Word ? IntegerSize.Word : IntegerSize.Dword;
				default: throw new ArgumentOutOfRangeException(nameof(size));
			}
		}

		public static ImmediateSizeEncoding AsImmediateSize(this BranchOffsetSize size)
		{
			switch (size)
			{
				case BranchOffsetSize.Short: return ImmediateSizeEncoding.Byte;
				case BranchOffsetSize.Long16: return ImmediateSizeEncoding.Word;
				case BranchOffsetSize.Long32: return ImmediateSizeEncoding.Dword;
				case BranchOffsetSize.Long16Or32: return ImmediateSizeEncoding.WordOrDword_OperandSize;
				default: throw new ArgumentOutOfRangeException(nameof(size));
			}
		}
	}
}
