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

	[StructLayout(LayoutKind.Sequential, Size = 2)]
	public readonly struct XedBitsBlotSpan : IEquatable<XedBitsBlotSpan>
	{
		private readonly byte valueOrLetter;
		private readonly byte isVariableAndBitCountMinusOne; // msb = isVariable
		
		public XedBitsBlotSpan(byte value)
		{
			this.valueOrLetter = value;
			this.isVariableAndBitCountMinusOne = 7;
		}

		public XedBitsBlotSpan(byte value, int bitCount)
		{
			if (bitCount < 1 || bitCount > 8) throw new ArgumentOutOfRangeException(nameof(bitCount));
			if ((value & ~((1 << bitCount) - 1)) != 0) throw new ArgumentOutOfRangeException(nameof(value));
			this.valueOrLetter = value;
			this.isVariableAndBitCountMinusOne = (byte)(bitCount - 1);
		}

		public XedBitsBlotSpan(char letter, int bitCount)
		{
			if (letter < 'a' || letter > 'z') throw new ArgumentOutOfRangeException(nameof(letter));
			if (bitCount < 1 || bitCount > 64) throw new ArgumentOutOfRangeException(nameof(bitCount));
			this.valueOrLetter = (byte)letter;
			this.isVariableAndBitCountMinusOne = (byte)(0x80 + bitCount - 1);
		}

		public XedBitsBlotSpan(char letter)
		{
			if (letter < 'a' || letter > 'z') throw new ArgumentOutOfRangeException(nameof(letter));
			this.valueOrLetter = (byte)letter;
			this.isVariableAndBitCountMinusOne = (byte)(0x80);
		}

		public bool IsVariable => (isVariableAndBitCountMinusOne & 0x80) != 0;
		public bool IsConstant => (isVariableAndBitCountMinusOne & 0x80) == 0;
		public char Letter => IsVariable ? (char)valueOrLetter : throw new InvalidOperationException();
		public byte Value => IsConstant ? valueOrLetter : throw new InvalidOperationException();
		public int BitCount => (isVariableAndBitCountMinusOne & 0x7F) + 1;

		public bool Equals(XedBitsBlotSpan other) => valueOrLetter == other.valueOrLetter
			&& isVariableAndBitCountMinusOne == other.isVariableAndBitCountMinusOne;
		public override bool Equals(object obj) => obj is XedBitsBlotSpan && Equals((XedBitsBlotSpan)obj);
		public override int GetHashCode() => ((int)valueOrLetter << 8) ^ isVariableAndBitCountMinusOne;
		public static bool Equals(XedBitsBlotSpan lhs, XedBitsBlotSpan rhs) => lhs.Equals(rhs);
		public static bool operator ==(XedBitsBlotSpan lhs, XedBitsBlotSpan rhs) => Equals(lhs, rhs);
		public static bool operator !=(XedBitsBlotSpan lhs, XedBitsBlotSpan rhs) => !Equals(lhs, rhs);

		public override string ToString()
		{
			if (IsConstant)
			{
				var bits = new char[BitCount];
				for (int i = 0; i < BitCount; ++i)
					bits[i] = (char)('0' + ((valueOrLetter >> (BitCount - 1 - i)) & 1));
				return new string(bits);
			}
			else
			{
				return BitCount < 8 ? new string(Letter, BitCount) : $"{Letter}/{BitCount}";
			}
		}

		public static ImmutableArray<XedBitsBlotSpan> Parse(string str)
		{
			if (str == null) throw new ArgumentNullException(nameof(str));
			var builder = ImmutableArray.CreateBuilder<XedBitsBlotSpan>(1);
			throw new NotImplementedException();
		}
		
		public static implicit operator XedBitsBlotSpan(char letter) => new XedBitsBlotSpan(letter);
		public static implicit operator XedBitsBlot(XedBitsBlotSpan span) => new XedBitsBlot(span);
		public static implicit operator XedBlot(XedBitsBlotSpan span) => new XedBitsBlot(span);
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
		public ImmutableArray<XedBitsBlotSpan> Spans { get; }
		
		public XedBitsBlot(string field, ImmutableArray<XedBitsBlotSpan> spans)
		{
			this.Field = field;
			this.Spans = spans.Length > 0 ? spans : throw new ArgumentException();
		}

		public XedBitsBlot(string field, params XedBitsBlotSpan[] spans)
			: this(field, ImmutableArray.Create(spans)) { }
		public XedBitsBlot(string field, XedBitsBlotSpan span)
			: this(field, ImmutableArray.Create(span)) { }
		public XedBitsBlot(string field, byte value, int bitCount)
			: this(field, new XedBitsBlotSpan(value, bitCount)) { }
		public XedBitsBlot(string field, char letter, int bitCount)
			: this(field, new XedBitsBlotSpan(letter, bitCount)) { }
		public XedBitsBlot(params XedBitsBlotSpan[] spans) : this(null, spans) { }
		public XedBitsBlot(ImmutableArray<XedBitsBlotSpan> spans) : this(null, spans) { }
		public XedBitsBlot(XedBitsBlotSpan span) : this(null, span) { }
		public XedBitsBlot(byte value, int bitCount) : this(null, value, bitCount) { }
		public XedBitsBlot(char letter, int bitCount) : this(null, letter, bitCount) { }
		public XedBitsBlot(byte value) : this(null, new XedBitsBlotSpan(value)) { }

		public int TotalBitCount
		{
			get
			{
				int count = 0;
				foreach (var span in Spans)
					count += span.BitCount;
				return count;
			}
		}
		
		public bool Equals(XedBitsBlot other)
		{
			if (Field != other.Field) return false;
			if (Spans.Length != other.Spans.Length) return false;
			for (int i = 0; i < Spans.Length; ++i)
				if (Spans[i] != other.Spans[i])
					return false;
			return true;
		}

		public override bool Equals(object obj) => obj is XedBitsBlot && Equals((XedBitsBlot)obj);
		public override int GetHashCode() => throw new NotImplementedException();
		public static bool Equals(XedBitsBlot lhs, XedBitsBlot rhs) => lhs.Equals(rhs);
		public static bool operator ==(XedBitsBlot lhs, XedBitsBlot rhs) => Equals(lhs, rhs);
		public static bool operator !=(XedBitsBlot lhs, XedBitsBlot rhs) => !Equals(lhs, rhs);

		public override string ToString()
		{
			var str = new StringBuilder();
			if (Field != null) str.Append(Field).Append('[');
			for (int i = 0; i < Spans.Length; ++i)
			{
				var span = Spans[i];
				if (i > 0 && Spans[i - 1].BitCount > 1 || span.BitCount > 1)
					str.Append('_');
				for (int j = 0; j < span.BitCount; ++j)
				{
					if (span.IsConstant)
					{
						int bit = (span.Value >> (span.BitCount - 1 - j)) & 1;
						str.Append((char)('0' + bit));
					}
					else str.Append(span.Letter);
				}
			}
			if (Field != null) str.Append(']');
			return str.ToString();
		}

		public static implicit operator XedBitsBlot(char letter) => new XedBitsBlotSpan(letter);
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
		BitsVariable,
		Call
	}

	/// <summary>
	/// A XED pattern blot that assigns a value to a field.
	/// </summary>
	public readonly struct XedAssignmentBlot : IEquatable<XedAssignmentBlot>
	{
		public string Field { get; }
		private readonly string rhsString;
		public XedAssignmentBlotValueType ValueType { get; }
		private readonly byte valueOrVariableLetter;
		private readonly byte bitCount; // If BitsVariable

		public XedAssignmentBlot(string field, int value)
		{
			this.Field = field ?? throw new ArgumentNullException(nameof(field));
			this.rhsString = null;
			this.ValueType = XedAssignmentBlotValueType.Integer;
			this.valueOrVariableLetter = checked((byte)value);
			this.bitCount = 0;
		}

		private XedAssignmentBlot(string field, string rhsString, bool isCall)
		{
			this.Field = field ?? throw new ArgumentNullException(nameof(field));
			this.rhsString = rhsString ?? throw new ArgumentNullException(nameof(rhsString));
			this.ValueType = isCall ? XedAssignmentBlotValueType.Call : XedAssignmentBlotValueType.NamedConstant;
			this.valueOrVariableLetter = 0;
			this.bitCount = 0;
		}
		
		public XedAssignmentBlot(string field, char bitsVariableLetter, int variableBitCount)
		{
			this.Field = field ?? throw new ArgumentNullException(nameof(field));
			if (bitsVariableLetter < 'a' || bitsVariableLetter > 'z')
				throw new ArgumentOutOfRangeException(nameof(bitsVariableLetter));
			if (variableBitCount < 1 || variableBitCount > 32)
				throw new ArgumentOutOfRangeException(nameof(bitsVariableLetter));
			this.rhsString = null;
			this.ValueType = XedAssignmentBlotValueType.BitsVariable;
			this.valueOrVariableLetter = (byte)bitsVariableLetter;
			this.bitCount = (byte)variableBitCount;
		}

		public int Integer => ValueType == XedAssignmentBlotValueType.Integer
			? valueOrVariableLetter : throw new InvalidOperationException();
		public char BitsVariableLetter => ValueType == XedAssignmentBlotValueType.BitsVariable
			? (char)valueOrVariableLetter : throw new InvalidOperationException();
		public int BitCount => ValueType == XedAssignmentBlotValueType.BitsVariable
			? (char)bitCount : throw new InvalidOperationException();
		public string Callee => ValueType == XedAssignmentBlotValueType.Call
			? rhsString : throw new InvalidOperationException();
		public string ConstantName => ValueType == XedAssignmentBlotValueType.NamedConstant
			? rhsString : throw new InvalidOperationException();

		public bool Equals(XedAssignmentBlot other) => Field == other.Field
			&& rhsString == other.rhsString && ValueType == other.ValueType
			&& valueOrVariableLetter == other.valueOrVariableLetter
			&& bitCount == other.bitCount;
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
					str.Append(rhsString);
					break;

				case XedAssignmentBlotValueType.BitsVariable:
					str.Append(new string(BitsVariableLetter, bitCount));
					break;

				case XedAssignmentBlotValueType.Call:
					str.Append(rhsString).Append("()");
					break;

				default: throw new UnreachableException();
			}

			return str.ToString();
		}

		public static XedAssignmentBlot Call(string field, string callee)
			=> new XedAssignmentBlot(field, callee, isCall: true);

		public static XedAssignmentBlot NamedConstant(string field, string constantName)
		{
			if (!constantName.StartsWith("XED_")) throw new ArgumentException();
			return new XedAssignmentBlot(field, constantName, isCall: false);
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
			[FieldOffset(2)] public byte assignment_bitsVariableLetter;
			[FieldOffset(3)] public byte assignment_bitCount;
			[FieldOffset(0)] public int raw;
		}

		public string Field { get; }
		private readonly object calleeOrSpans;
		private readonly Union union;

		public XedBlot(in XedBitsBlot bits)
		{
			this.Field = bits.Field;
			this.calleeOrSpans = bits.Spans;
			this.union = new Union { type = XedBlotType.Bits };
		}

		public XedBlot(in XedPredicateBlot predicate)
		{
			this.Field = predicate.Field;
			this.calleeOrSpans = null;
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
					this.calleeOrSpans = null;
					this.union.assignment_value = (byte)assignment.Integer;
					break;

				case XedAssignmentBlotValueType.NamedConstant:
					this.calleeOrSpans = assignment.ConstantName;
					break;

				case XedAssignmentBlotValueType.BitsVariable:
					this.calleeOrSpans = null;
					this.union.assignment_bitsVariableLetter = (byte)assignment.BitsVariableLetter;
					this.union.assignment_bitCount = (byte)assignment.BitCount;
					break;
					
				case XedAssignmentBlotValueType.Call:
					this.calleeOrSpans = assignment.Callee;
					break;

				default: throw new UnreachableException();
			}
		}

		private XedBlot(string callee)
		{
			this.Field = null;
			this.calleeOrSpans = callee ?? throw new ArgumentNullException(nameof(callee));
			this.union = new Union { type = XedBlotType.Call };
		}

		public XedBlotType Type => union.type;

		public XedBitsBlot Bits
		{
			get
			{
				if (Type != XedBlotType.Bits) throw new InvalidOperationException();
				return new XedBitsBlot(Field, (ImmutableArray<XedBitsBlotSpan>)calleeOrSpans);
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
						return XedAssignmentBlot.NamedConstant(Field, (string)calleeOrSpans);

					case XedAssignmentBlotValueType.BitsVariable:
						return new XedAssignmentBlot(Field,
							(char)union.assignment_bitsVariableLetter, union.assignment_bitCount);

					case XedAssignmentBlotValueType.Call:
						return XedAssignmentBlot.Call(Field, (string)calleeOrSpans);

					default: throw new UnreachableException();
				}
			}
		}

		public string Callee => Type == XedBlotType.Call ? (string)calleeOrSpans
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
