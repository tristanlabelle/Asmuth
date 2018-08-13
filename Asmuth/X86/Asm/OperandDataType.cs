using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Asm
{
	public enum OperandDataType : ushort
	{
		ElementSize_Shift = 0,
		ElementSize_0Bits = 0 << (int)ElementSize_Shift, // Doesn't actually access memory, like LEA
		ElementSize_Byte = 1 << (int)ElementSize_Shift,
		ElementSize_Word = 2 << (int)ElementSize_Shift,
		ElementSize_Dword = 4 << (int)ElementSize_Shift,
		ElementSize_48Bits = 6 << (int)ElementSize_Shift,
		ElementSize_Qword = 8 << (int)ElementSize_Shift,
		ElementSize_80Bits = 10 << (int)ElementSize_Shift,
		ElementSize_128Bits = 16 << (int)ElementSize_Shift,
		ElementSize_256Bits = 32 << (int)ElementSize_Shift,
		ElementSize_512Bits = 64 << (int)ElementSize_Shift,
		ElementSize_Mask = 0xFF << (int)ElementSize_Shift,

		VectorLength_Shift = ElementSize_Shift + 8,
		VectorLength_1 = 0 << (int)ElementSize_Shift,
		VectorLength_2 = 1 << (int)ElementSize_Shift,
		VectorLength_4 = 2 << (int)ElementSize_Shift,
		VectorLength_8 = 3 << (int)ElementSize_Shift,
		VectorLength_16 = 4 << (int)ElementSize_Shift,
		VectorLength_Mask = 7 << (int)ElementSize_Shift,

		Type_Shift = VectorLength_Shift + 3,
		Type_Unknown = 0 << (int)Type_Shift,
		Type_Int = 1 << (int)Type_Shift,
		Type_Float = 2 << (int)Type_Shift,
		Type_FarPtr = 3 << (int)Type_Shift,
		Type_Mask = 0xF << (int)Type_Shift,

		Unknown = Type_Unknown | ElementSize_0Bits,

		_8 = Type_Unknown | ElementSize_Byte,
		_16 = Type_Unknown | ElementSize_Word,
		_32 = Type_Unknown | ElementSize_Dword,
		_64 = Type_Unknown | ElementSize_Qword,
		_128 = Type_Unknown | ElementSize_128Bits,
		_256 = Type_Unknown | ElementSize_256Bits,
		_512 = Type_Unknown | ElementSize_512Bits,

		I8 = Type_Int | ElementSize_Byte,
		I16 = Type_Int | ElementSize_Word,
		I32 = Type_Int | ElementSize_Dword,
		I64 = Type_Int | ElementSize_Qword,

		F32 = Type_Float | ElementSize_Dword,
		F64 = Type_Float | ElementSize_Qword,
		F80 = Type_Float | ElementSize_80Bits,

		FarPtr16 = Type_FarPtr | ElementSize_Dword,
		FarPtr32 = Type_FarPtr | ElementSize_48Bits,
		FarPtr64 = Type_FarPtr | ElementSize_80Bits,
	}

	public static class OperandDataTypeEnum
	{
		public static bool IsVector(this OperandDataType type)
			=> (type & OperandDataType.VectorLength_Mask) != OperandDataType.VectorLength_1;

		public static int GetVectorLength(this OperandDataType type)
			=> 1 << ((int)(type & OperandDataType.VectorLength_Mask) >> (int)OperandDataType.VectorLength_Shift);

		public static int GetElementSizeInBytes(this OperandDataType type)
			=> (int)(type & OperandDataType.ElementSize_Mask) >> (int)OperandDataType.ElementSize_Shift;

		public static int GetElementSizeInBits(this OperandDataType type)
			=> GetElementSizeInBytes(type) * 8;

		public static int GetTotalSizeInBytes(this OperandDataType type)
			=> GetElementSizeInBytes(type) * GetVectorLength(type);

		public static int GetTotalSizeInBits(this OperandDataType type)
			=> GetTotalSizeInBytes(type) * 8;

		public static int? GetImpliedRegisterSizeInBytes(this OperandDataType type)
		{
			if ((type & OperandDataType.Type_Mask) == OperandDataType.Type_FarPtr) return null;
			if ((type & OperandDataType.ElementSize_Mask) == OperandDataType.ElementSize_0Bits) return null;
			return type.GetTotalSizeInBytes();
		}
	}
}
