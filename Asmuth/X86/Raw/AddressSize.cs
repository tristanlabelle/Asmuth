using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	public enum AddressSize : byte
	{
		_16,
		_32,
		_64
	}

	public static class AddressSizeEnum
	{
		[Pure]
		public static OperandSize ToOperandSize(this AddressSize size)
			=> (OperandSize)((int)size + 1);

		[Pure]
		public static int InBytes(this AddressSize size)
			=> (2 << (int)size);

		[Pure]
		public static int InBits(this AddressSize size)
			=> InBytes(size) * 8;

		[Pure]
		public static AddressSize GetEffective(this AddressSize size, bool @override)
		{
			if (!@override) return size;
			return size == AddressSize._32 ? AddressSize._16 : AddressSize._32;
		}
	}
}
