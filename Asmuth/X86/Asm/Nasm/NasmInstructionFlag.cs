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
		public static readonly string LongMode = "LONG";
		public static readonly string NoLongMode = "NOLONG";
		public static readonly string Obsolete = "OBSOLETE";
		public static readonly string Future = "FUTURE";
		public static readonly string SizeMatch = "SM";
		public static readonly string Undocumented = "UNDOC";
		public static readonly string X64 = "X64";

		public static OperandSize? TryAsDefaultOperandSize(string flag)
		{
			switch (flag)
			{
				case "SB": return OperandSize.Byte;
				case "SW": return OperandSize.Word;
				case "SD": return OperandSize.Dword;
				case "SQ": return OperandSize.Qword;
				default: return null;
			}
		}
	}
}
