using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86
{
    public enum IntegerSize : byte
	{
		Byte,
		Word,
		Dword,
		Qword
	}

	public static class IntegerSizeEnum
	{
		public static IntegerSize? TryFromBits(int value)
		{
			if (value == 8) return IntegerSize.Byte;
			if (value == 16) return IntegerSize.Word;
			if (value == 32) return IntegerSize.Dword;
			if (value == 64) return IntegerSize.Qword;
			return null;
		}

		public static IntegerSize? TryFromBytes(int value) => TryFromBits(value * 8);

		public static int InBytes(this IntegerSize size) => 1 << (int)size;
		public static int InBits(this IntegerSize size) => InBytes(size) * 8;

		public static AddressSize ToAddressSize(this IntegerSize size)
		{
			if (size == IntegerSize.Byte) throw new ArgumentOutOfRangeException(nameof(size));
			return (AddressSize)((int)AddressSize._16Bits + (size - IntegerSize.Word));
		}
	}
}
