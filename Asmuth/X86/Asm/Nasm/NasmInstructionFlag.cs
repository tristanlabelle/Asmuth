using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Asm.Nasm
{
	public enum NasmInstructionFlag : byte
	{
		SM = 0, // Size match
		SM2 = 1, // Size match first two operands
		SB = 2, // Unsized operands can't be non-byte
		SW = 3, // Unsized operands can't be non-word
		SD = 4, // Unsized operands can't be non-dword
		SQ = 5, // Unsized operands can't be non-qword
		SO = 6, // Unsized operands can't be non-oword
		SY = 7, // Unsized operands can't be non-yword
		SZ = 8, // Unsized operands can't be non-zword
		SIZE = 9, // Unsized operands must match the bitsize
		SX = 10, // Unsized operands not allowed
		AR0 = 11, // SB, SW, SD applies to argument 0
		AR1 = 12, // SB, SW, SD applies to argument 1
		AR2 = 13, // SB, SW, SD applies to argument 2
		AR3 = 14, // SB, SW, SD applies to argument 3
		AR4 = 15, // SB, SW, SD applies to argument 4
		OPT = 16, // Optimizing assembly only

		PRIV = 32, // Privileged instruction
		SMM = 33, // Only valid in SMM
		PROT = 34, // Protected mode only
		LOCK = 35, // Lockable if operand 0 is memory
		NOLONG = 36, // Not available in long mode
		LONG = 37, // Long mode
		NOHLE = 38, // HLE prefixes forbidden
		MIB = 39, // disassemble with split EA
		BND = 40, // BND (0xF2) prefix available
		UNDOC = 41, // Undocumented
		HLE = 42, // HLE prefixed

		FPU = 43, // FPU
		MMX = 44, // MMX
		_3DNOW = 45, // 3DNow!
		SSE = 46, // SSE (KNI, MMX2)
		SSE2 = 47, // SSE2
		SSE3 = 48, // SSE3 (PNI)
		VMX = 49, // VMX
		SSSE3 = 50, // SSSE3
		SSE4A = 51, // AMD SSE4a
		SSE41 = 52, // SSE4.1
		SSE42 = 53, // SSE4.2
		SSE5 = 54, // SSE5
		AVX = 55, // AVX (128b)
		AVX2 = 56, // AVX2 (256b)
		FMA = 57,
		BMI1 = 58,
		BMI2 = 59,
		TBM = 60,
		RTM = 61,
		INVPCID = 62,
		AVX512 = 64, // AVX-512F (512b)
		AVX512CD = 65, // AVX-512 Conflict Detection
		AVX512ER = 66, // AVX-512 Exponential and Reciprocal
		AVX512PF = 67, // AVX-512 Prefetch
		MPX = 68, // MPX
		SHA = 69, // SHA
		PREFETCHWT1 = 70, // PREFETCHWT1

		VEX = 94, // VEX or XOP encoded instruction
		EVEX = 95, // EVEX encoded instruction

		_8086 = 96, // 8086
		_186 = 97, // 186+
		_286 = 98, // 286+
		_386 = 99, // 386+
		_486 = 100, // 486+
		PENT = 101, // Pentium
		P6 = 102, // P6
		KATMAI = 103, // Katmai
		WILLAMETTE = 104, // Willamette
		PRESCOTT = 105, // Prescott
		X86_64 = 106, // x86-64 (long or legacy mode)
		X64 = X86_64,
		NEHALEM = 107, // Nehalem
		WESTMERE = 108, // Westmere
		SANDYBRIDGE = 109, // Sandy Bridge
		FUTURE = 110, // Future processor (not yet disclosed)
		IA64 = 111, // IA64 (in x86 mode)
		CYRIX = 126, // Cyrix-specific
		AMD = 127, // AMD-specific

		ND = 128, // No disassembly, ignored by ndisasm
	}
}
