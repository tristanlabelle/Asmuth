using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	// We cheat a bit here:
	// VEX only defines 1, 2, 3, but we use 0 to mean no leading byte,
	// and include AMD's XOP prefixes maps as well since they don't overlap.
	public enum OpcodeMap : byte
	{
		Default = 0,
		Escape0F = 1,
		Escape0F38 = 2,
		Escape0F3A = 3,
		Xop8 = 8,
		Xop9 = 9
	}

	public static class OpcodeMapEnum
	{
		[Pure]
		public static int GetEscapeByteCount(this OpcodeMap map)
		{
			switch (map)
			{
				case OpcodeMap.Default: return 0;
				case OpcodeMap.Escape0F: return 1;
				case OpcodeMap.Escape0F38: return 2;
				case OpcodeMap.Escape0F3A: return 2;
				default: throw new ArgumentException(nameof(map));
			}
		}

		[Pure]
		public static bool IsEncodableAsEscapeBytes(this OpcodeMap map)
			=> map <= OpcodeMap.Escape0F3A;
	}
}
