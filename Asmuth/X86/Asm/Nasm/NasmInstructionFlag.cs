using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Asm.Nasm
{
	public static class NasmInstructionFlags
	{
		public static readonly string AssemblerOnly = "ND";
		public static readonly string LockCompatible = "LOCK";
		public static readonly string NoLongMode = "NOLONG";
		public static readonly string Obsolete = "OBSOLETE";
		public static readonly string Future = "FUTURE";
		public static readonly string SizeMatch = "SM";
		public static readonly string Undocumented = "UNDOC";
		public static readonly string X64 = "X64";
	}
}
