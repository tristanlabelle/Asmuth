using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	public enum AddressSize : byte
	{
		_16,
		_32,
		_64
	}

	public static class AddressSizeEnum
	{
		public static OperandSize ToOperandSize(this AddressSize size)
			=> OperandSize.Word + (int)size;
		public static int InBytes(this AddressSize size)
			=> (2 << (int)size);
		public static int InBits(this AddressSize size)
			=> InBytes(size) * 8;
	}
}
