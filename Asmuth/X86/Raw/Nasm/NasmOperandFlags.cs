using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw.Nasm
{
	[Flags]
	public enum NasmOperandFlags : ushort
	{
		None = 0,
		Short = 1 << 0,
		Near = 1 << 1,
		Far = 1 << 2,
		To = 1 << 3,
		Mask = 1 << 4,
		Z = 1 << 5,
		B32 = 1 << 6,
		B64 = 1 << 7,
		Er = 1 << 8,
		Sae = 1 << 9
	}
}
