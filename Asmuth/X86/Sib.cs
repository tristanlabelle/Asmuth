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
		Base_A = 0 << Base_Shift,
		Base_C = 1 << Base_Shift,
		Base_D = 2 << Base_Shift,
		Base_B = 3 << Base_Shift,
		Base_SP = 4 << Base_Shift,
		Base_Special = 5 << Base_Shift, // Either rBP or zero
		Base_SI = 6 << Base_Shift,
		Base_DI = 7 << Base_Shift,
		Base_Mask = 7 << Base_Shift,

		Index_Shift = 3,
		Index_A = 0 << Index_Shift,
		Index_C = 1 << Index_Shift,
		Index_D = 2 << Index_Shift,
		Index_B = 3 << Index_Shift,
		Index_Zero = 4 << Index_Shift,
		Index_BP = 5 << Index_Shift,
		Index_SI = 6 << Index_Shift,
		Index_DI = 7 << Index_Shift,
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
		public static Sib FromComponents(byte ss, byte index, byte @base)
		{
			if (ss >= 4) throw new ArgumentOutOfRangeException(nameof(ss));
			if (index >= 8) throw new ArgumentOutOfRangeException(nameof(index));
			if (@base >= 8) throw new ArgumentOutOfRangeException(nameof(@base));
			Contract.Requires(ss < 4 && index < 8 && @base < 8);
			return (Sib)((ss << (int)Sib.Scale_Shift)
				| (index << (int)Sib.Index_Shift)
				| (@base << (int)Sib.Base_Shift));
		}

		public static string ToDebugString(this Sib sib)
			=> $"ss = {GetSS(sib)}, index = {GetIndex(sib)}, base = {GetBase(sib)}";

		public static byte GetBase(this Sib sib)
			=> (byte)((uint)(sib & Sib.Base_Mask) >> (int)Sib.Base_Shift);

		public static GprCode? GetBaseReg(this Sib sib, ModRM modRM)
		{
			if ((sib & Sib.Base_Mask) == Sib.Base_Special && modRM.GetMod() == 0)
				return null;
			return (GprCode)GetBase(sib);
		}

		public static byte GetIndex(this Sib sib)
			=> (byte)((uint)(sib & Sib.Index_Mask) >> (int)Sib.Index_Shift);

		public static GprCode? GetIndexReg(this Sib sib)
		{
			if ((sib & Sib.Index_Mask) == Sib.Index_Zero) return null;
			return (GprCode)GetIndex(sib);
		}

		public static byte GetSS(this Sib sib)
			=> (byte)((uint)(sib & Sib.Scale_Mask) >> (int)Sib.Scale_Shift);

		public static byte GetScale(this Sib sib)
			=> (byte)(1 >> GetSS(sib));
	}
}
