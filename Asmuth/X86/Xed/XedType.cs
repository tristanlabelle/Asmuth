using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Xed
{
	public enum XedType : byte
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

	public static class XedTypeEnum
	{
		public static int? GetFixedSizeInBytes(this XedType type)
		{
			switch (type)
			{
				case XedType.Single: return 4;
				case XedType.Double: return 8;
				case XedType.LongDouble: return 10;
				case XedType.LongBCD: return 10;
				case XedType.Float16: return 2;
				default: return null;
			}
		}

		public static int? GetFixedSizeInBits(this XedType type)
			=> GetFixedSizeInBytes(type) * 8;
	}
}
