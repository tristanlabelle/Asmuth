using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Xed
{
	public enum XedBaseType : byte
	{ 
		UInt, // Unsigned integer
		Int, // Signed integer
		Single, // 32b FP single precision
		Double, // 64b FP double precision
		LongDouble, // 80b FP x87
		LongBCD, // 80b decimal BCD
		Struct, // a structure of various fields
		Variable, // depends on other fields in the instruction
		Float16, // 16b floating point
	}

	public static class XedBaseTypeEnum
	{
		public static int? GetFixedSizeInBytes(this XedBaseType type)
		{
			switch (type)
			{
				case XedBaseType.Single: return 4;
				case XedBaseType.Double: return 8;
				case XedBaseType.LongDouble: return 10;
				case XedBaseType.LongBCD: return 10;
				case XedBaseType.Float16: return 2;
				default: return null;
			}
		}

		public static bool IsFloat(this XedBaseType type)
			=> type == XedBaseType.Float16 || type == XedBaseType.Single
			|| type == XedBaseType.Double || type == XedBaseType.LongDouble;

		public static int? GetFixedSizeInBits(this XedBaseType type)
			=> GetFixedSizeInBytes(type) * 8;
	}
}
