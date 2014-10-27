using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	[Flags]
	public enum ModRM : byte
	{
		RM_Shift = 0,
        RM_Mask = 7 << RM_Shift,

		Reg_Shift = 3,
		Reg_Mask = 7 << Reg_Shift,

		Mod_Shift = 6,
		Mod_Mask = 3 << Mod_Shift,
	}

	public static class ModRMEnum
	{
		[Pure]
		public static ModRM FromFields(byte mod, byte reg, byte rm)
		{
			Contract.Requires(mod < 4);
			Contract.Requires(reg < 8);
			Contract.Requires(rm < 8);
			return (ModRM)((mod << 6) | (reg << 3) | rm);
		}

		[Pure]
		public static byte GetRMField(this ModRM modRM)
			=> (byte)((byte)(modRM & ModRM.RM_Mask) >> (byte)ModRM.RM_Shift);

		[Pure]
		public static byte GetRegField(this ModRM modRM)
			=> (byte)((byte)(modRM & ModRM.Reg_Mask) >> (byte)ModRM.Reg_Shift);

		[Pure]
		public static byte GetModField(this ModRM modRM)
			=> (byte)((byte)(modRM & ModRM.Mod_Mask) >> (byte)ModRM.Mod_Shift);

		[Pure]
		public static int GetDisplacementSizeInBytes(this ModRM modRM, bool addressing32)
		{
			switch (GetModField(modRM))
			{
				case 0:
				{
					if (addressing32) return GetRMField(modRM) == 5 ? 4 : 0;
					else return GetRMField(modRM) == 6 ? 2 : 0;
				}
				case 1: return 1;
				case 2: return addressing32 ? 4 : 2;
				case 3: return 0;
				default: throw new UnreachableException("Unexpected ModRM mod field value.");
			}
		}

		[Pure]
		public static bool ImpliesSib(this ModRM modRM, bool addressing32 = true)
			=> addressing32 && GetRMField(modRM) == 4 && GetModField(modRM) != 3;
	}
}
