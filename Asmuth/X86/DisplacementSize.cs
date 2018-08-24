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
		SByte,
		SWord, // 16-bit effective address size only
		SDword // 32/64-bit effective address sizes only
	}

	public static class DisplacementSizeEnum
	{
		public static int InBytes(this DisplacementSize size)
		{
			switch (size)
			{
				case DisplacementSize.None: return 0;
				case DisplacementSize.SByte: return 1;
				case DisplacementSize.SWord: return 2;
				case DisplacementSize.SDword: return 4;
				default: throw new ArgumentOutOfRangeException(nameof(size));
			}
		}
		public static bool IsLong(this DisplacementSize size)
			=> size >= DisplacementSize.SWord;
		public static DisplacementSize GetMaximum(AddressSize addressSize)
			=> addressSize == AddressSize._16 ? DisplacementSize.SWord : DisplacementSize.SDword;
		public static bool IsEncodable(this DisplacementSize size, AddressSize addressSize)
			=> (size == DisplacementSize.SWord) == (addressSize == AddressSize._16);
		public static bool CanEncodeValue(this DisplacementSize size, int displacement)
		{
			switch (size)
			{
				case DisplacementSize.None: return displacement == 0;
				case DisplacementSize.SByte: return unchecked((sbyte)displacement) == displacement;
				case DisplacementSize.SWord: return unchecked((short)displacement) == displacement;
				case DisplacementSize.SDword: return true;
				default: throw new ArgumentOutOfRangeException(nameof(size));
			}
		}
	}
}
