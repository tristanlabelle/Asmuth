using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	public enum InterruptVector
	{
		DivideByZero = 0,
		Debug = 1,
		NonMaskable = 2,
		Breakpoint = 3,
		Overflow = 4,
		BoundRange = 5,
		InvalidOpcode = 6,
		DeviceNotAvailable = 7,
		DoubleFault = 8,
		CoprocessorSegmentOverrun = 9,
		InvalidTss = 10,
		SegmentNotPresent = 11,
		Stack = 12,
		GeneralProtection = 13,
		PageFault = 14,
		FloatingPointExceptionPending = 16,
		AlignmentCheck = 17,
		MachineCheck = 18,
		SimdFloatingPoint = 19,
		SvmSecurity = 30,
	}
}
