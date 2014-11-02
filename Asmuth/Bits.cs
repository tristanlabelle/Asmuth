using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	public static class Bits
	{
		[Pure]
		public static bool IsSingle(uint value)
			=> value != 0 && (value & (value - 1)) == 0;

		[Pure]
		public static bool IsSingle(ulong value)
			=> value != 0 && (value & (value - 1)) == 0;

		[Pure]
		public static uint MaskAndShiftRight(uint value, uint mask, int shift)
			=> (value & mask) >> shift;

		[Pure]
		public static ulong MaskAndShiftRight(ulong value, ulong mask, int shift)
			=> (value & mask) >> shift;

		[Pure]
		public static bool IsContiguous(uint value)
			=> (value & unchecked(value + 1)) == 0;

		[Pure]
		public static bool IsContiguous(ulong value)
			=> (value & unchecked(value + 1)) == 0;

		[Pure]
		public static uint SetMask(uint value, uint mask, uint newValue)
			=> (value & ~mask) | (newValue & mask);
	}
}
