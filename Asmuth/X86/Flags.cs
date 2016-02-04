using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	[Flags]
	public enum Flags : uint
	{
		None = 0,

		Carry = 1 << 0,
		Parity = 1 << 2,
		Adjust = 1 << 4,
		Zero = 1 << 6,
		Sign = 1 << 7,
		Trap = 1 << 8,
		InterruptDisable = 1 << 9,
		Direction = 1 << 10,
		Overflow = 1 << 11,
		NestedTask = 1 << 14,
		Resume = 1 << 16,
		Virtual8086 = 1 << 17,
		AlignmentCheck = 1 << 18,
		VirtualInterrupt = 1 << 19,
		VirtualInterruptPending = 1 << 20,
		CpuidAvailable = 1 << 21,
	}
}
