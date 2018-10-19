using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	/// <summary>
	/// Identifies one of the "legacy prefixes", bytes that optionally appear before
	/// general-purpose instructions.
	/// </summary>
	public enum LegacyPrefix : byte
	{
		Lock,

		RepeatNotEqual,
		RepeatEqual,

		CSSegmentOverride,
		SSSegmentOverride,
		DSSegmentOverride,
		ESSegmentOverride,
		FSSegmentOverride,
		GSSegmentOverride,

		OperandSizeOverride,

		AddressSizeOverride,

		BranchNotTakenHint = CSSegmentOverride,
		BranchTakenHint = DSSegmentOverride,
		Repeat = RepeatEqual,
		RepeatNonZero = RepeatNotEqual,
		RepeatZero = RepeatEqual,
	}

	public enum LegacyPrefixGroup : byte
	{
		Repeat,
		SegmentOverride,
		OperandSizeOverride,
		AddressSizeOverride,
		Lock, // Intel actually considers this part of the repeat group
	}

	public static class LegacyPrefixEnum
	{
		public static LegacyPrefix GetSegmentOverride(SegmentRegister segment)
		{
			switch (segment)
			{
				case SegmentRegister.CS: return LegacyPrefix.CSSegmentOverride;
				case SegmentRegister.DS: return LegacyPrefix.DSSegmentOverride;
				case SegmentRegister.ES: return LegacyPrefix.ESSegmentOverride;
				case SegmentRegister.FS: return LegacyPrefix.FSSegmentOverride;
				case SegmentRegister.GS: return LegacyPrefix.GSSegmentOverride;
				case SegmentRegister.SS: return LegacyPrefix.SSSegmentOverride;
				default: throw new ArgumentOutOfRangeException(nameof(segment));
			}
		}
		public static LegacyPrefix? TryFromEncodingByte(byte value)
		{
			switch (value)
			{
				case 0xF0: return LegacyPrefix.Lock;
				case 0xF2: return LegacyPrefix.RepeatNotEqual;
				case 0xF3: return LegacyPrefix.RepeatEqual;
				case 0x2E: return LegacyPrefix.CSSegmentOverride;
				case 0x36: return LegacyPrefix.SSSegmentOverride;
				case 0x3E: return LegacyPrefix.DSSegmentOverride;
				case 0x26: return LegacyPrefix.ESSegmentOverride;
				case 0x64: return LegacyPrefix.FSSegmentOverride;
				case 0x65: return LegacyPrefix.GSSegmentOverride;
				case 0x66: return LegacyPrefix.OperandSizeOverride;
				case 0x67: return LegacyPrefix.AddressSizeOverride;
				default: return null;
			}
		}
		public static string GetMnemonic(this LegacyPrefix prefix)
		{
			switch (prefix)
			{
				case LegacyPrefix.Lock: return "lock";
				case LegacyPrefix.RepeatNotEqual: return "repne";
				case LegacyPrefix.RepeatEqual: return "repe";
				case LegacyPrefix.CSSegmentOverride: return "cs";
				case LegacyPrefix.DSSegmentOverride: return "ds";
				case LegacyPrefix.ESSegmentOverride: return "es";
				case LegacyPrefix.FSSegmentOverride: return "fs";
				case LegacyPrefix.GSSegmentOverride: return "gs";
				case LegacyPrefix.SSSegmentOverride: return "ss";
				case LegacyPrefix.OperandSizeOverride: return "oso";
				case LegacyPrefix.AddressSizeOverride: return "aso";
				default: throw new ArgumentOutOfRangeException(nameof(prefix));
			}
		}

		public static byte GetEncodingByte(this LegacyPrefix prefix)
		{
			switch (prefix)
			{
				case LegacyPrefix.Lock: return 0xF0;
				case LegacyPrefix.RepeatNotEqual: return 0xF2;
				case LegacyPrefix.RepeatEqual: return 0xF3;
				case LegacyPrefix.CSSegmentOverride: return 0x2E;
				case LegacyPrefix.SSSegmentOverride: return 0x36;
				case LegacyPrefix.DSSegmentOverride: return 0x3E;
				case LegacyPrefix.ESSegmentOverride: return 0x26;
				case LegacyPrefix.FSSegmentOverride: return 0x64;
				case LegacyPrefix.GSSegmentOverride: return 0x65;
				case LegacyPrefix.OperandSizeOverride: return 0x66;
				case LegacyPrefix.AddressSizeOverride: return 0x67;
				default: throw new ArgumentOutOfRangeException(nameof(prefix));
			}
		}

		public static LegacyPrefixGroup GetGroup(this LegacyPrefix prefix)
		{
			switch (prefix)
			{
				case LegacyPrefix.AddressSizeOverride: return LegacyPrefixGroup.AddressSizeOverride;
				case LegacyPrefix.OperandSizeOverride: return LegacyPrefixGroup.OperandSizeOverride;

				case LegacyPrefix.CSSegmentOverride:
				case LegacyPrefix.DSSegmentOverride:
				case LegacyPrefix.ESSegmentOverride:
				case LegacyPrefix.FSSegmentOverride:
				case LegacyPrefix.GSSegmentOverride:
				case LegacyPrefix.SSSegmentOverride:
					return LegacyPrefixGroup.SegmentOverride;

				case LegacyPrefix.Lock: return LegacyPrefixGroup.Lock;

				case LegacyPrefix.RepeatNotEqual:
				case LegacyPrefix.RepeatEqual:
					return LegacyPrefixGroup.Repeat;

				default: throw new ArgumentOutOfRangeException(nameof(prefix));
			}
		}
	}
}
