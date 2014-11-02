using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
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
		Fpu = 1 << (32 + 0),
		CMov = 1 << (32 + 15),
		Mmx = 1 << (32 + 23),
		Sse = 1 << (32 + 25),
		Sse2 = 1 << (32 + 26),
	}
}
