using System;
using System.Collections.Generic;
using System.Globalization;
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
			if (type == ScalarType.Untyped) return size > 0;

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

	[StructLayout(LayoutKind.Sequential, Size = 4)]
	public readonly struct OperandDataType : IEquatable<OperandDataType>
	{
		private static readonly Bitfield32.Builder bitfieldBuilder = new Bitfield32.Builder();
		private readonly Bitfield32 bitfield;

		private static readonly Bitfield32.UInt4 scalarTypeField = bitfieldBuilder;
		private static readonly Bitfield32.UInt3 vectorSizeLog2Field = bitfieldBuilder;
		private static readonly Bitfield32.Bool isVariableScalarSizeField = bitfieldBuilder;

		// Either it's a single size which can be very big,
		// or it's a operand/address-size dependent size which are smaller.
		private static readonly Bitfield32.UInt singleScalarSizeMinusOneField = new Bitfield32.UInt(bitfieldBuilder.Clone(), 24);
		private static readonly Bitfield32.Byte scalarSize16Field = bitfieldBuilder;
		private static readonly Bitfield32.Byte scalarSize32Field = bitfieldBuilder;
		private static readonly Bitfield32.Byte scalarSize64Field = bitfieldBuilder;

		private OperandDataType(ScalarType scalarType, int scalarSizeInBytes, int vectorLength)
			=> bitfield = InitWithSingleSize(scalarType, scalarSizeInBytes, vectorLength);

		public OperandDataType(ScalarType scalarType, int sizeInBytes)
			: this(scalarType, sizeInBytes, sizeInBytes, sizeInBytes) {}

		private OperandDataType(ScalarType scalarType, int sizeInBytes16, int sizeInBytes32, int sizeInBytes64)
		{
			if (sizeInBytes16 == sizeInBytes32 && sizeInBytes32 == sizeInBytes64)
			{
				bitfield = InitWithSingleSize(scalarType, sizeInBytes16);
			}
			else
			{
				if (sizeInBytes16 != 0 && (!scalarType.IsValidSizeInBytes(sizeInBytes16) || sizeInBytes16 > byte.MaxValue))
					throw new ArgumentOutOfRangeException(nameof(sizeInBytes16));
				if (sizeInBytes32 != 0 && (!scalarType.IsValidSizeInBytes(sizeInBytes32) || sizeInBytes32 > byte.MaxValue))
					throw new ArgumentOutOfRangeException(nameof(sizeInBytes32));
				if (sizeInBytes64 != 0 && (!scalarType.IsValidSizeInBytes(sizeInBytes64) || sizeInBytes64 > byte.MaxValue))
					throw new ArgumentOutOfRangeException(nameof(sizeInBytes64));

				bitfield = default;
				bitfield[scalarTypeField] = (byte)scalarType;
				bitfield[isVariableScalarSizeField] = true;
				bitfield[scalarSize16Field] = (byte)sizeInBytes16;
				bitfield[scalarSize32Field] = (byte)sizeInBytes32;
				bitfield[scalarSize64Field] = (byte)sizeInBytes64;
			}
		}

		public ScalarType ScalarType => (ScalarType)bitfield[scalarTypeField];
		public bool HasDependentScalarSize => bitfield[isVariableScalarSizeField];
		public int? ScalarSizeInBytes => bitfield[isVariableScalarSizeField]
			? null : (int?)(bitfield[singleScalarSizeMinusOneField] + 1);
		public int? ScalarSizeInBits => ScalarSizeInBytes * 8;
		public int ScalarSizeInBytes_16 => GetVariableScalarSize(scalarSize16Field);
		public int ScalarSizeInBytes_32 => GetVariableScalarSize(scalarSize32Field);
		public int ScalarSizeInBytes_64 => GetVariableScalarSize(scalarSize64Field);

		public int VectorLength => 1 << bitfield[vectorSizeLog2Field];
		public bool IsVector => bitfield[vectorSizeLog2Field] != 0;

		public int? TotalSizeInBytes => ScalarSizeInBytes * VectorLength;
		public int? TotalSizeInBits => TotalSizeInBytes * 8;

		private static Bitfield32 InitWithSingleSize(ScalarType scalarType, int sizeInBytes, int vectorLength = 1)
		{
			var bitfield = new Bitfield32();
			
			if (!scalarType.IsValidSizeInBytes(sizeInBytes) || sizeInBytes >= singleScalarSizeMinusOneField.MaxValue)
				throw new ArgumentOutOfRangeException(nameof(sizeInBytes));

			bitfield[scalarTypeField] = (byte)scalarType;
			bitfield[singleScalarSizeMinusOneField] = (uint)(sizeInBytes - 1);

			if (vectorLength != 1) throw new NotImplementedException();

			return bitfield;
		}

		[Obsolete("For NASM size matching.")]
		public IntegerSize? GetImpliedGprSize()
		{
			return (ScalarType == ScalarType.Untyped || ScalarType == ScalarType.SignedInt || ScalarType == ScalarType.UnsignedInt)
				&& !IsVector ? IntegerSizeEnum.TryFromBytes(ScalarSizeInBytes.Value) : null;
		}

		public bool Equals(OperandDataType other) => bitfield == other.bitfield;
		public override bool Equals(object obj) => obj is OperandDataType && Equals((OperandDataType)obj);
		public override int GetHashCode() => bitfield.GetHashCode();
		public static bool Equals(OperandDataType lhs, OperandDataType rhs) => lhs.Equals(rhs);
		public static bool operator ==(OperandDataType lhs, OperandDataType rhs) => Equals(lhs, rhs);
		public static bool operator !=(OperandDataType lhs, OperandDataType rhs) => !Equals(lhs, rhs);

		public override string ToString()
		{
			var str = new StringBuilder();

			bool isTyped = ScalarType != ScalarType.Untyped;

			bool appendSize = true;
			if (isTyped)
			{
				switch (ScalarType)
				{
					case ScalarType.SignedInt: str.Append('i'); break;
					case ScalarType.UnsignedInt: str.Append('u'); break;
					case ScalarType.Ieee754Float: str.Append('f'); break;
					case ScalarType.X87Float80: str.Append("f80"); appendSize = false; break;
					case ScalarType.NearPointer: str.Append("ptr"); break;
					case ScalarType.FarPointer: str.Append("ptr16:"); break;
					case ScalarType.UnpackedBcd: str.Append("bcd4"); appendSize = false; break;
					case ScalarType.PackedBcd: str.Append("bcd8"); appendSize = false; break;
					case ScalarType.LongPackedBcd: str.Append("bcd80"); appendSize = false; break;
					default: str.Append('?'); break;
				}
			}

			if (appendSize)
			{
				if (HasDependentScalarSize)
				{
					AppendSizeAndSlash(str, ScalarSizeInBytes_16, isTyped);
					if (ScalarSizeInBytes_32 != ScalarSizeInBytes_16)
						AppendSizeAndSlash(str, ScalarSizeInBytes_32, isTyped);
					if (ScalarSizeInBytes_64 != ScalarSizeInBytes_32)
						AppendSizeAndSlash(str, ScalarSizeInBytes_64, isTyped);
				}
				else
				{
					AppendSizeAndSlash(str, ScalarSizeInBytes.Value, isTyped);
				}

				str.Length = str.Length - 1; // Remove trailing slash.
			}

			if (IsVector) str.Append('x').AppendFormat(CultureInfo.InvariantCulture, "{0}", VectorLength);

			return str.ToString();
		}

		private void AppendSizeAndSlash(StringBuilder str, int inBytes, bool isTyped)
		{
			if (inBytes == 0) return;

			bool appendByteCount = true;
			if (!isTyped)
			{
				appendByteCount = false;
				if (inBytes == 1) str.Append("byte");
				else if (inBytes == 2) str.Append("word");
				else if (inBytes == 4) str.Append("dword");
				else if (inBytes == 8) str.Append("qword");
				else
				{
					str.Append("data");
					appendByteCount = true;
				}
			}

			if (appendByteCount) str.AppendFormat(CultureInfo.InvariantCulture, "{0}", inBytes * 8);

			str.Append('/');
		}

		private int GetVariableScalarSize(Bitfield32.Byte field)
		{
			return bitfield[isVariableScalarSizeField]
				? bitfield[field] : (int)(bitfield[singleScalarSizeMinusOneField] + 1);
		}

		public static OperandDataType FromVector(ScalarType scalarType, int scalarSizeInBytes, int vectorLength)
			=> new OperandDataType(scalarType, scalarSizeInBytes, vectorLength);

		public static OperandDataType FromVariableSizeInBytes(ScalarType scalarType,
			int sizeInBytes16, int sizeInBytes32, int sizeInBytes64)
			=> new OperandDataType(scalarType, sizeInBytes16, sizeInBytes32, sizeInBytes64);

		public static readonly OperandDataType Byte = new OperandDataType(ScalarType.Untyped, 1);
		public static readonly OperandDataType Word = new OperandDataType(ScalarType.Untyped, 2);
		public static readonly OperandDataType Dword = new OperandDataType(ScalarType.Untyped, 4);
		public static readonly OperandDataType Qword = new OperandDataType(ScalarType.Untyped, 8);
		public static readonly OperandDataType Untyped80 = new OperandDataType(ScalarType.Untyped, 10);
		public static readonly OperandDataType Untyped128 = new OperandDataType(ScalarType.Untyped, 16);
		public static readonly OperandDataType Untyped256 = new OperandDataType(ScalarType.Untyped, 32);
		public static readonly OperandDataType Untyped512 = new OperandDataType(ScalarType.Untyped, 64);

		public static readonly OperandDataType WordOrDwordOrQword = FromVariableSizeInBytes(ScalarType.Untyped, 2, 4, 8);
		public static readonly OperandDataType DwordOrQword = FromVariableSizeInBytes(ScalarType.Untyped, 4, 4, 8);

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
