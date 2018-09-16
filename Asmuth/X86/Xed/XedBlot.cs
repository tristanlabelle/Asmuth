using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Asmuth.X86.Xed
{
	public enum XedBlotType : byte
	{
		Bits,
		Predicate,
		Assignment,
		Call
	}
	
	/// <summary>
	/// A XED pattern blot which consumes or produces instruction bits.
	/// </summary>
	public readonly struct XedBitsBlot : IEquatable<XedBitsBlot>
	{
		// 0b0100
		// wrxb
		// SIBINDEX[0b000]
		// SIBBASE[bbb]
		// UIMM0[ssss_uuuu]
		// UIMM0[i/32]

		public string Field { get; }
		public string Pattern { get; }
		
		public XedBitsBlot(string field, string pattern)
		{
			this.Field = field;
			this.Pattern = XedBitPattern.Normalize(pattern);
		}

		public XedBitsBlot(string field, char c)
		{
			if ((c < 'a' || c > 'z') && c != '0' && c != '1')
				throw new ArgumentOutOfRangeException(nameof(c));
			this.Field = field;
			this.Pattern = c.ToString();
		}
		
		public XedBitsBlot(string field, byte value, int bitCount)
		{
			if (bitCount < 1 || bitCount > 8) throw new ArgumentOutOfRangeException(nameof(bitCount));

			this.Field = field;

			char[] bits = new char[bitCount];
			for (int i = 0; i < bitCount; ++i)
				bits[i] = (char)('0' + (value >> (bitCount - 1 - i) & 1));

			this.Pattern = new string(bits);
		}

		public XedBitsBlot(string pattern) : this(null, pattern) { }
		public XedBitsBlot(char c) : this(null, c) { }
		public XedBitsBlot(byte value, int bitCount) : this(null, value, bitCount) { }

		public int BitCount => Pattern.Length;

		public byte? ConstantValue
		{
			get
			{
				if (Pattern.Length > 8) return null;
				byte value = 0;
				foreach (var c in Pattern)
				{
					if (c != '0' && c != '1')
						return null;
					value = (byte)((value << 1) | (c - '0'));
				}
				return value;
			}
		}

		public bool Equals(XedBitsBlot other) => Field == other.Field && Pattern == other.Pattern;
		public override bool Equals(object obj) => obj is XedBitsBlot && Equals((XedBitsBlot)obj);
		public override int GetHashCode() => throw new NotImplementedException();
		public static bool Equals(XedBitsBlot lhs, XedBitsBlot rhs) => lhs.Equals(rhs);
		public static bool operator ==(XedBitsBlot lhs, XedBitsBlot rhs) => Equals(lhs, rhs);
		public static bool operator !=(XedBitsBlot lhs, XedBitsBlot rhs) => !Equals(lhs, rhs);

		public override string ToString()
		{
			var str = new StringBuilder();
			if (Field != null) str.Append(Field).Append('[');
			if (ConstantValue.HasValue) str.Append("0b");
			str.Append(Pattern);
			if (Field != null) str.Append(']');
			return str.ToString();
		}

		public static implicit operator XedBitsBlot(char c) => new XedBitsBlot(c);
	}

	/// <summary>
	/// A XED pattern blot which compares a field against a constant value.
	/// </summary>
	public readonly struct XedPredicateBlot : IEquatable<XedPredicateBlot>
	{
		public string Field { get; }
		public bool IsNotEqual { get; }
		public byte Value { get; }

		public XedPredicateBlot(string field, bool equal, byte value)
		{
			this.Field = field ?? throw new ArgumentNullException(nameof(field));
			this.IsNotEqual = !equal;
			this.Value = value;
		}

		public bool IsEqual => IsNotEqual;

		public bool Equals(XedPredicateBlot other) => Field == other.Field
			&& IsNotEqual == other.IsNotEqual && Value == other.Value;
		public override bool Equals(object obj) => obj is XedPredicateBlot && Equals((XedPredicateBlot)obj);
		public override int GetHashCode() => Field.GetHashCode() ^ Value;
		public static bool Equals(XedPredicateBlot lhs, XedPredicateBlot rhs) => lhs.Equals(rhs);
		public static bool operator ==(XedPredicateBlot lhs, XedPredicateBlot rhs) => Equals(lhs, rhs);
		public static bool operator !=(XedPredicateBlot lhs, XedPredicateBlot rhs) => !Equals(lhs, rhs);

		public override string ToString() => Field + (IsEqual ? "=" : "!=") + Value;

		public static XedPredicateBlot Equal(string field, byte value)
			=> new XedPredicateBlot(field, equal: true, value);

		public static XedPredicateBlot NotEqual(string field, byte value)
			=> new XedPredicateBlot(field, equal: false, value);
	}

	public enum XedAssignmentBlotValueType : byte
	{
		Integer,
		NamedConstant,
		BitPattern,
		Call
	}

	/// <summary>
	/// A XED pattern blot that assigns a value to a field.
	/// </summary>
	public readonly struct XedAssignmentBlot : IEquatable<XedAssignmentBlot>
	{
		public string Field { get; }
		private readonly string valueString;
		public XedAssignmentBlotValueType ValueType { get; }
		private readonly byte value;

		public XedAssignmentBlot(string field, int value)
		{
			this.Field = field ?? throw new ArgumentNullException(nameof(field));
			this.valueString = null;
			this.ValueType = XedAssignmentBlotValueType.Integer;
			this.value = checked((byte)value);
		}

		private XedAssignmentBlot(string field, string value, XedAssignmentBlotValueType valueType)
		{
			this.Field = field ?? throw new ArgumentNullException(nameof(field));
			this.valueString = value ?? throw new ArgumentNullException(nameof(value));
			this.ValueType = valueType;
			this.value = 0;
		}

		public int Integer => ValueType == XedAssignmentBlotValueType.Integer
			? value : throw new InvalidOperationException();
		public int BitCount => ValueType == XedAssignmentBlotValueType.BitPattern
			? valueString.Length : throw new InvalidOperationException();
		public string Callee => ValueType == XedAssignmentBlotValueType.Call
			? valueString : throw new InvalidOperationException();
		public string ConstantName => ValueType == XedAssignmentBlotValueType.NamedConstant
			? valueString : throw new InvalidOperationException();
		public string BitsPattern => ValueType == XedAssignmentBlotValueType.BitPattern
			? valueString : throw new InvalidOperationException();

		public bool Equals(XedAssignmentBlot other) => Field == other.Field
			&& valueString == other.valueString && ValueType == other.ValueType
			&& value == other.value;
		public override bool Equals(object obj) => obj is XedAssignmentBlot && Equals((XedAssignmentBlot)obj);
		public override int GetHashCode() => Field.GetHashCode() ^ ValueType.GetHashCode();
		public static bool Equals(XedAssignmentBlot lhs, XedAssignmentBlot rhs) => lhs.Equals(rhs);
		public static bool operator ==(XedAssignmentBlot lhs, XedAssignmentBlot rhs) => Equals(lhs, rhs);
		public static bool operator !=(XedAssignmentBlot lhs, XedAssignmentBlot rhs) => !Equals(lhs, rhs);

		public override string ToString()
		{
			var str = new StringBuilder();
			str.Append(Field);
			str.Append('=');
			switch (ValueType)
			{
				case XedAssignmentBlotValueType.Integer:
					str.Append(Integer);
					break;

				case XedAssignmentBlotValueType.NamedConstant:
					str.Append(valueString);
					break;

				case XedAssignmentBlotValueType.BitPattern:
					str.Append(BitsPattern);
					break;

				case XedAssignmentBlotValueType.Call:
					str.Append(valueString).Append("()");
					break;

				default: throw new UnreachableException();
			}

			return str.ToString();
		}

		public static XedAssignmentBlot Call(string field, string callee)
			=> new XedAssignmentBlot(field, callee, XedAssignmentBlotValueType.Call);

		public static XedAssignmentBlot NamedConstant(string field, string constantName)
		{
			if (!constantName.StartsWith("XED_")) throw new ArgumentException();
			return new XedAssignmentBlot(field, constantName, XedAssignmentBlotValueType.NamedConstant);
		}

		public static XedAssignmentBlot BitPattern(string field, string pattern)
		{
			return new XedAssignmentBlot(field, XedBitPattern.Normalize(pattern),
				XedAssignmentBlotValueType.BitPattern);
		}
	}

	public readonly partial struct XedBlot : IEquatable<XedBlot>
	{
		[StructLayout(LayoutKind.Explicit, Size = 4)]
		private struct Union
		{
			[FieldOffset(0)] public XedBlotType type;
			[FieldOffset(1)] public bool predicate_notEqual;
			[FieldOffset(2)] public byte predicate_value;
			[FieldOffset(1)] public XedAssignmentBlotValueType assignment_valueType;
			[FieldOffset(2)] public byte assignment_value;
			[FieldOffset(0)] public int raw;
		}

		public string Field { get; }
		private readonly string valueStr;
		private readonly Union union;

		public XedBlot(in XedBitsBlot bits)
		{
			this.Field = bits.Field;
			this.valueStr = bits.Pattern;
			this.union = new Union { type = XedBlotType.Bits };
		}

		public XedBlot(in XedPredicateBlot predicate)
		{
			this.Field = predicate.Field;
			this.valueStr = null;
			this.union = new Union
			{
				type = XedBlotType.Predicate,
				predicate_notEqual = predicate.IsNotEqual,
				predicate_value = predicate.Value
			};
		}

		public XedBlot(in XedAssignmentBlot assignment)
		{
			this.Field = assignment.Field;
			this.union = new Union
			{
				type = XedBlotType.Assignment,
				assignment_valueType = assignment.ValueType
			};

			switch (assignment.ValueType)
			{
				case XedAssignmentBlotValueType.Integer:
					this.valueStr = null;
					this.union.assignment_value = (byte)assignment.Integer;
					break;

				case XedAssignmentBlotValueType.NamedConstant:
					this.valueStr = assignment.ConstantName;
					break;

				case XedAssignmentBlotValueType.BitPattern:
					this.valueStr = assignment.BitsPattern;
					break;
					
				case XedAssignmentBlotValueType.Call:
					this.valueStr = assignment.Callee;
					break;

				default: throw new UnreachableException();
			}
		}

		private XedBlot(string callee)
		{
			this.Field = null;
			this.valueStr = callee ?? throw new ArgumentNullException(nameof(callee));
			this.union = new Union { type = XedBlotType.Call };
		}

		public XedBlotType Type => union.type;

		public XedBitsBlot Bits
		{
			get
			{
				if (Type != XedBlotType.Bits) throw new InvalidOperationException();
				return new XedBitsBlot(Field, valueStr);
			}
		}

		public XedPredicateBlot Predicate
		{
			get
			{
				if (Type != XedBlotType.Predicate) throw new InvalidOperationException();
				return new XedPredicateBlot(Field, !union.predicate_notEqual, union.predicate_value);
			}
		}

		public XedAssignmentBlot Assignment
		{
			get
			{
				if (Type != XedBlotType.Assignment) throw new InvalidOperationException();
				switch (union.assignment_valueType)
				{
					case XedAssignmentBlotValueType.Integer:
						return new XedAssignmentBlot(Field, union.assignment_value);

					case XedAssignmentBlotValueType.NamedConstant:
						return XedAssignmentBlot.NamedConstant(Field, valueStr);

					case XedAssignmentBlotValueType.BitPattern:
						return XedAssignmentBlot.BitPattern(Field, valueStr);

					case XedAssignmentBlotValueType.Call:
						return XedAssignmentBlot.Call(Field, valueStr);

					default: throw new UnreachableException();
				}
			}
		}

		public string Callee => Type == XedBlotType.Call ? valueStr
			: throw new InvalidOleVariantTypeException();

		public bool Equals(XedBlot other)
		{
			if (Type != other.Type) return false;
			switch (Type)
			{
				case XedBlotType.Bits: return Bits == other.Bits;
				case XedBlotType.Predicate: return Predicate == other.Predicate;
				case XedBlotType.Assignment: return Assignment == other.Assignment;
				case XedBlotType.Call: return Callee == other.Callee;
				default: throw new UnreachableException();
			}
		}

		public override bool Equals(object obj) => obj is XedBlot && Equals((XedBlot)obj);
		public override int GetHashCode() => (Field?.GetHashCode()).GetValueOrDefault() ^ union.raw;
		public static bool Equals(XedBlot lhs, XedBlot rhs) => lhs.Equals(rhs);
		public static bool operator ==(XedBlot lhs, XedBlot rhs) => Equals(lhs, rhs);
		public static bool operator !=(XedBlot lhs, XedBlot rhs) => !Equals(lhs, rhs);

		public override string ToString()
		{
			switch (Type)
			{
				case XedBlotType.Bits: return Bits.ToString();
				case XedBlotType.Predicate: return Predicate.ToString();
				case XedBlotType.Assignment: return Assignment.ToString();
				case XedBlotType.Call: return Callee + "()";
				default: throw new UnreachableException();
			}
		}

		public static XedBlot Call(string callee) => new XedBlot(callee);

		public static implicit operator XedBlot(in XedBitsBlot bits) => new XedBlot(bits);
		public static implicit operator XedBlot(in XedPredicateBlot predicate) => new XedBlot(predicate);
		public static implicit operator XedBlot(in XedAssignmentBlot assignment) => new XedBlot(assignment);
	}
}
