using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	/// <summary>
	/// Represents the main opcode byte and all other data necessary
	/// to identify the operation to be performed (short of specific operands).
	/// </summary>
	[Flags]
	public enum Opcode : uint
	{
		// 0b0000 0011: SIMD prefix, matches the SimdPrefix enum
		SimdPrefix_Shift = 0,
		SimdPrefix_None = (uint)SimdPrefix.None << (int)SimdPrefix_Shift,
		SimdPrefix_66 = (uint)SimdPrefix._66 << (int)SimdPrefix_Shift,
		SimdPrefix_F2 = (uint)SimdPrefix._F2 << (int)SimdPrefix_Shift,
		SimdPrefix_F3 = (uint)SimdPrefix._F3 << (int)SimdPrefix_Shift,
		SimdPrefix_Mask = 3 << (int)SimdPrefix_Shift,

		// Xex flags, specified in VEX, XOP or EVEX
		RexW = 1 << 2,
		Vex = 1 << 3, // VEX will change some instructions to use 3 operands
		VexL = 1 << 4,
		EVex = 1 << 5, // TODO: Check if EVEX ever disambiguates instructions
		EVexL2 = 1 << 6,

		// Opcode map, specified by escape bytes, in VEX, XOP or EVEX
		Map_Shift = 7,
		Map_OneByte = 0 << (int)Map_Shift,
		Map_0F = 1 << (int)Map_Shift,
		Map_0F38 = 2 << (int)Map_Shift,
		Map_0F3A = 3 << (int)Map_Shift,
		Map_Xop8 = 8 << (int)Map_Shift, // AMD XOP opcode map 8
		Map_Xop9 = 9 << (int)Map_Shift, // AMD XOP opcode map 9
		Map_Xop10 = 10 << (int)Map_Shift, // AMD XOP opcode map 10
		Map_Mask = 0xF << (int)Map_Shift,

		// 0xFF0000: Main opcode byte, always present
		MainByte_Shift = 16,
		MainByte_Low3Mask = 0x07 << (int)MainByte_Shift,
		MainByte_High5Mask = 0xF8 << (int)MainByte_Shift,
		MainByte_Mask = 0xFF << (int)MainByte_Shift,

		// 0xFF000000: Extra byte (ModReg, ModRM, EVEX.IS4 or 3DNow! Imm8)
		ExtraByte_Shift = 24,
		ExtraByte_Mask = 0xFFU << (int)ExtraByte_Shift,

		ModRM_Shift = ExtraByte_Shift,
		ModRM_RegShift = ModRM_Shift + 3,
		ModRM_RegMask = 7 << (int)ModRM_RegShift,
		ModRM_Mask = ExtraByte_Mask,

		EVexIs4_Shift = ExtraByte_Shift,
		EVexIs4_Mask = ExtraByte_Mask,

		_3DNow_Shift = ExtraByte_Shift,
		_3DNow_Mask = ExtraByte_Mask,
	}

	public enum SimdPrefix : byte
	{
		None,
		_66,
		_F2,
		_F3,
	}

	public static class OpcodeEnum
	{
		[Pure]
		public static SimdPrefix GetSimdPrefix(this Opcode opcode)
			=> (SimdPrefix)Bits.MaskAndShiftRight((uint)opcode, (uint)Opcode.SimdPrefix_Mask, (int)Opcode.SimdPrefix_Shift);

		[Pure]
		public static OpcodeMap GetMap(this Opcode opcode)
		{
			throw new NotImplementedException();
		}

		[Pure]
		public static byte GetMainByte(this Opcode opcode)
			=> (byte)Bits.MaskAndShiftRight((uint)opcode, (uint)Opcode.MainByte_Mask, (int)Opcode.MainByte_Shift);

		[Pure]
		public static ModRM GetModRM(this Opcode opcode)
			=> (ModRM)Bits.MaskAndShiftRight((uint)opcode, (uint)Opcode.ModRM_Mask, (int)Opcode.ModRM_Shift);

		[Pure]
		public static Opcode WithSimdPrefix(this Opcode opcode, SimdPrefix simdPrefix)
			=> (opcode & ~Opcode.SimdPrefix_Mask) | (Opcode)((uint)simdPrefix << (int)Opcode.SimdPrefix_Shift);

		[Pure]
		public static Opcode WithMap(this Opcode opcode, OpcodeMap map)
		{
			throw new NotImplementedException();
		}

		[Pure]
		public static Opcode WithMainByte(this Opcode opcode, byte mainByte)
			=> (opcode & ~Opcode.MainByte_Mask) | (Opcode)((uint)mainByte << (int)Opcode.MainByte_Shift);

		[Pure]
		public static Opcode WithModRM(this Opcode opcode, ModRM modRM)
			=> (opcode & ~Opcode.ModRM_Mask) | (Opcode)((uint)modRM << (int)Opcode.ModRM_Shift);
	}
}
