using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86
{
    partial class OpcodeEncodingTable<TTag>
	{
		public enum EncodingLookupKey : ushort
		{
			XexType_Shift = 0,
			XexType_Escapes_MaybeRex = 0 << (int)XexType_Shift,
			XexType_Vex = 1 << (int)XexType_Shift,
			XexType_Xop = 2 << (int)XexType_Shift,
			XexType_EVex = 3 << (int)XexType_Shift,

			SimdPrefix_Shift = XexType_Shift + 2,
			SimdPrefix_Unknown = 0 << (int)SimdPrefix_Shift, // If XEX is not vector
			SimdPrefix_None = 0 << (int)SimdPrefix_Shift,
			SimdPrefix_66 = 1 << (int)SimdPrefix_Shift,
			SimdPrefix_F2 = 2 << (int)SimdPrefix_Shift,
			SimdPrefix_F3 = 3 << (int)SimdPrefix_Shift,

			OpcodeMap_Shift = SimdPrefix_Shift + 2,
			OpcodeMap_Default = 0 << (int)OpcodeMap_Shift,
			OpcodeMap_Escape0F = 1 << (int)OpcodeMap_Shift,
			OpcodeMap_Escape0F38 = 2 << (int)OpcodeMap_Shift,
			OpcodeMap_Escape0F3A = 3 << (int)OpcodeMap_Shift,
			OpcodeMap_Xop8 = 8 << (int)OpcodeMap_Shift,
			OpcodeMap_Xop9 = 9 << (int)OpcodeMap_Shift,
			OpcodeMap_Xop10 = 10 << (int)OpcodeMap_Shift,

			OpcodeHigh5Bits_Shift = OpcodeMap_Shift + 4
		}


		private static EncodingLookupKey GetEncodingLookupKey(in Instruction instruction)
			=> GetEncodingLookupKey(instruction.LegacyPrefixes, instruction.Xex, instruction.MainByte);

		private static EncodingLookupKey GetEncodingLookupKey(
			ImmutableLegacyPrefixList legacyPrefixes, Xex xex, byte opcode)
			=> GetEncodingLookupKey(xex.Type, xex.SimdPrefix ?? legacyPrefixes.PotentialSimdPrefix,
				xex.OpcodeMap, opcode);

		public static EncodingLookupKey GetEncodingLookupKey(
			XexType xexType, SimdPrefix simdPrefix, OpcodeMap opcodeMap, byte opcode)
		{
			EncodingLookupKey lookupKey;
			switch (xexType)
			{
				case XexType.Escapes:
				case XexType.RexAndEscapes:
					lookupKey = EncodingLookupKey.XexType_Escapes_MaybeRex;
					// We cannot know whether the opcode admits a SIMD prefix or not,
					// so it cannot be part of the lookup key.
					simdPrefix = SimdPrefix.None;
					break;

				case XexType.Vex2:
				case XexType.Vex3:
					lookupKey = EncodingLookupKey.XexType_Vex;
					break;

				case XexType.Xop: lookupKey = EncodingLookupKey.XexType_Xop; break;
				case XexType.EVex: lookupKey = EncodingLookupKey.XexType_EVex; break;
				default: throw new ArgumentOutOfRangeException(nameof(xexType));
			}

			return lookupKey | GetEncodingLookupKeyWithoutXexType(simdPrefix, opcodeMap, opcode);
		}

		private static EncodingLookupKey GetEncodingLookupKeyWithoutXexType(
			SimdPrefix simdPrefix, OpcodeMap opcodeMap, byte opcode)
		{
			return (EncodingLookupKey)(
				((uint)simdPrefix << (int)EncodingLookupKey.SimdPrefix_Shift)
				| ((uint)opcodeMap << (int)EncodingLookupKey.OpcodeMap_Shift)
				| ((((uint)opcode & 0b1111_1000) >> 3) << (int)EncodingLookupKey.OpcodeHigh5Bits_Shift));
		}

		private static EncodingLookupKey GetLookupKey(in OpcodeEncoding encoding)
		{
			EncodingLookupKey lookupKey;
			var simdPrefix = encoding.SimdPrefix.GetValueOrDefault();
			switch (encoding.Flags & OpcodeEncodingFlags.XexType_Mask)
			{
				case OpcodeEncodingFlags.XexType_Escapes_RexOpt:
					lookupKey = EncodingLookupKey.XexType_Escapes_MaybeRex;
					// The encoding might know that a SIMD prefix is required,
					// but given a raw instruction, we do not know, so it
					// cannot be part of the key.
					simdPrefix = SimdPrefix.None;
					break;

				case OpcodeEncodingFlags.XexType_Vex:
					lookupKey = EncodingLookupKey.XexType_Vex;
					break;

				case OpcodeEncodingFlags.XexType_Xop:
					lookupKey = EncodingLookupKey.XexType_Xop;
					break;

				case OpcodeEncodingFlags.XexType_EVex:
					lookupKey = EncodingLookupKey.XexType_EVex;
					break;

				default: throw new UnreachableException();
			}

			return lookupKey | GetEncodingLookupKeyWithoutXexType(
				simdPrefix, encoding.Map, encoding.MainByte);
		}
	}
}
