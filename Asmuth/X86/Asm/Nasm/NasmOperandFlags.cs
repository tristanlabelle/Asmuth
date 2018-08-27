using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Asm.Nasm
{
	[Flags]
	public enum NasmOperandFlags : ushort
	{
		None = 0,

		[NasmName("short")]
		Short = 1 << 0,
		[NasmName("near")]
		NearPointer = 1 << 1,
		[NasmName("far")]
		FarPointer = 1 << 2,
		[NasmName("to")]
		To = 1 << 3,
		[NasmName("mask")]
		MaskingSupported = 1 << 4,
		[NasmName("z")]
		Z = 1 << 5,
		[NasmName("b32")]
		Broadcast32x16 = 1 << 6,
		[NasmName("b64")]
		Broadcast64x8 = 1 << 7,
		[NasmName("er")]
		EmbeddedRounding = 1 << 8,
		[NasmName("sae")]
		SuppressAllExceptions = 1 << 9,

		Relaxed = 1 << 10, // xmmreg*
		Colon = 1 << 11, // imm:imm
	}
}
