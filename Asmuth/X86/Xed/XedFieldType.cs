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
		private sealed class XedFieldBitsType : XedFieldType
		{
			public override string Name => "xed_bits_t";
			public override int? SizeInBits => null;
		}

		public abstract string Name { get; }
		public abstract int? SizeInBits { get; }

		public sealed override string ToString() => Name;

		public static XedFieldType Bits { get; } = new XedFieldBitsType();
	}

	public abstract class XedFieldIntegerType : XedFieldType
	{
		
	}

	public abstract class XedFieldEnumerationType : XedFieldType
	{
		public abstract string EnumerationName { get; }

		public abstract int GetValue(string enumerant);
		public abstract string GetEnumerant(int value);

		public override string Name => $"xed_{EnumerationName}_enum_t";
	}

	public abstract class XedFieldRegisterEnumerationType : XedFieldEnumerationType
	{
		public XedRegisterTable RegisterTable { get; }
		public override int? SizeInBits => 16;
		public override string EnumerationName => "reg";

		public override int GetValue(string enumerant) => RegisterTable.ByName[enumerant].IndexInTable + 1;
		public override string GetEnumerant(int value) => RegisterTable.ByIndex[value - 1].Name;
	}
}
