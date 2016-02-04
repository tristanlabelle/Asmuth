using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	// CPUID, EAX = 0x00000001
	public enum CpuidFeatureFlags : ulong
	{
		// Low 32 bits, in ECX
		Sse3 = 1 << 0,
		Ssse3 = 1 << 9,
		Sma = 1 << 12,
		CmpXChg16B = 1 << 13,
		Sse4_1 = 1 << 19,
		Sse4_2 = 1 << 20,
		Avx = 1 << 28,
		F16C = 1 << 29,
		NDRand = 1 << 30,

		// High 32 bits, in EDX
		Fpu = 1UL << (32 + 0),
		CMov = 1UL << (32 + 15),
		Mmx = 1UL << (32 + 23),
		Sse = 1UL << (32 + 25),
		Sse2 = 1UL << (32 + 26),
	}

	// CPUID, EAX = 0x80000001
	public enum CpuidExtendedFeatureFlags : ulong
	{
		// ECX,
		StatusFlagAH = 1 << 0,
		AdvancedBitManipulation = 1 << 5,
		Sse4A = 1 << 6,
		PrefetchW = 1 << 8,
		Xop = 1 << 11,
		Fma4 = 1 << 16,
		TimeStampCounter = 1 << 27,
		DataBreakpoint = 1 << 26,

		// EDX
		CmpXChg8B = 1UL << (32 + 8),
		Syscall64 = 1UL << (32 + 11),
		CMov = 1UL << (32 + 15),
		ExecuteDisableBit = 1UL << (32 + 20),
		MmxExt = 1UL << (32 + 22),
		Mmx = 1UL << (32 + 23),
		FXSaveRestore = 1UL << (32 + 24),
		GigabytePages = 1UL << (32 + 26),
		Rdtscp = 1UL << (32 + 27),
		LongMode = 1UL << (32 + 29),
		Amd3DNowExt = 1UL << (32 + 30),
		Amd3DNow = 1UL << (32 + 31)
	}
}
