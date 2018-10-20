using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Asmuth.X86.Encoding
{
	public enum ScalarType : byte
	{
		Untyped, // byte, word, dword, qword, ...
		SignedInt,
		UnsignedInt,
		Ieee754Float,
		X87Float80,
		NearPointer,
		FarPointer, // ptr16:16, ptr16:32, ptr16:64
		UnpackedBcd,
		PackedBcd, // (not a vector because packed nibbles)
		LongPackedBcd, // m80bcd (not a vector because of sign nibble)
		DescriptorTable, // m16&32, m16&64
	}

	public static class ScalarTypeEnum
	{
		public static bool IsValidSizeInBytes(this ScalarType type, int size)
		{
			if (type == ScalarType.Untyped) return size > 0 && size <= 256;

			uint packedValidSizes = GetPackedValidSizes(type);
			while (packedValidSizes != 0)
			{
				if (size == (packedValidSizes & 0xFF)) return true;
				packedValidSizes >>= 8;
			}

			return false;
		}

		public static bool IsVectorable(this ScalarType type, int sizeInBytes)
		{
			switch (type)
			{
				case ScalarType.SignedInt:
				case ScalarType.UnsignedInt:
				case ScalarType.Ieee754Float:
					return true;

				default: return false;
			}
		}

		private static uint GetPackedValidSizes(ScalarType type)
		{
			switch (type)
			{
				case ScalarType.SignedInt: return 0x08_04_02_01;
				case ScalarType.UnsignedInt: return 0x08_04_02_01;
				case ScalarType.Ieee754Float: return 0x08_04_02;
				case ScalarType.X87Float80: return 10;
				case ScalarType.NearPointer: return 0x08_04_02;
				case ScalarType.FarPointer: return 0x0A_06_04;
				case ScalarType.PackedBcd: return 1;
				case ScalarType.UnpackedBcd: return 1;
				case ScalarType.LongPackedBcd: return 10;
				case ScalarType.DescriptorTable: return 0x0A_06;
				default: throw new ArgumentOutOfRangeException(nameof(type));
			}
		}
	}

	[StructLayout(LayoutKind.Sequential, Size = 2)]
	public readonly struct OperandDataType : IEquatable<OperandDataType>
	{
		private readonly byte scalarSizeInBytesMinusOne;
		private readonly byte vectorLengthLog2_scalarType;

		public OperandDataType(ScalarType scalarType, int scalarSizeInBytes, byte vectorLength)
		{
			throw new NotImplementedException();
		}

		public OperandDataType(ScalarType scalarType, int scalarSizeInBytes)
		{
			if (!scalarType.IsValidSizeInBytes(scalarSizeInBytes))
				throw new ArgumentOutOfRangeException(nameof(scalarSizeInBytes));
			scalarSizeInBytesMinusOne = (byte)(scalarSizeInBytes - 1);
			vectorLengthLog2_scalarType = (byte)scalarType;
		}

		public ScalarType ScalarType => (ScalarType)(vectorLengthLog2_scalarType & 0xF);
		public int ScalarSizeInBytes => scalarSizeInBytesMinusOne + 1;
		public int ScalarSizeInBits => ScalarSizeInBytes * 8;
		public int VectorLength => 1 << (vectorLengthLog2_scalarType >> 4);
		public bool IsVector => (vectorLengthLog2_scalarType & 0xF0) > 0;
		public int TotalSizeInBytes => ScalarSizeInBytes * VectorLength;
		public int TotalSizeInBits => TotalSizeInBytes * 8;

		public IntegerSize? GetImpliedGprSize()
		{
			return (ScalarType == ScalarType.Untyped || ScalarType == ScalarType.SignedInt || ScalarType == ScalarType.UnsignedInt)
				&& !IsVector ? IntegerSizeEnum.TryFromBytes(ScalarSizeInBytes) : null;
		}

		public bool Equals(OperandDataType other)
			=> scalarSizeInBytesMinusOne == other.scalarSizeInBytesMinusOne
			&& vectorLengthLog2_scalarType == other.vectorLengthLog2_scalarType;
		public override bool Equals(object obj) => obj is OperandDataType && Equals((OperandDataType)obj);
		public override int GetHashCode() => ((int)scalarSizeInBytesMinusOne << 8) | vectorLengthLog2_scalarType;
		public static bool Equals(OperandDataType lhs, OperandDataType rhs) => lhs.Equals(rhs);
		public static bool operator ==(OperandDataType lhs, OperandDataType rhs) => Equals(lhs, rhs);
		public static bool operator !=(OperandDataType lhs, OperandDataType rhs) => !Equals(lhs, rhs);
		
		public static readonly OperandDataType Byte = new OperandDataType(ScalarType.Untyped, 1);
		public static readonly OperandDataType Word = new OperandDataType(ScalarType.Untyped, 2);
		public static readonly OperandDataType Dword = new OperandDataType(ScalarType.Untyped, 4);
		public static readonly OperandDataType Qword = new OperandDataType(ScalarType.Untyped, 8);
		public static readonly OperandDataType Untyped80 = new OperandDataType(ScalarType.Untyped, 10);
		public static readonly OperandDataType Untyped128 = new OperandDataType(ScalarType.Untyped, 16);
		public static readonly OperandDataType Untyped256 = new OperandDataType(ScalarType.Untyped, 32);
		public static readonly OperandDataType Untyped512 = new OperandDataType(ScalarType.Untyped, 64);

		public static readonly OperandDataType I8 = new OperandDataType(ScalarType.SignedInt, 1);
		public static readonly OperandDataType I16 = new OperandDataType(ScalarType.SignedInt, 2);
		public static readonly OperandDataType I32 = new OperandDataType(ScalarType.SignedInt, 4);
		public static readonly OperandDataType I64 = new OperandDataType(ScalarType.SignedInt, 8);

		public static readonly OperandDataType U8 = new OperandDataType(ScalarType.UnsignedInt, 1);
		public static readonly OperandDataType U16 = new OperandDataType(ScalarType.UnsignedInt, 2);
		public static readonly OperandDataType U32 = new OperandDataType(ScalarType.UnsignedInt, 4);
		public static readonly OperandDataType U64 = new OperandDataType(ScalarType.UnsignedInt, 8);

		public static readonly OperandDataType F16 = new OperandDataType(ScalarType.Ieee754Float, 2);
		public static readonly OperandDataType F32 = new OperandDataType(ScalarType.Ieee754Float, 4);
		public static readonly OperandDataType F64 = new OperandDataType(ScalarType.Ieee754Float, 8);
		public static readonly OperandDataType F80 = new OperandDataType(ScalarType.X87Float80, 10);
		
		public static readonly OperandDataType NearPtr16 = new OperandDataType(ScalarType.NearPointer, 2);
		public static readonly OperandDataType NearPtr32 = new OperandDataType(ScalarType.NearPointer, 4);
		public static readonly OperandDataType NearPtr64 = new OperandDataType(ScalarType.NearPointer, 8);

		public static readonly OperandDataType FarPtr16 = new OperandDataType(ScalarType.FarPointer, 4);
		public static readonly OperandDataType FarPtr32 = new OperandDataType(ScalarType.FarPointer, 6);
		public static readonly OperandDataType FarPtr64 = new OperandDataType(ScalarType.FarPointer, 10);

		public static readonly OperandDataType UnpackedBcd = new OperandDataType(ScalarType.UnpackedBcd, 1);
		public static readonly OperandDataType PackedBcd = new OperandDataType(ScalarType.PackedBcd, 1);

		public static readonly OperandDataType DescriptorTable32 = new OperandDataType(ScalarType.DescriptorTable, 6);
		public static readonly OperandDataType DescriptorTable64 = new OperandDataType(ScalarType.DescriptorTable, 10);
	}
}
