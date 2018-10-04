using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Xed
{
	public enum XedBlotOperator : byte
	{
		Equal,
		NotEqual
	}

	public enum XedBlotValueKind : byte
	{
		Constant,
		Callee
	}

	public readonly struct XedBlot2
	{
		public XedField Field { get; }
		public string BitsPattern { get; }
		private readonly string callee;
		private readonly byte value;
		private readonly byte operatorPlusOne;

		public XedBlot2(XedField field, string bitsPattern)
		{
			this.Field = field ?? throw new ArgumentNullException(nameof(field));
			this.BitsPattern = bitsPattern ?? throw new ArgumentNullException(nameof(bitsPattern));
			this.callee = null;
			this.value = 0;
			this.operatorPlusOne = 0;
		}

		public XedBlot2(string bitsPattern)
		{
			this.Field = null;
			this.BitsPattern = bitsPattern;
			this.callee = null;
			this.value = 0;
			this.operatorPlusOne = 0;
		}

		public XedBlot2(XedField field, string bitsPattern, XedBlotOperator @operator, int value)
		{
			this.Field = field ?? throw new ArgumentNullException(nameof(field));
			this.BitsPattern = bitsPattern;
			this.callee = null;
			this.value = checked((byte)value);
			this.operatorPlusOne = (byte)(@operator + 1);
		}

		private readonly struct CalleeTag { } // For overloading
		private XedBlot2(XedField field, CalleeTag tag, string callee)
		{
			this.Field = field ?? throw new ArgumentNullException(nameof(field));
			this.BitsPattern = null;
			this.callee = callee;
			this.value = 0;
			this.operatorPlusOne = (byte)(XedBlotOperator.Equal + 1);
		}

		public XedBlot2(XedField field, XedBlotOperator @operator, int value)
			: this(field, bitsPattern: null, @operator, value) { }

		public bool HasValue => operatorPlusOne > 0;
		public XedBlotOperator? Operator => HasValue ? (XedBlotOperator?)(operatorPlusOne - 1) : null;
		public XedBlotValueKind? ValueKind => HasValue
			? (callee == null ? XedBlotValueKind.Constant : XedBlotValueKind.Callee)
			: (XedBlotValueKind?)null;
		public string ValueCallee => HasValue && callee != null
			? callee : throw new InvalidOperationException();
		public int Value => HasValue && callee == null
			? value : throw new InvalidOperationException();

		public override string ToString()
		{
			if (Field == null) return BitsPattern;

			var str = new StringBuilder();
			str.Append(Field.Name);
			if (BitsPattern != null) str.Append('[').Append(BitsPattern).Append(']');
			if (HasValue)
			{
				str.Append(Operator == XedBlotOperator.Equal ? "=" : "!=");
				if (ValueKind == XedBlotValueKind.Callee) str.Append(ValueCallee).Append("()");
				else str.Append(Field.Type.FormatValue(value));
			}

			return str.ToString();
		}

		public static XedBlot2 FromCallAssignment(XedField field, string callee)
			=> new XedBlot2(field, default(CalleeTag), callee);

	}
}
