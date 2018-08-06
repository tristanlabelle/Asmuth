using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	public static class Bits
	{
		public const int PerByte = 8;
		public static bool IsSingle(uint value)
			=> value != 0 && (value & (value - 1)) == 0;
		public static bool IsSingle(ulong value)
			=> value != 0 && (value & (value - 1)) == 0;
		public static uint MaskAndShiftRight(uint value, uint mask, int shift)
			=> (value & mask) >> shift;
		public static ulong MaskAndShiftRight(ulong value, ulong mask, int shift)
			=> (value & mask) >> shift;
		public static bool IsContiguous(uint value)
			=> (value & unchecked(value + 1)) == 0;
		public static bool IsContiguous(ulong value)
			=> (value & unchecked(value + 1)) == 0;
		public static uint SetMask(uint value, uint mask, uint newValue)
			=> (value & ~mask) | (newValue & mask);
	}
}
