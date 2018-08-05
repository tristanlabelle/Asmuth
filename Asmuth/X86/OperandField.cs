using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	/// <summary>
	/// Identifies a fields within an instruction which can encode operands.
	/// </summary>
	public enum OperandField : byte
	{
		ModReg, // 80+r or ModRM.Reg
		BaseReg, // ModRM.RM / SIB.Base
		IndexReg, // SIB.Index
		NonDestructiveReg, // Vex.vvvv
		VectorOpmask, // Evex.aaa
		Immediate, // imm
		Immediate2, // imm (ENTER imm, imm)

		OpcodeReg = ModReg, // Low 3 bits of opcode (no ModRM)
		Vidx = IndexReg, // Evex.vidx
		IS4 = Immediate, // Evex.is4
	}
}
