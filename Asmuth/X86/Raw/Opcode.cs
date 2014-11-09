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
		// The xex type of the opcode
		XexType_Shift = 0,
		XexType_Legacy = 0 << (int)XexType_Shift,
		XexType_Vex = 1 << (int)XexType_Shift,
		XexType_Xop = 2 << (int)XexType_Shift,
		XexType_EVex = 3 << (int)XexType_Shift,
		XexType_Mask = 3 << (int)XexType_Shift,

		// SIMD Prefix
		SimdPrefix_Shift = 2,
		SimdPrefix_None = 0 << (int)SimdPrefix_Shift,
		SimdPrefix_66 = 1 << (int)SimdPrefix_Shift,
		SimdPrefix_F2 = 2 << (int)SimdPrefix_Shift,
		SimdPrefix_F3 = 3 << (int)SimdPrefix_Shift,
		SimdPrefix_Mask = 3 << (int)SimdPrefix_Shift,

		// Xex flags
		RexW = 1 << 4,
		VexL_Shift = 5,
		VexL_0 = 0 << (int)VexL_Shift,
		VexL_1 = 1 << (int)VexL_Shift,
		VexL_2 = 2 << (int)VexL_Shift,
		VexL_Mask = 3 << (int)VexL_Shift,

		// Opcode map, specified by escape bytes, in VEX, XOP or EVEX
		Map_Shift = 7,
		Map_Default = 0 << (int)Map_Shift,
		Map_0F = 1 << (int)Map_Shift,
		Map_0F38 = 2 << (int)Map_Shift,
		Map_0F3A = 3 << (int)Map_Shift,
		Map_Xop8 = 8 << (int)Map_Shift, // AMD XOP opcode map 8
		Map_Xop9 = 9 << (int)Map_Shift, // AMD XOP opcode map 9
		Map_Xop10 = 10 << (int)Map_Shift, // AMD XOP opcode map 10
		Map_Mask = 0xF << (int)Map_Shift,

		// 0xFF0000: Main opcode byte, always present
		MainByte_Shift = 16,
		MainByte_High4Mask = 0xF0 << (int)MainByte_Shift,
		MainByte_High5Mask = 0xF8 << (int)MainByte_Shift,
		MainByte_RegisterMask = 0x07 << (int)MainByte_Shift,
		MainByte_ConditionCodeMask = 0x0F << (int)MainByte_Shift,
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
		None = 0,
		_66 = 1,
		_F2 = 2,
		_F3 = 3,
	}

	public static class OpcodeEnum
	{
		#region Getters
		[Pure]
		public static SimdPrefix GetSimdPrefix(this Opcode opcode)
			=> (SimdPrefix)Bits.MaskAndShiftRight((uint)opcode, (uint)Opcode.SimdPrefix_Mask, (int)Opcode.SimdPrefix_Shift);

		[Pure]
		public static OpcodeMap GetMap(this Opcode opcode)
		{
			OpcodeMap map = 0;
			switch (opcode & Opcode.XexType_Mask)
			{
				case Opcode.XexType_Legacy: map = OpcodeMap.Type_Legacy; break;
				case Opcode.XexType_Xop: map = OpcodeMap.Type_Xop; break;
				case Opcode.XexType_Vex: map = OpcodeMap.Type_Vex; break;
				case Opcode.XexType_EVex: map = OpcodeMap.Type_Vex; break;
				default: throw new UnreachableException();
			}

			return (OpcodeMap)((int)map | ((int)(opcode & Opcode.Map_Mask) >> (int)Opcode.Map_Shift));
		}

		[Pure]
		public static byte GetMainByte(this Opcode opcode)
			=> (byte)Bits.MaskAndShiftRight((uint)opcode, (uint)Opcode.MainByte_Mask, (int)Opcode.MainByte_Shift);

		[Pure]
		public static byte GetExtraByte(this Opcode opcode)
			=> (byte)Bits.MaskAndShiftRight((uint)opcode, (uint)Opcode.ExtraByte_Mask, (int)Opcode.ExtraByte_Shift);
		#endregion

		#region With***
		[Pure]
		public static Opcode WithSimdPrefix(this Opcode opcode, SimdPrefix simdPrefix)
			=> (opcode & ~Opcode.SimdPrefix_Mask) | (Opcode)((uint)simdPrefix << (int)Opcode.SimdPrefix_Shift);

		[Pure]
		public static Opcode WithMap(this Opcode opcode, Opcode map)
		{
			Contract.Requires((map & ~Opcode.Map_Mask) == 0);
			return (opcode & ~Opcode.Map_Mask) | map;
		}

		[Pure]
		public static Opcode WithMap(this Opcode opcode, OpcodeMap map)
		{
			throw new NotImplementedException();
		}

		[Pure]
		public static Opcode WithMainByte(this Opcode opcode, byte mainByte)
			=> (opcode & ~Opcode.MainByte_Mask) | (Opcode)((uint)mainByte << (int)Opcode.MainByte_Shift);

		[Pure]
		public static Opcode WithExtraByte(this Opcode opcode, byte extraByte)
			=> (opcode & ~Opcode.ExtraByte_Shift) | (Opcode)((uint)extraByte << (int)Opcode.ExtraByte_Shift);  
		#endregion
	}
}
