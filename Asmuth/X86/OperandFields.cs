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
		OpcodeReg = 1 << 0, // Low 3 bits of opcode (no ModRM)
		ModReg = OpcodeReg, // ModRM.Reg
		BaseReg = 1 << 1, // ModRM.RM / SIB.Base
		IndexReg = 1 << 2, // SIB.Index
		NonDestructiveReg = 1 << 3, // Vex.vvvv
		VectorOpmask = 1 << 4, // Evex.aaa
		Immediate = 1 << 5, // imm
		Immediate2 = 1 << 6, // imm (ENTER imm, imm)
		Vidx = IndexReg, // Evex.vidx
		IS4 = Immediate, // Evex.is4
	}
}
