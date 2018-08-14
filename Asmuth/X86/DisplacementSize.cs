using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	public enum DisplacementSize
	{
		None,
		_8Bits,
		_16Bits, // 16-bit effective address size only
		_32Bits // 32/64-bit effective address sizes only
	}

	public static class DisplacementSizeEnum
	{
		public static int InBytes(this DisplacementSize size)
		{
			switch (size)
			{
				case DisplacementSize.None: return 0;
				case DisplacementSize._8Bits: return 1;
				case DisplacementSize._16Bits: return 2;
				case DisplacementSize._32Bits: return 4;
				default: throw new ArgumentOutOfRangeException(nameof(size));
			}
		}
		public static bool IsLong(this DisplacementSize size)
			=> size >= DisplacementSize._16Bits;
		public static DisplacementSize GetMaximum(AddressSize addressSize)
			=> addressSize == AddressSize._16Bits ? DisplacementSize._16Bits : DisplacementSize._32Bits;
		public static bool IsEncodable(this DisplacementSize size, AddressSize addressSize)
			=> (size == DisplacementSize._16Bits) == (addressSize == AddressSize._16Bits);
		public static bool CanEncodeValue(this DisplacementSize size, int displacement)
		{
			switch (size)
			{
				case DisplacementSize.None: return displacement == 0;
				case DisplacementSize._8Bits: return unchecked((sbyte)displacement) == displacement;
				case DisplacementSize._16Bits: return unchecked((short)displacement) == displacement;
				case DisplacementSize._32Bits: return true;
				default: throw new ArgumentOutOfRangeException(nameof(size));
			}
		}
	}
}
