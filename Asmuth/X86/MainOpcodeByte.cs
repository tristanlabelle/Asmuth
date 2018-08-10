using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86
{
	public static class MainOpcodeByte
	{
		public const byte EmbeddedRegMask = 0x7;
		public const byte EmbeddedConditionCodeMask = 0xF;

		public static byte GetEmbeddedReg(byte value) => (byte)(value & EmbeddedRegMask);
		public static GprCode GetEmbeddedGprCode(byte value) => (GprCode)(value & EmbeddedRegMask);
		public static ConditionCode GetEmbeddedConditionCode(byte value)
			=> (ConditionCode)(value & EmbeddedConditionCodeMask);
	}
}
