using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	[Flags]
	public enum ModRM : byte
	{
		RM_Shift = 0,
		RM_0 = 0 << RM_Shift, RM_GprA = RM_0,
		RM_1 = 1 << RM_Shift, RM_GprC = RM_1,
		RM_2 = 2 << RM_Shift, RM_GprD = RM_2,
		RM_3 = 3 << RM_Shift, RM_GprB = RM_3,
		RM_4 = 4 << RM_Shift, RM_Sib = RM_4,
		RM_5 = 5 << RM_Shift, RM_GprBP = RM_5,
		RM_6 = 6 << RM_Shift, RM_GprSI = RM_6,
		RM_7 = 7 << RM_Shift, RM_GprDI = RM_7,
		RM_Mask = 7 << RM_Shift,

		Reg_Shift = 3,
		Reg_0 = 0 << Reg_Shift, Reg_GprA = Reg_0,
		Reg_1 = 1 << Reg_Shift, Reg_GprC = Reg_1,
		Reg_2 = 2 << Reg_Shift, Reg_GprD = Reg_2,
		Reg_3 = 3 << Reg_Shift, Reg_GprB = Reg_3,
		Reg_4 = 4 << Reg_Shift, Reg_GprSP = Reg_4,
		Reg_5 = 5 << Reg_Shift, Reg_GprBP = Reg_5,
		Reg_6 = 6 << Reg_Shift, Reg_GprSI = Reg_6,
		Reg_7 = 7 << Reg_Shift, Reg_GprDI = Reg_7,
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
		public static string ToDebugString(this ModRM modRM)
			=> $"mod = {GetMod(modRM)}, reg = {GetReg(modRM)}, rm = {GetRM(modRM)}";

		public static ModRM FromReg(byte reg)
		{
			if (reg >= 8) throw new ArgumentOutOfRangeException(nameof(reg));
			return (ModRM)(reg << 3);
		}

		public static ModRM FromComponents(byte mod, byte reg, byte rm)
		{
			if (mod >= 4) throw new ArgumentOutOfRangeException(nameof(mod));
			if (reg >= 8) throw new ArgumentOutOfRangeException(nameof(reg));
			if (rm >= 8) throw new ArgumentOutOfRangeException(nameof(rm));
			return (ModRM)((mod << 6) | (reg << 3) | rm);
		}

		public static ModRM FromComponents(byte mod, GprCode reg, GprCode rm)
			=> FromComponents(mod, (byte)reg, (byte)rm);

		public static byte GetRM(this ModRM modRM)
			=> (byte)((byte)(modRM & ModRM.RM_Mask) >> (byte)ModRM.RM_Shift);

		public static byte GetReg(this ModRM modRM)
			=> (byte)((byte)(modRM & ModRM.Reg_Mask) >> (byte)ModRM.Reg_Shift);

		public static GprCode GetRegGpr(this ModRM modRM)
			=> (GprCode)GetReg(modRM);

		public static byte GetMod(this ModRM modRM)
			=> (byte)((byte)(modRM & ModRM.Mod_Mask) >> (byte)ModRM.Mod_Shift);

		public static bool IsDirectRM(this ModRM modRM)
			=> (modRM & ModRM.Mod_Mask) == ModRM.Mod_Direct;

		public static bool IsMemoryRM(this ModRM modRM)
			=> (modRM & ModRM.Mod_Mask) != ModRM.Mod_Direct;

		public static DisplacementSize GetDisplacementSize(this ModRM modRM, Sib sib, AddressSize addressSize)
		{
			switch (modRM & ModRM.Mod_Mask)
			{
				case ModRM.Mod_IndirectDisplacement8:
					return DisplacementSize._8Bits;
				case ModRM.Mod_IndirectLongDisplacement:
					return addressSize == AddressSize._16 ? DisplacementSize._16Bits : DisplacementSize._32Bits;
				case ModRM.Mod_Direct: return 0;
			}

			// Mod = 0
			if (addressSize == AddressSize._16)
				return GetRM(modRM) == 6 ? DisplacementSize._16Bits : DisplacementSize.None;

			if (GetRM(modRM) == 5) return DisplacementSize._32Bits;

			// 32-bit mode, mod = 0, RM = 6 (sib byte)
			return (sib & Sib.Base_Mask) == Sib.Base_Special ? DisplacementSize._32Bits : DisplacementSize.None;
		}

		public static bool ImpliesSib(this ModRM modRM, AddressSize addressSize)
			=> addressSize >= AddressSize._32 && GetRM(modRM) == 4 && GetMod(modRM) != 3;
	}
}
