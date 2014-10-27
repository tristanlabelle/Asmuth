using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	[Flags]
	public enum Sib : byte
	{
		Base_Shift = 0,
		Base_Mask = 7 << Base_Shift,

		Index_Shift = 3,
		Index_Mask = 7 << Index_Shift,

		Scale_Shift = 6,
		Scale_1 = 0 << Scale_Shift,
		Scale_2 = 1 << Scale_Shift,
		Scale_4 = 2 << Scale_Shift,
		Scale_8 = 3 << Scale_Shift,
		Scale_Mask = 3 << Scale_Shift,
	}

	public static class SibEnum
	{
		[Pure]
		public static byte GetBase(this Sib sib)
			=> (byte)((uint)(sib & Sib.Base_Mask) << (int)Sib.Base_Shift);

		[Pure]
		public static byte GetIndex(this Sib sib)
			=> (byte)((uint)(sib & Sib.Index_Mask) << (int)Sib.Index_Shift);

		[Pure]
		public static byte GetScaleCode(this Sib sib)
			=> (byte)((uint)(sib & Sib.Scale_Mask) << (int)Sib.Scale_Shift);

		[Pure]
		public static byte GetScaleValue(this Sib sib)
			=> (byte)(1 << GetScaleCode(sib));
	}
}
