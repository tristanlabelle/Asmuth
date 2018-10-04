using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Xed
{
	// xed_u?int(8|16|32|64)_t
	// xed_bits_t
	// xed_(chip|reg|error|iclass)_enum_t

	public abstract class XedFieldType
	{
		public abstract string NameInCode { get; }
		public abstract int SizeInBits { get; }

		public abstract string FormatValue(ulong value);

		public sealed override string ToString() => NameInCode;
	}

	public sealed class XedFieldIntegerType : XedFieldType
	{
		private readonly byte sizeInBytes;
		public bool IsSigned { get; }

		private XedFieldIntegerType(int sizeInBytes, bool isSigned)
		{
			if (sizeInBytes != sizeof(byte) && sizeInBytes != sizeof(ushort)
				&& sizeInBytes != sizeof(uint) && sizeInBytes != sizeof(ulong))
				throw new ArgumentOutOfRangeException(nameof(sizeInBytes));
			this.sizeInBytes = (byte)sizeInBytes;
			this.IsSigned = isSigned;
		}

		public int SizeInBytes => sizeInBytes;
		public override int SizeInBits => sizeInBytes * 8;
		public override string NameInCode => (IsSigned ? "xed_int" : "xed_uint") + SizeInBits + "_t";

		public override string FormatValue(ulong value) => throw new NotImplementedException();

		public static XedFieldIntegerType Get(int sizeInBytes, bool isSigned)
		{
			switch (sizeInBytes)
			{
				case 1: return isSigned ? Int8 : UInt8;
				case 2: return isSigned ? Int16 : UInt16;
				case 4: return isSigned ? Int32 : UInt32;
				case 8: return isSigned ? Int64 : UInt64;
				default: throw new ArgumentOutOfRangeException(nameof(sizeInBytes));
			}
		}

		public static XedFieldIntegerType UInt8 { get; } = new XedFieldIntegerType(1, false);
		public static XedFieldIntegerType UInt16 { get; } = new XedFieldIntegerType(2, false);
		public static XedFieldIntegerType UInt32 { get; } = new XedFieldIntegerType(4, false);
		public static XedFieldIntegerType UInt64 { get; } = new XedFieldIntegerType(8, false);
		public static XedFieldIntegerType Int8 { get; } = new XedFieldIntegerType(1, true);
		public static XedFieldIntegerType Int16 { get; } = new XedFieldIntegerType(2, true);
		public static XedFieldIntegerType Int32 { get; } = new XedFieldIntegerType(4, true);
		public static XedFieldIntegerType Int64 { get; } = new XedFieldIntegerType(8, true);
	}

	public sealed class XedFieldBitsType : XedFieldType
	{
		private readonly byte size;
		
		private XedFieldBitsType(int size)
		{
			if (size < 1 || size > 32) throw new ArgumentOutOfRangeException(nameof(size));
			this.size = (byte)size;
		}

		public override string NameInCode => "xed_bits_t";
		public override int SizeInBits => size;

		public override string FormatValue(ulong value) => throw new NotImplementedException();

		public static XedFieldBitsType FromSize(int size)
		{
			switch (size)
			{
				case 1: return Bit;
				case 2: return _2;
				case 3: return _3;
				default: return new XedFieldBitsType(size);
			}
		}

		public static XedFieldBitsType Bit { get; } = new XedFieldBitsType(1);
		public static XedFieldBitsType _2 { get; } = new XedFieldBitsType(2);
		public static XedFieldBitsType _3 { get; } = new XedFieldBitsType(3);
	}

	public abstract class XedFieldEnumerationType : XedFieldType
	{
		public abstract string ShortName { get; }

		public abstract int GetValue(string enumerant);
		public abstract string GetEnumerant(int value);

		public override sealed string NameInCode => $"xed_{ShortName}_enum_t";

		public override sealed string FormatValue(ulong value)
			=> "XED_" + ShortName.ToUpperInvariant() + "_" + GetEnumerant((int)value);
	}

	public abstract class XedFieldRegisterEnumerationType : XedFieldEnumerationType
	{
		public XedRegisterTable RegisterTable { get; }
		public override int SizeInBits => 16;
		public override string ShortName => "reg";

		public override int GetValue(string enumerant) => RegisterTable.ByName[enumerant].IndexInTable + 1;
		public override string GetEnumerant(int value) => RegisterTable.ByIndex[value - 1].Name;
	}
}
