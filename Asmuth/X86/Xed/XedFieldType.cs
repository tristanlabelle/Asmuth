using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Asmuth.X86.Xed
{
	public abstract class XedFieldType : IEquatable<XedFieldType>
	{
		public abstract string NameInCode { get; }
		public abstract int SizeInBits { get; }

		public abstract string FormatValue(ulong value);

		public sealed override string ToString() => NameInCode;
		
		public abstract bool Equals(XedFieldType other);
		public override sealed bool Equals(object obj) => obj is XedFieldType && Equals((XedFieldType)obj);
		public override abstract int GetHashCode();
		public static bool Equals(XedFieldType lhs, XedFieldType rhs) => lhs.Equals(rhs);
		public static bool operator ==(XedFieldType lhs, XedFieldType rhs) => Equals(lhs, rhs);
		public static bool operator !=(XedFieldType lhs, XedFieldType rhs) => !Equals(lhs, rhs);

		private static readonly Regex nameInCodeRegex = new Regex(
			@"xed_(
				(?<i>(?<u>u)?int(?<s>8|16|32|64))
				| (?<b>bits)
				| (?<e>\w+)_enum
			)_t", RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

		public static XedFieldType FromNameInCodeAndSizeInBits(string nameInCode, int sizeInBits,
			Func<string, XedEnumFieldType> enumerationShortNameResolver)
		{
			var match = nameInCodeRegex.Match(nameInCode);
			if (!match.Success) throw new FormatException();

			if (match.Groups["b"].Success) return XedBitsFieldType.FromSize(sizeInBits);

			XedFieldType fieldType;
			if (match.Groups["i"].Success)
			{
				int sizeInBytes = int.Parse(match.Groups["s"].Value, CultureInfo.InvariantCulture) / 8;
				fieldType = XedIntegerFieldType.Get(sizeInBytes, isSigned: !match.Groups["u"].Success);
			}
			else if (match.Groups["e"].Success)
			{
				fieldType = enumerationShortNameResolver(match.Groups["e"].Value);
			}
			else throw new UnreachableException();

			if (fieldType.SizeInBits != sizeInBits)
				throw new ArgumentOutOfRangeException(nameof(sizeInBits));

			return fieldType;
		}
	}
	
	// xed_u?int(8|16|32|64)_t
	public sealed class XedIntegerFieldType : XedFieldType
	{
		private readonly byte sizeInBytes;
		public bool IsSigned { get; }

		private XedIntegerFieldType(int sizeInBytes, bool isSigned)
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

		public override string FormatValue(ulong value)
			=> IsSigned ? ((long)value).ToString() : value.ToString();

		public override bool Equals(XedFieldType other) => ReferenceEquals(this, other);
		public override int GetHashCode() => GetType().GetHashCode() ^ (IsSigned ? ~sizeInBytes : sizeInBytes);

		public static XedIntegerFieldType Get(int sizeInBytes, bool isSigned)
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

		public static XedIntegerFieldType UInt8 { get; } = new XedIntegerFieldType(1, false);
		public static XedIntegerFieldType UInt16 { get; } = new XedIntegerFieldType(2, false);
		public static XedIntegerFieldType UInt32 { get; } = new XedIntegerFieldType(4, false);
		public static XedIntegerFieldType UInt64 { get; } = new XedIntegerFieldType(8, false);
		public static XedIntegerFieldType Int8 { get; } = new XedIntegerFieldType(1, true);
		public static XedIntegerFieldType Int16 { get; } = new XedIntegerFieldType(2, true);
		public static XedIntegerFieldType Int32 { get; } = new XedIntegerFieldType(4, true);
		public static XedIntegerFieldType Int64 { get; } = new XedIntegerFieldType(8, true);
	}

	// xed_bits_t
	public sealed class XedBitsFieldType : XedFieldType
	{
		private readonly byte size;
		
		private XedBitsFieldType(int size)
		{
			if (size < 1 || size > 32) throw new ArgumentOutOfRangeException(nameof(size));
			this.size = (byte)size;
		}

		public override string NameInCode => "xed_bits_t";
		public override int SizeInBits => size;

		public override string FormatValue(ulong value)
			=> size == 8
				?  "0x" + value.ToString("X2", CultureInfo.InvariantCulture)
				: Convert.ToString(unchecked((long)value), toBase: 2).PadLeft(size, '0');

		public override bool Equals(XedFieldType other) => size == (other as XedBitsFieldType)?.size;
		public override int GetHashCode() => GetType().GetHashCode() ^ size;

		public static XedBitsFieldType FromSize(int size)
		{
			switch (size)
			{
				case 1: return Bit;
				case 2: return _2;
				case 3: return _3;
				default: return new XedBitsFieldType(size);
			}
		}

		public static XedBitsFieldType Bit { get; } = new XedBitsFieldType(1);
		public static XedBitsFieldType _2 { get; } = new XedBitsFieldType(2);
		public static XedBitsFieldType _3 { get; } = new XedBitsFieldType(3);
	}

	// xed_(chip|reg|error|iclass)_enum_t
	public abstract class XedEnumFieldType : XedFieldType
	{
		private sealed class ErrorEnumType : XedEnumFieldType
		{
			public override string ShortName => "error";
			public override int SizeInBits => 8;

			protected override string GetEnumerant_NotNull(ushort value)
				=> XedEnumNameAttribute.GetNameOrNull((XedError)value);
			protected override ushort GetValue_NotNull(string enumerant)
				=> (ushort)XedEnumNameAttribute.GetEnumerantOrNull<XedError>(enumerant).Value;
		}

		private sealed class DummyEnumType : XedEnumFieldType
		{
			private readonly string shortName;
			private readonly byte sizeInBits;
			public override string ShortName => shortName;
			public override int SizeInBits => sizeInBits;

			public DummyEnumType(string shortName, byte sizeInBits)
			{
				this.shortName = shortName ?? throw new ArgumentNullException(nameof(shortName));
				this.sizeInBits = sizeInBits;
			}

			protected override string GetEnumerant_NotNull(ushort value)
				=> throw new NotSupportedException();
			protected override ushort GetValue_NotNull(string enumerant)
				=> throw new NotSupportedException();
		}

		public abstract string ShortName { get; }

		public override sealed string NameInCode => $"xed_{ShortName}_enum_t";

		public ushort GetValue(string enumerant)
		{
			if (enumerant == "@") return 0;
			string prefix = "XED_" + ShortName.ToUpperInvariant() + "_";
			if (!enumerant.StartsWith(prefix)) throw new FormatException();
			string shortName = enumerant.Substring(prefix.Length);
			if (shortName == "INVALID") return 0;
			return GetValue_NotNull(shortName);
		}

		public string GetEnumerant(ushort value) => value == 0 ? "@" : GetEnumerant((ushort)(value - 1));

		public override sealed string FormatValue(ulong value)
			=> "XED_" + ShortName.ToUpperInvariant() + "_" + GetEnumerant((ushort)value);

		public override sealed bool Equals(XedFieldType other) => ReferenceEquals(this, other);
		public override sealed int GetHashCode() => GetType().GetHashCode();

		protected abstract ushort GetValue_NotNull(string enumerant);
		protected abstract string GetEnumerant_NotNull(ushort value);

		public static XedEnumFieldType Error { get; } = new ErrorEnumType();
		public static XedEnumFieldType DummyChip { get; } = new DummyEnumType("chip", 16);
		public static XedEnumFieldType DummyIClass { get; } = new DummyEnumType("iclass", 16);
	}

	public sealed class XedRegisterFieldType : XedEnumFieldType
	{
		public XedRegisterTable RegisterTable { get; }
		public override int SizeInBits => 16;
		public override string ShortName => "reg";

		public XedRegisterFieldType(XedRegisterTable registerTable)
		{
			this.RegisterTable = registerTable ?? throw new ArgumentNullException(nameof(registerTable));
		}

		protected override ushort GetValue_NotNull(string enumerant) => (ushort)RegisterTable.ByName[enumerant].IndexInTable;
		protected override string GetEnumerant_NotNull(ushort value) => RegisterTable.ByIndex[value].Name;
	}
}
