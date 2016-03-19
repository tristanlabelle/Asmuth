using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	[Flags]
	public enum ModRM : byte
	{
		RM_Shift = 0,
		RM_0 = 0 << RM_Shift,
		RM_1 = 1 << RM_Shift,
		RM_2 = 2 << RM_Shift,
		RM_3 = 3 << RM_Shift,
		RM_4 = 4 << RM_Shift,
		RM_5 = 5 << RM_Shift,
		RM_6 = 6 << RM_Shift,
		RM_7 = 7 << RM_Shift,
		RM_Mask = 7 << RM_Shift,
		RM_Sib = RM_4,

		Reg_Shift = 3,
		Reg_0 = 0 << Reg_Shift,
		Reg_1 = 1 << Reg_Shift,
		Reg_2 = 2 << Reg_Shift,
		Reg_3 = 3 << Reg_Shift,
		Reg_4 = 4 << Reg_Shift,
		Reg_5 = 5 << Reg_Shift,
		Reg_6 = 6 << Reg_Shift,
		Reg_7 = 7 << Reg_Shift,
		Reg_Mask = 7 << Reg_Shift,

		Mod_Shift = 6,
		Mod_Indirect = 0 << Mod_Shift,
		Mod_IndirectDisplacement8 = 1 << Mod_Shift,
		Mod_IndirectLongDisplacement = 2 << Mod_Shift,
		Mod_Direct = 3 << Mod_Shift,
		Mod_Mask = 3 << Mod_Shift,
	}

	public static class ModRMEnum
	{
		[Pure]
		public static string ToDebugString(this ModRM modRM)
			=> $"mod = {GetMod(modRM)}, reg = {GetReg(modRM)}, rm = {GetRM(modRM)}";

		[Pure]
		public static ModRM FromComponents(byte mod, byte reg, byte rm)
		{
			Contract.Requires(mod < 4);
			Contract.Requires(reg < 8);
			Contract.Requires(rm < 8);
			return (ModRM)((mod << 6) | (reg << 3) | rm);
		}

		[Pure]
		public static ModRM FromComponents(byte mod, GprCode reg, GprCode rm)
			=> FromComponents(mod, (byte)reg, (byte)rm);

		[Pure]
		public static byte GetRM(this ModRM modRM)
			=> (byte)((byte)(modRM & ModRM.RM_Mask) >> (byte)ModRM.RM_Shift);

		[Pure]
		public static byte GetReg(this ModRM modRM)
			=> (byte)((byte)(modRM & ModRM.Reg_Mask) >> (byte)ModRM.Reg_Shift);

		[Pure]
		public static GprCode GetRegGpr(this ModRM modRM)
			=> (GprCode)GetReg(modRM);

		[Pure]
		public static byte GetMod(this ModRM modRM)
			=> (byte)((byte)(modRM & ModRM.Mod_Mask) >> (byte)ModRM.Mod_Shift);

		[Pure]
		public static bool IsMemoryRM(this ModRM modRM)
			=> (modRM & ModRM.Mod_Mask) != ModRM.Mod_Direct;

		[Pure]
		public static DisplacementSize GetDisplacementSize(this ModRM modRM, Sib sib, AddressSize addressSize)
		{
			switch (modRM & ModRM.Mod_Mask)
			{
				case ModRM.Mod_IndirectDisplacement8:
					return DisplacementSize._8;
				case ModRM.Mod_IndirectLongDisplacement:
					return addressSize == AddressSize._16 ? DisplacementSize._16 : DisplacementSize._32;
				case ModRM.Mod_Direct: return 0;
			}

			// Mod = 0
			if (addressSize == AddressSize._16)
				return GetRM(modRM) == 6 ? DisplacementSize._16 : DisplacementSize._0;

			if (GetRM(modRM) == 5) return DisplacementSize._32;

			// 32-bit mode, mod = 0, RM = 6 (sib byte)
			return (sib & Sib.Base_Mask) == Sib.Base_Special ? DisplacementSize._32 : DisplacementSize._0;
		}

		[Pure]
		public static bool ImpliesSib(this ModRM modRM, AddressSize addressSize)
			=> addressSize >= AddressSize._32 && GetRM(modRM) == 4 && GetMod(modRM) != 3;
	}
}
