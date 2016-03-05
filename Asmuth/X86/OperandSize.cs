using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
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
		public static AddressSize ToAddressSize(this OperandSize size)
		{
			switch (size)
			{
				case OperandSize.Word: return AddressSize._16;
				case OperandSize.Dword: return AddressSize._32;
				case OperandSize.Qword: return AddressSize._64;
				default: throw new ArgumentOutOfRangeException(nameof(size));
			}
		}

		[Pure]
		public static int InBytes(this OperandSize size) => 1 << (int)size;

		[Pure]
		public static int InBits(this OperandSize size) => InBytes(size) * Bits.PerByte;

		[Pure]
		public static int InBytes(this OperandSize? size) => size.HasValue ? InBytes(size.Value) : 0;

		[Pure]
		public static int InBits(this OperandSize? size) => InBytes(size) * Bits.PerByte;

		[Pure]
		public static OperandSize OverrideWordDword(this OperandSize size, bool @override = true)
		{
			if (size == OperandSize.Word) return @override ? OperandSize.Dword : OperandSize.Word;
			if (size == OperandSize.Dword) return @override ? OperandSize.Word : OperandSize.Dword;
			return size;
		}
	}
}
