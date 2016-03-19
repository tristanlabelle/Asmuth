using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	public enum DisplacementSize
	{
		_0,
		_8,
		_16, // 16-bit effective address size only
		_32 // 32/64-bit effective address sizes only
	}

	public static class DisplacementSizeEnum
	{
		[Pure]
		public static int InBytes(this DisplacementSize size)
		{
			switch (size)
			{
				case DisplacementSize._0: return 0;
				case DisplacementSize._8: return 1;
				case DisplacementSize._16: return 2;
				case DisplacementSize._32: return 4;
				default: throw new ArgumentOutOfRangeException(nameof(size));
			}
		}

		[Pure]
		public static bool IsLong(this DisplacementSize size)
			=> size >= DisplacementSize._16;

		[Pure]
		public static DisplacementSize GetMaximum(AddressSize addressSize)
			=> addressSize == AddressSize._16 ? DisplacementSize._16 : DisplacementSize._32;

		[Pure]
		public static bool IsEncodable(this DisplacementSize size, AddressSize addressSize)
			=> (size == DisplacementSize._16) == (addressSize == AddressSize._16);

		[Pure]
		public static bool CanEncodeValue(this DisplacementSize size, int displacement)
		{
			switch (size)
			{
				case DisplacementSize._0: return displacement == 0;
				case DisplacementSize._8: return unchecked((sbyte)displacement) == displacement;
				case DisplacementSize._16: return unchecked((short)displacement) == displacement;
				case DisplacementSize._32: return true;
				default: throw new ArgumentOutOfRangeException(nameof(size));
			}
		}
	}
}
