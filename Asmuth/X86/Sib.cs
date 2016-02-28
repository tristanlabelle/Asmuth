using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	[Flags]
	public enum Sib : byte
	{
		Base_Shift = 0,
		Base_rAX = 0 << Base_Shift,
		Base_rCX = 1 << Base_Shift,
		Base_rDX = 2 << Base_Shift,
		Base_rBX = 3 << Base_Shift,
		Base_rSP = 4 << Base_Shift,
		Base_Special = 5 << Base_Shift, // Either rBP or zero
		Base_rSI = 6 << Base_Shift,
		Base_rDI = 7 << Base_Shift,
		Base_Mask = 7 << Base_Shift,

		Index_Shift = 3,
		Index_rAX = 0 << Index_Shift,
		Index_rCX = 1 << Index_Shift,
		Index_rDX = 2 << Index_Shift,
		Index_rBX = 3 << Index_Shift,
		Index_rSP = 4 << Index_Shift,
		Index_Zero = 5 << Index_Shift,
		Index_rSI = 6 << Index_Shift,
		Index_rDI = 7 << Index_Shift,
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
		public static string ToDebugString(this Sib sib)
			=> $"ss = {GetSS(sib)}, index = {GetIndex(sib)}, base = {GetBase(sib)}";

		[Pure]
		public static byte GetBase(this Sib sib)
			=> (byte)((uint)(sib & Sib.Base_Mask) << (int)Sib.Base_Shift);

		[Pure]
		public static GprCode? GetBaseReg(this Sib sib, ModRM modRM)
		{
			if ((sib & Sib.Base_Mask) == Sib.Base_Special && modRM.GetMod() == 0)
				return null;
			return (GprCode)GetBase(sib);
		}

		[Pure]
		public static byte GetIndex(this Sib sib)
			=> (byte)((uint)(sib & Sib.Index_Mask) << (int)Sib.Index_Shift);

		[Pure]
		public static GprCode? GetIndexReg(this Sib sib)
		{
			if ((sib & Sib.Index_Mask) == Sib.Index_Zero) return null;
			return (GprCode)GetIndex(sib);
		}

		[Pure]
		public static byte GetSS(this Sib sib)
			=> (byte)((uint)(sib & Sib.Scale_Mask) << (int)Sib.Scale_Shift);

		[Pure]
		public static byte GetScale(this Sib sib)
			=> (byte)(1 << GetSS(sib));
	}
}
