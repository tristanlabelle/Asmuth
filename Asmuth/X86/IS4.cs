using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86
{
	/// <summary>
	/// Represents VEX /is4 immediate bytes, which encode some opcode data.
	/// </summary>
    public enum IS4 : byte
    {
		PayloadMask = 0xF,
		SourceRegCodeMask_IA32 = 0x70,
		SourceRegCodeMask_Long = 0xF0
    }

	public static class IS4Enum
	{
		public static byte GetPayload(this IS4 is4) => (byte)(is4 & IS4.PayloadMask);
		public static byte GetSourceRegCode(this IS4 is4, bool longMode = true)
			=> (byte)((byte)(is4 & (longMode ? IS4.SourceRegCodeMask_Long : IS4.SourceRegCodeMask_IA32)) >> 4);
	}
}
