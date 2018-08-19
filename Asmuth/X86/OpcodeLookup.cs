using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86
{
	/// <summary>
	/// Provides helper functions for two-stage opcode lookup,
	/// the first of which is a simple key-based lookup.
	/// </summary>
    public static class OpcodeLookup
    {
		public enum BucketKey : ushort { }

		private const int NullableVexTypeShift = 0;
		private const int SimdPrefixShift = NullableVexTypeShift + 2;
		private const int MapShift = SimdPrefixShift + 2;
		private const int MainByteHigh5Bits = MapShift + 4;
		private const ushort TestOverflow = 0x1F << MainByteHigh5Bits;

		public static BucketKey GetBucketKey(VexType? vexType, SimdPrefix potentialSimdPrefix,
			OpcodeMap map, byte mainByte)
		{
			int key = 0;
			if (vexType.HasValue)
			{
				key |= ((int)vexType.Value + 1) << NullableVexTypeShift;
				key |= (int)potentialSimdPrefix << SimdPrefixShift;
			}
			else
			{
				// The potential SIMD prefix might or not be one, so don't take it
				// into account.
			}

			key |= (int)map << MapShift;
			key |= (int)(mainByte >> 3) << MainByteHigh5Bits;
			return (BucketKey)key;
		}

		public static BucketKey GetBucketKey(ImmutableLegacyPrefixList legacyPrefixes, Xex xex, byte mainByte)
			=> GetBucketKey(xex.VexType,
				xex.SimdPrefix.GetValueOrDefault(legacyPrefixes.PotentialSimdPrefix),
				xex.OpcodeMap, mainByte);

		public static BucketKey GetBucketKey(in Instruction instruction)
			=> GetBucketKey(instruction.LegacyPrefixes, instruction.Xex, instruction.MainOpcodeByte);

		public static BucketKey GetBucketKey(in OpcodeEncoding encoding)
			=> GetBucketKey(encoding.VexType, encoding.SimdPrefix.GetValueOrDefault(),
				encoding.Map, encoding.MainByte);
    }
}
