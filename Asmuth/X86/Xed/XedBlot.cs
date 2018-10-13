using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Asmuth.X86.Xed
{
	public enum XedBlotType : byte
	{
		Bits,
		Equality, // Can be assignment or comparison, depending on encoder/decoder context and field flags
		Inequality,
		Call,
	}

	public enum XedBlotValueKind : byte
	{
		Constant,
		Bits,
		CallResult
	}

	public readonly struct XedBlotValue : IEquatable<XedBlotValue>
	{
		private readonly string str; // Encoding bits, value bits or callee
		private readonly ushort constant;
		public XedBlotValueKind Kind { get; }

		private XedBlotValue(XedBlotValueKind kind, string str, ushort constant = 0)
		{
			this.Kind = kind;
			this.str = str;
			this.constant = constant;
		}

		public ushort Constant => Kind == XedBlotValueKind.Constant
			? constant : throw new InvalidOperationException();
		public string BitPattern => Kind == XedBlotValueKind.Bits
			? str : throw new InvalidOperationException();
		public string Callee => Kind == XedBlotValueKind.CallResult
			? str : throw new InvalidOperationException();

		public override string ToString()
		{
			if (Kind == XedBlotValueKind.Constant) return constant.ToString(CultureInfo.InvariantCulture);
			if (Kind == XedBlotValueKind.Bits) return XedBitPattern.Prettify(str);
			if (Kind == XedBlotValueKind.CallResult) return str + "()";
			throw new UnreachableException();
		}

		public bool Equals(XedBlotValue other) => str == other.str
			&& constant == other.constant && Kind == other.Kind;
		public override bool Equals(object obj) => obj is XedBlotValue && Equals((XedBlotValue)obj);
		public override int GetHashCode() => (str?.GetHashCode()).GetValueOrDefault()
			^ constant ^ (Kind.GetHashCode() << 17);
		public static bool Equals(XedBlotValue lhs, XedBlotValue rhs) => lhs.Equals(rhs);
		public static bool operator ==(XedBlotValue lhs, XedBlotValue rhs) => Equals(lhs, rhs);
		public static bool operator !=(XedBlotValue lhs, XedBlotValue rhs) => !Equals(lhs, rhs);

		public static XedBlotValue MakeBits(string pattern)
			=> new XedBlotValue(XedBlotValueKind.Bits, XedBitPattern.Normalize(pattern));

		public static XedBlotValue MakeCallResult(string callee)
			=> new XedBlotValue(XedBlotValueKind.CallResult, callee ?? throw new ArgumentNullException(nameof(callee)));

		public static XedBlotValue MakeConstant(ushort value)
			=> new XedBlotValue(XedBlotValueKind.Constant, null, value);
	}

	public readonly partial struct XedBlot : IEquatable<XedBlot>
	{
		public XedField Field { get; }
		private readonly string str; // bitPattern or callee
		private readonly ushort constantOrIsCallResult;
		public XedBlotType Type { get; }

		private XedBlot(XedBlotType type, XedField field, string bitPattern, XedBlotValue value = default)
		{
			this.Type = type;
			this.Field = field;
			this.str = bitPattern;
			this.constantOrIsCallResult = 0;

			if (value.Kind == XedBlotValueKind.Constant)
				this.constantOrIsCallResult = value.Constant;
			else if (value.Kind == XedBlotValueKind.Bits)
				this.str = value.BitPattern;
			else if (value.Kind == XedBlotValueKind.CallResult)
			{
				this.str = value.Callee;
				this.constantOrIsCallResult = 1;
			}
			else throw new UnreachableException();
		}

		public string BitPattern => Type == XedBlotType.Bits
			? str : throw new InvalidOperationException();

		public XedBlotValue Value
		{
			get
			{
				if (Type == XedBlotType.Bits || Type == XedBlotType.Call)
					throw new InvalidOperationException();
				if (str == null) return XedBlotValue.MakeConstant(constantOrIsCallResult);
				return constantOrIsCallResult == 0
					? XedBlotValue.MakeBits(str) : XedBlotValue.MakeCallResult(str);
			}
		}

		public string Callee => Type == XedBlotType.Call
			? str : throw new InvalidOperationException();

		public override string ToString()
		{
			if (Type == XedBlotType.Bits)
			{
				if (Field == null) return str;
				return Field.Name + "[" + XedBitPattern.Prettify(str) + "]";
			}
			if (Type == XedBlotType.Call) return str + "()";

			return Field.Name + (Type == XedBlotType.Equality ? "=" : "!=") + Value.ToString();
		}

		public bool Equals(XedBlot other) => Field == other.Field
			&& Type == other.Type && str == other.str
			&& constantOrIsCallResult == other.constantOrIsCallResult;
		public override bool Equals(object obj) => obj is XedBlot && Equals((XedBlot)obj);
		public override int GetHashCode() => (Type.GetHashCode() << 17)
			^ (Field?.GetHashCode()).GetValueOrDefault()
			^ (str?.GetHashCode()).GetValueOrDefault();
		public static bool Equals(XedBlot lhs, XedBlot rhs) => lhs.Equals(rhs);
		public static bool operator ==(XedBlot lhs, XedBlot rhs) => Equals(lhs, rhs);
		public static bool operator !=(XedBlot lhs, XedBlot rhs) => !Equals(lhs, rhs);

		public static XedBlot MakeBits(XedField field, string pattern)
			=> new XedBlot(XedBlotType.Bits, field,
				XedBitPattern.Normalize(pattern ?? throw new ArgumentNullException(nameof(pattern))));

		public static XedBlot MakeBits(string pattern) => MakeBits(null, pattern);

		public static XedBlot MakeEquality(XedField field, XedBlotValue value)
			=> new XedBlot(XedBlotType.Equality, field ?? throw new ArgumentNullException(nameof(field)),
				bitPattern: null, value);

		public static XedBlot MakeEquality(XedField field, ushort value)
			=> MakeEquality(field, XedBlotValue.MakeConstant(value));

		public static XedBlot MakeInequality(XedField field, XedBlotValue value)
			=> new XedBlot(XedBlotType.Inequality, field ?? throw new ArgumentNullException(nameof(field)),
				bitPattern: null, value);

		public static XedBlot MakeInequality(XedField field, ushort value)
			=> MakeInequality(field, XedBlotValue.MakeConstant(value));

		public static XedBlot MakeCall(string callee)
			=> new XedBlot(XedBlotType.Call, field: null,
				callee ?? throw new ArgumentNullException(nameof(callee)));
	}
}
