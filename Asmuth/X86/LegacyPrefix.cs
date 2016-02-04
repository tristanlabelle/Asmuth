using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	public enum LegacyPrefix : byte
	{
		Lock = 0xF0,

		RepeatNonZero = 0xF2,
		RepeatZero = 0xF3,
		RepeatNotEqual = RepeatNonZero,
		RepeatEqual = RepeatZero,

		CSSegmentOverride = 0x2E,
		SSSegmentOverride = 0x36,
		DSSegmentOverride = 0x3E,
		ESSegmentOverride = 0x26,
		FSSegmentOverride = 0x64,
		GSSegmentOverride = 0x65,
		BranchNotTakenHint = CSSegmentOverride,
		BranchTakenHint = DSSegmentOverride,

		OperandSizeOverride = 0x66,

		AddressSizeOverride = 0x67
	}

	public static class LegacyPrefixEnum
	{
		[Pure]
		public static bool IsSimdPrefix(this LegacyPrefix prefix)
			=> prefix == LegacyPrefix.OperandSizeOverride
			|| prefix == LegacyPrefix.RepeatNonZero
			|| prefix == LegacyPrefix.RepeatEqual;

		[Pure]
		public static InstructionFields GetFieldOrNone(this LegacyPrefix prefix)
		{
			switch (prefix)
			{
				case LegacyPrefix.Lock:
					return InstructionFields.LegacyLock;

				case LegacyPrefix.RepeatNonZero:
				case LegacyPrefix.RepeatZero:
					return InstructionFields.LegacyRepeat;

				case LegacyPrefix.CSSegmentOverride:
				case LegacyPrefix.SSSegmentOverride:
				case LegacyPrefix.DSSegmentOverride:
				case LegacyPrefix.ESSegmentOverride:
				case LegacyPrefix.FSSegmentOverride:
				case LegacyPrefix.GSSegmentOverride:
					return InstructionFields.LegacySegmentOverride;

				case LegacyPrefix.OperandSizeOverride:
					return InstructionFields.LegacyOperandSizeOverride;

				case LegacyPrefix.AddressSizeOverride:
					return InstructionFields.LegacyAddressSizeOverride;

				default:
					return InstructionFields.None;
			}
		}
	}
}
