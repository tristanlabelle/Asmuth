using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	[Flags]
	public enum Mode64OperandSizePolicy : byte
	{
		// AMD64 APM Vol. 3, B.1 General Rules for 64-Bit Mode
		Default32_Override64,
		Fixed64,
		Default64_Override16,
		SameAsCompatibility,
	}
}
