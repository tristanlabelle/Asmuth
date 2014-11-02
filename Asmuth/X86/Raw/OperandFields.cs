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
	public enum OperandFields : byte
	{
		None = 0,
		Opcode_Low3 = 1 << 0,
		ModRM_Reg = Opcode_Low3,
		ModRM_RM = 1 << 1,
		Sib_Base = 1 << 2,
		Sib_Index = 1 << 3,
		Immediate = 1 << 4,
		Vex_V = 1 << 5,
		EVex_Vidx = Sib_Index,
		EVex_Opmask = 1 << 6,
		EVex_IS4 = Immediate,
		SecondImmediate = 1 << 7
	}
}
