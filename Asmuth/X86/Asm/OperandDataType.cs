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

		ElementType_Shift = VectorLength_Shift + 3,
		ElementType_Unknown = 0 << (int)ElementType_Shift,
		ElementType_Int = 1 << (int)ElementType_Shift,
		ElementType_Float = 2 << (int)ElementType_Shift,
		ElementType_MemoryAddress = 3 << (int)ElementType_Shift, // moffs8/16/32/64
		ElementType_FarPtr = 4 << (int)ElementType_Shift, // ptr16:16/32,m16:16/32/64
		ElementType_Mask = 0xF << (int)ElementType_Shift,
		// Bounds, BCD

		Unknown = ElementType_Unknown | ElementSize_0Bits,

		Byte = ElementType_Unknown | ElementSize_Byte,
		Word = ElementType_Unknown | ElementSize_Word,
		Dword = ElementType_Unknown | ElementSize_Dword,
		Qword = ElementType_Unknown | ElementSize_Qword,
		_128 = ElementType_Unknown | ElementSize_128Bits,
		_256 = ElementType_Unknown | ElementSize_256Bits,
		_512 = ElementType_Unknown | ElementSize_512Bits,

		I8 = ElementType_Int | ElementSize_Byte,
		I16 = ElementType_Int | ElementSize_Word,
		I32 = ElementType_Int | ElementSize_Dword,
		I64 = ElementType_Int | ElementSize_Qword,

		F32 = ElementType_Float | ElementSize_Dword,
		F64 = ElementType_Float | ElementSize_Qword,
		F80 = ElementType_Float | ElementSize_80Bits,

		FarPtr16_16 = ElementType_FarPtr | ElementSize_Dword,
		FarPtr16_32 = ElementType_FarPtr | ElementSize_48Bits,
		FarPtr16_64 = ElementType_FarPtr | ElementSize_80Bits,
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

		public static IntegerSize? GetImpliedGprSize(this OperandDataType type)
		{
			if ((type & OperandDataType.ElementType_Mask) != OperandDataType.ElementType_Unknown
				&& (type & OperandDataType.ElementType_Mask) != OperandDataType.ElementType_Int) return null;
			if (GetVectorLength(type) != 1) return null;
			return IntegerSizeEnum.TryFromBytes(GetElementSizeInBytes(type));
		}
	}
}
