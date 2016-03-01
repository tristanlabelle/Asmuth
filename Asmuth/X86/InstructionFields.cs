﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	[Flags]
	public enum InstructionFields : ushort
	{
		None = 0,
		LegacyLock = 1 << 0,
		LegacyRepeat = 1 << 1, // Note: intel puts lock and repeat together
		LegacySegmentOverride = 1 << 2,
		LegacyBranchHint = LegacySegmentOverride,
		LegacyOperandSizeOverride = 1 << 3,
		LegacyAddressSizeOverride = 1 << 4,
		Xex = 1 << 5,
		Opcode = 1 << 6,
		ModRM = 1 << 7,
		Sib = 1 << 8,
		Displacement = 1 << 9,
		Immediate1 = 1 << 10,
		Immediate2 = 1 << 11 // C8 iw ib = ENTER imm16, imm8
	}

	public static class InstructionFieldsEnum
	{
		public static bool Has(this InstructionFields fields, InstructionFields mask)
			=> (fields & mask) == mask;
	}
}
