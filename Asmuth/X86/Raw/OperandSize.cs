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
		_526
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
	}
}
