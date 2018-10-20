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
		Ieee754Float, // f16, f32, f64
		X87Float80, // f80 (not vectorable)
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
	public readonly struct DataType : IEquatable<DataType>
	{
		private readonly byte scalarSizeInBytesMinusOne;
		private readonly byte vectorLengthLog2_scalarType;

		public DataType(ScalarType scalarType, int scalarSizeInBytes, byte vectorLength)
		{
			throw new NotImplementedException();
		}

		public DataType(ScalarType scalarType, int scalarSizeInBytes)
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

		public bool Equals(DataType other)
			=> scalarSizeInBytesMinusOne == other.scalarSizeInBytesMinusOne
			&& vectorLengthLog2_scalarType == other.vectorLengthLog2_scalarType;
		public override bool Equals(object obj) => obj is DataType && Equals((DataType)obj);
		public override int GetHashCode() => ((int)scalarSizeInBytesMinusOne << 8) | vectorLengthLog2_scalarType;
		public static bool Equals(DataType lhs, DataType rhs) => lhs.Equals(rhs);
		public static bool operator ==(DataType lhs, DataType rhs) => Equals(lhs, rhs);
		public static bool operator !=(DataType lhs, DataType rhs) => !Equals(lhs, rhs);
		
		public static readonly DataType Byte = new DataType(ScalarType.Untyped, 1);
		public static readonly DataType Word = new DataType(ScalarType.Untyped, 2);
		public static readonly DataType Dword = new DataType(ScalarType.Untyped, 4);
		public static readonly DataType Qword = new DataType(ScalarType.Untyped, 8);
		public static readonly DataType Untyped80 = new DataType(ScalarType.Untyped, 10);
		public static readonly DataType Untyped128 = new DataType(ScalarType.Untyped, 16);
		public static readonly DataType Untyped256 = new DataType(ScalarType.Untyped, 32);
		public static readonly DataType Untyped512 = new DataType(ScalarType.Untyped, 64);

		public static readonly DataType I8 = new DataType(ScalarType.SignedInt, 1);
		public static readonly DataType I16 = new DataType(ScalarType.SignedInt, 2);
		public static readonly DataType I32 = new DataType(ScalarType.SignedInt, 4);
		public static readonly DataType I64 = new DataType(ScalarType.SignedInt, 8);

		public static readonly DataType U8 = new DataType(ScalarType.UnsignedInt, 1);
		public static readonly DataType U16 = new DataType(ScalarType.UnsignedInt, 2);
		public static readonly DataType U32 = new DataType(ScalarType.UnsignedInt, 4);
		public static readonly DataType U64 = new DataType(ScalarType.UnsignedInt, 8);

		public static readonly DataType F16 = new DataType(ScalarType.Ieee754Float, 2);
		public static readonly DataType F32 = new DataType(ScalarType.Ieee754Float, 4);
		public static readonly DataType F64 = new DataType(ScalarType.Ieee754Float, 8);
		public static readonly DataType F80 = new DataType(ScalarType.X87Float80, 10);
		
		public static readonly DataType NearPtr16 = new DataType(ScalarType.NearPointer, 2);
		public static readonly DataType NearPtr32 = new DataType(ScalarType.NearPointer, 4);
		public static readonly DataType NearPtr64 = new DataType(ScalarType.NearPointer, 8);

		public static readonly DataType FarPtr16 = new DataType(ScalarType.FarPointer, 4);
		public static readonly DataType FarPtr32 = new DataType(ScalarType.FarPointer, 6);
		public static readonly DataType FarPtr64 = new DataType(ScalarType.FarPointer, 10);

		public static readonly DataType UnpackedBcd = new DataType(ScalarType.UnpackedBcd, 1);
		public static readonly DataType PackedBcd = new DataType(ScalarType.PackedBcd, 1);

		public static readonly DataType DescriptorTable32 = new DataType(ScalarType.DescriptorTable, 6);
		public static readonly DataType DescriptorTable64 = new DataType(ScalarType.DescriptorTable, 10);
	}
}
