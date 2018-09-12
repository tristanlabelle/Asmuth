using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
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
			this.isVariableAndBitCountMinusOne = (byte)(128 + bitCount - 1);
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
			=> BitCount < 8 ? new string(Letter, BitCount) : $"{Letter}/{BitCount}";

		public static ImmutableArray<XedBitsBlotSpan> Parse(string str)
		{
			if (str == null) throw new ArgumentNullException(nameof(str));
			var builder = ImmutableArray.CreateBuilder<XedBitsBlotSpan>(1);
			throw new NotImplementedException();
		}
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
		private readonly ImmutableArray<XedBitsBlotSpan> spans;

		public XedBitsBlot(string field, ImmutableArray<XedBitsBlotSpan> spans)
		{
			this.Field = field;
			this.spans = spans.Length > 0 ? spans : throw new ArgumentException();
		}

		public XedBitsBlot(string field, XedBitsBlotSpan span)
		{
			this.Field = field;
			this.spans = ImmutableArray.Create(span);
		}

		public XedBitsBlot(byte value)
		{
			this.Field = null;
			this.spans = ImmutableArray.Create(new XedBitsBlotSpan(value, 8));
		}

		public ImmutableArray<XedBitsBlotSpan> Spans => spans;

		public int TotalBitCount
		{
			get
			{
				int count = 0;
				foreach (var span in spans)
					count += span.BitCount;
				return count;
			}
		}
		
		public bool Equals(XedBitsBlot other) => throw new NotImplementedException();
		public override bool Equals(object obj) => obj is XedBitsBlot && Equals((XedBitsBlot)obj);
		public override int GetHashCode() => throw new NotImplementedException();
		public static bool Equals(XedBitsBlot lhs, XedBitsBlot rhs) => lhs.Equals(rhs);
		public static bool operator ==(XedBitsBlot lhs, XedBitsBlot rhs) => Equals(lhs, rhs);
		public static bool operator !=(XedBitsBlot lhs, XedBitsBlot rhs) => !Equals(lhs, rhs);
	}

	/// <summary>
	/// A XED pattern blot which compares a field against a constant value.
	/// </summary>
	public readonly struct XedPredicateBlot : IEquatable<XedPredicateBlot>
	{
		public string Field { get; }
		public bool NotEqual { get; }
		public byte Value { get; }

		public XedPredicateBlot(string field, bool equal, byte value)
		{
			this.Field = field ?? throw new ArgumentNullException(nameof(field));
			this.NotEqual = !equal;
			this.Value = value;
		}

		public bool Equal => NotEqual;

		public bool Equals(XedPredicateBlot other) => Field == other.Field
			&& NotEqual == other.NotEqual && Value == other.Value;
		public override bool Equals(object obj) => obj is XedPredicateBlot && Equals((XedPredicateBlot)obj);
		public override int GetHashCode() => Field.GetHashCode() ^ Value;
		public static bool Equals(XedPredicateBlot lhs, XedPredicateBlot rhs) => lhs.Equals(rhs);
		public static bool operator ==(XedPredicateBlot lhs, XedPredicateBlot rhs) => Equals(lhs, rhs);
		public static bool operator !=(XedPredicateBlot lhs, XedPredicateBlot rhs) => !Equals(lhs, rhs);
	}

	/// <summary>
	/// A XED pattern blot that assigns a value to a field.
	/// </summary>
	public readonly struct XedAssignmentBlot : IEquatable<XedAssignmentBlot>
	{
		public string Field { get; }
		private readonly string callee;
		private readonly byte value;

		public XedAssignmentBlot(string field, int value)
		{
			this.Field = field ?? throw new ArgumentNullException(nameof(field));
			this.callee = null;
			this.value = checked((byte)value);
		}

		public XedAssignmentBlot(string field, string callee)
		{
			this.Field = field ?? throw new ArgumentNullException(nameof(field));
			this.callee = callee ?? throw new ArgumentNullException(nameof(callee));
			this.value = 0;
		}

		public bool IsCall => callee != null;
		public bool IsConstant => callee == null;
		public string Callee => callee ?? throw new InvalidOperationException();
		public int Value => IsConstant ? value : throw new InvalidOperationException();

		public bool Equals(XedAssignmentBlot other) => Field == other.Field
			&& callee == other.callee && value == other.value;
		public override bool Equals(object obj) => obj is XedAssignmentBlot && Equals((XedAssignmentBlot)obj);
		public override int GetHashCode() => Field.GetHashCode() ^ (IsCall ? callee.GetHashCode() : value);
		public static bool Equals(XedAssignmentBlot lhs, XedAssignmentBlot rhs) => lhs.Equals(rhs);
		public static bool operator ==(XedAssignmentBlot lhs, XedAssignmentBlot rhs) => Equals(lhs, rhs);
		public static bool operator !=(XedAssignmentBlot lhs, XedAssignmentBlot rhs) => !Equals(lhs, rhs);
	}

	public readonly struct XedBlot : IEquatable<XedBlot>
	{
		[StructLayout(LayoutKind.Explicit, Size = 4)]
		private struct Union
		{
			[FieldOffset(0)] public XedBlotType type;
			[FieldOffset(1)] public bool predicate_notEqual;
			[FieldOffset(2)] public byte predicate_value;
			[FieldOffset(1)] public byte assignment_value;
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
				predicate_notEqual = predicate.NotEqual,
				predicate_value = predicate.Value
			};
		}

		public XedBlot(in XedAssignmentBlot assignment)
		{
			this.Field = assignment.Field;
			this.union = new Union { type = XedBlotType.Assignment };
			if (assignment.IsConstant)
			{
				this.calleeOrSpans = null;
				this.union.assignment_value = (byte)assignment.Value;
			}
			else
			{
				this.calleeOrSpans = assignment.Callee;
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
				return calleeOrSpans == null
					? new XedAssignmentBlot(Field, (string)calleeOrSpans)
					: new XedAssignmentBlot(Field, union.assignment_value);
			}
		}

		public string Callee => Type == XedBlotType.Call ? (string)calleeOrSpans
			: throw new InvalidOleVariantTypeException();

		public bool Equals(XedBlot other) => throw new NotImplementedException();
		public override bool Equals(object obj) => obj is XedBlot && Equals((XedBlot)obj);
		public override int GetHashCode() => (Field?.GetHashCode()).GetValueOrDefault() ^ union.raw;
		public static bool Equals(XedBlot lhs, XedBlot rhs) => lhs.Equals(rhs);
		public static bool operator ==(XedBlot lhs, XedBlot rhs) => Equals(lhs, rhs);
		public static bool operator !=(XedBlot lhs, XedBlot rhs) => !Equals(lhs, rhs);

		public static XedBlot MakeCall(string callee) => new XedBlot(callee);

		private static readonly Regex parseRegex = new Regex(
			@"^\s*(
				(?<bits>[a-z0-9_]+)
				| (?<field>[a-z][a-z0-9]*)(
					(?<ne>!)?=(?<bits>[0-9]+)
					| \[(?<bits>[a-z0-9_]+)\])
				| (?<callee>[a-z][a-z0-9]*)\s*\(\s*\)
			)\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

		public static XedBlot Parse(string str, bool predicate)
		{
			var match = parseRegex.Match(str);
			if (!match.Success) throw new FormatException();

			if (match.Groups["byte"].Success)
				return new XedBitsBlot(byte.Parse(match.Groups["byte"].Value, CultureInfo.InvariantCulture));

			var field = match.Groups["field"].Value;
			if (match.Groups["bitsvar"].Success)
			{
				var name = match.Groups["bitsvar"].Value;
				throw new NotImplementedException();
			}

			var value = byte.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
			throw new NotImplementedException();
		}

		public static implicit operator XedBlot(in XedBitsBlot bits) => new XedBlot(bits);
		public static implicit operator XedBlot(in XedPredicateBlot predicate) => new XedBlot(predicate);
		public static implicit operator XedBlot(in XedAssignmentBlot assignment) => new XedBlot(assignment);
	}
}
