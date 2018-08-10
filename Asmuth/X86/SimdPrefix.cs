using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86
{
	public enum SimdPrefix : byte
	{
		None = 0,
		_66 = 1,
		_F3 = 2, // Following VEX.pp encoding
		_F2 = 3, // Following VEX.pp encoding
	}

	public static class SimdPrefixEnum
	{
		public static byte? GetByte(this SimdPrefix value)
		{
			switch (value)
			{
				case SimdPrefix.None: return null;
				case SimdPrefix._66: return 0x66;
				case SimdPrefix._F3: return 0xF3;
				case SimdPrefix._F2: return 0xF2;
				default: throw new ArgumentOutOfRangeException(nameof(value));
			}
		}
	}
}
