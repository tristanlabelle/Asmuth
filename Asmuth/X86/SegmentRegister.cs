using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	public enum SegmentRegister : byte
	{
		ES = 0,
		CS = 1,
		SS = 2,
		DS = 3,
		FS = 4,
		GS = 5
	}

	public static class SegmentRegisterEnum
	{
		private static readonly string[] names = { "es", "cs", "ss", "ds", "fs", "gs" };

		public static string GetName(this SegmentRegister reg) => names[(int)reg];
		public static char GetLetter(this SegmentRegister reg) => GetName(reg)[0];
		public static bool IsZeroInLongMode(this SegmentRegister reg) => reg < SegmentRegister.FS;
	}
}
