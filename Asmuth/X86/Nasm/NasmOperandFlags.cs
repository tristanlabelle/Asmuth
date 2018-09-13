using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Nasm
{
	[Flags]
	public enum NasmOperandFlags : ushort
	{
		None = 0,

		[NasmEnumName("short")]
		Short = 1 << 0,
		[NasmEnumName("near")]
		NearPointer = 1 << 1,
		[NasmEnumName("far")]
		FarPointer = 1 << 2,
		[NasmEnumName("to")]
		To = 1 << 3,
		[NasmEnumName("mask")]
		MaskingSupported = 1 << 4,
		[NasmEnumName("z")]
		Z = 1 << 5,
		[NasmEnumName("b32")]
		Broadcast32x16 = 1 << 6,
		[NasmEnumName("b64")]
		Broadcast64x8 = 1 << 7,
		[NasmEnumName("er")]
		EmbeddedRounding = 1 << 8,
		[NasmEnumName("sae")]
		SuppressAllExceptions = 1 << 9,

		Relaxed = 1 << 10, // xmmreg*
		Colon = 1 << 11, // imm:imm
	}
}
