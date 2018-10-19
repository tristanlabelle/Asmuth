using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Encoding.Nasm
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
		public static readonly string SizeMatchFirstTwo = "SM2";
		public static readonly string Undocumented = "UNDOC";
		public static readonly string X64 = "X64";

		public static int? TryAsDefaultOperandSizeInBytes(string flag)
		{
			switch (flag)
			{
				case "SB": return 1;
				case "SW": return 2;
				case "SD": return 4;
				case "SQ": return 8;
				case "SO": return 16;
				default: return null;
			}
		}
	}
}
