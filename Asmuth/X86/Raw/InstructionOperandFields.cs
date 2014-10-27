using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	/// <summary>
	/// Identifies one or more fields within an instruction which can encode operands.
	/// </summary>
	[Flags]
	public enum InstructionOperandFields : byte
	{
		None = 0,
		EVex_Opmask = 1 << 0,
		Vex_V = 1 << 1,
		Opcode_Low3 = 1 << 2,
		ModRM_Reg = 1 << 3,
		ModRM_RM = 1 << 4,
		Sib_Base = 1 << 5,
		Sib_Index = 1 << 6,
		EVex_Vidx = Sib_Index,
		EVex_IS4 = 1 << 7
	}
}
