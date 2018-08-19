using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86
{
	[Flags]
	public enum Rex : byte
	{
		ByteCount = 1,
		HighNibble = 0x40,

		Reserved_Mask = 0xF0,
		Reserved_Value = 0x40,

		B = 1 << 0,
		X = 1 << 1,
		R = 1 << 2,
		W = 1 << 3,

		BaseRegExtension = B,
		IndexRegExtension = X,
		ModRegExtension = R,
		OperandSize64 = W
	}

	public static class RexEnum
	{
		public static Rex FromBits(bool modRegExt, bool baseRegExt, bool indexRegExt, bool op64)
		{
			var rex = Rex.Reserved_Value;
			if (modRegExt) rex |= Rex.ModRegExtension;
			if (baseRegExt) rex |= Rex.BaseRegExtension;
			if (indexRegExt) rex |= Rex.IndexRegExtension;
			if (op64) rex |= Rex.OperandSize64;
			return rex;
		}

		public static string ToIntelStyleString(this Rex rex)
		{
			var stringBuilder = new StringBuilder(8);
			stringBuilder.Append("rex");
			if ((rex & ~Rex.Reserved_Mask) != 0)
			{
				stringBuilder.Append('.');
				for (int i = 3; i >= 0; i--)
					if ((rex & (Rex)(1 << i)) != 0)
						stringBuilder.Append("bxrw"[i]);
			}
			return stringBuilder.ToString();
		}
	}
}
