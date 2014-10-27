using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	public enum OperandFields : byte
	{
		None = 0,
		MainOpcodeLowBits = 1 << 0,
		ModReg = 1 << 1,
		ModRM = 1 << 2,
		SibBase = 1 << 3,
		SibIndex = 1 << 4,
		Immediate = 1 << 5,
		SecondaryImmediate = 1 << 6,
		VexIS4 = Immediate,
		VexNds = 1 << 7,
	}
}
