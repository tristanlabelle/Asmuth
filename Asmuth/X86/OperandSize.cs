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
		public static int InBytes(this OperandSize size) => 1 << (int)size;
		public static int InBits(this OperandSize size) => InBytes(size) * Bits.PerByte;
		public static int InBytes(this OperandSize? size) => size.HasValue ? InBytes(size.Value) : 0;
		public static int InBits(this OperandSize? size) => InBytes(size) * Bits.PerByte;
	}
}
