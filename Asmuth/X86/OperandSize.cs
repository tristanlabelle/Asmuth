using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	public enum OperandSize
	{
		Byte,
		Word,
		Dword,
		Qword,
		_128,
		_256,
		_512
	}

	public static class OperandSizeEnum
	{
		[Pure]
		public static int InBytes(this OperandSize size) => 1 << (int)size;

		[Pure]
		public static int InBits(this OperandSize size) => InBytes(size) * Bits.PerByte;

		[Pure]
		public static int InBytes(this OperandSize? size) => size.HasValue ? InBytes(size.Value) : 0;

		[Pure]
		public static int InBits(this OperandSize? size) => InBytes(size) * Bits.PerByte;

		[Pure]
		public static OperandSize OverrideWordDword(this OperandSize size, bool @override)
		{
			if (size == OperandSize.Word) return @override ? OperandSize.Dword : OperandSize.Word;
			if (size == OperandSize.Dword) return @override ? OperandSize.Word : OperandSize.Dword;
			throw new ArgumentOutOfRangeException(nameof(size));
		}
	}
}
