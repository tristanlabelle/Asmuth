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
		public static char GetLetter(this SegmentRegister reg)
		{
			return "ECSDFG"[(int)reg];
		}
	}
}
