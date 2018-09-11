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

	public readonly struct XedBitsBlotVariable : IEquatable<XedBitsBlotVariable>
	{
		private readonly byte letterIndex;
		private readonly byte bitCountMinusOne;

		public XedBitsBlotVariable(char letter, int bitCount)
		{
			if (letter < 'a' || letter > 'z') throw new ArgumentOutOfRangeException(nameof(letter));
			if (bitCount < 1 || bitCount > 64) throw new ArgumentOutOfRangeException(nameof(bitCount));
			this.letterIndex = (byte)(letter - 'a');
			this.bitCountMinusOne = (byte)(bitCount - 1);
		}

		public char Letter => (char)('a' + letterIndex);
		public int BitCount => bitCountMinusOne + 1;

		public bool Equals(XedBitsBlotVariable other) => letterIndex == other.letterIndex
			&& bitCountMinusOne == other.bitCountMinusOne;
		public override bool Equals(object obj) => obj is XedBitsBlotVariable && Equals((XedBitsBlotVariable)obj);
		public override int GetHashCode() => ((int)letterIndex << 8) ^ bitCountMinusOne;
		public static bool Equals(XedBitsBlotVariable lhs, XedBitsBlotVariable rhs) => lhs.Equals(rhs);
		public static bool operator ==(XedBitsBlotVariable lhs, XedBitsBlotVariable rhs) => Equals(lhs, rhs);
		public static bool operator !=(XedBitsBlotVariable lhs, XedBitsBlotVariable rhs) => !Equals(lhs, rhs);
		
		public override string ToString()
			=> BitCount < 8 ? new string(Letter, BitCount) : $"{Letter}/{BitCount}";

		public static ImmutableArray<XedBitsBlotVariable> Parse(string str)
		{
			if (str == null) throw new ArgumentNullException(nameof(str));
			var builder = ImmutableArray.CreateBuilder<XedBitsBlotVariable>(1);
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
		private readonly ImmutableArray<XedBitsBlotVariable> variables;
		private readonly byte value;
		private readonly byte totalBitCountMinusOne;

		public XedBitsBlot(string field, ImmutableArray<XedBitsBlotVariable> variables)
		{
			this.Field = field;
			this.variables = variables.Length > 0 ? variables : throw new ArgumentException();
			this.value = 0;

			int totalBitCount = 0;
			foreach (var variable in variables)
				totalBitCount += variable.BitCount;
			this.totalBitCountMinusOne = (byte)(totalBitCount - 1);
		}

		public XedBitsBlot(string field, XedBitsBlotVariable variable)
		{
			this.Field = field;
			this.variables = ImmutableArray.Create(variable);
			this.value = 0;
			this.totalBitCountMinusOne = (byte)(variable.BitCount - 1);
		}

		public XedBitsBlot(string field, byte value, int bitCount)
		{
			if (bitCount < 1 || bitCount > 8) throw new ArgumentOutOfRangeException(nameof(bitCount));
			if ((value & ~((1 << bitCount) - 1)) != 0) throw new ArgumentOutOfRangeException(nameof(value));
			this.Field = field;
			this.variables = default;
			this.value = value;
			this.totalBitCountMinusOne = (byte)(bitCount - 1);
		}

		public XedBitsBlot(byte value)
		{
			this.Field = null;
			this.variables = default;
			this.value = value;
			this.totalBitCountMinusOne = 7;
		}

		public bool IsConstant => variables.IsDefault;
		public bool IsVariable => !variables.IsDefault;
		public int TotalBitCount => totalBitCountMinusOne + 1;

		public ImmutableArray<XedBitsBlotVariable> Variables => IsVariable
			? variables : throw new InvalidOperationException();
		public byte Value => IsConstant ? value : throw new InvalidOperationException();
		
		public bool Equals(XedBitsBlot other) => throw new NotImplementedException();
		public override bool Equals(object obj) => obj is XedBitsBlot && Equals((XedBitsBlot)obj);
		public override int GetHashCode() => (Field?.GetHashCode()).GetValueOrDefault()
			^ ((int)value << 16) ^ totalBitCountMinusOne;
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
			[FieldOffset(1)] public bool bits_isVariable;
			[FieldOffset(2)] public byte bits_variableCharOrValue;
			[FieldOffset(3)] public byte bits_bitCountMinusOne;
			[FieldOffset(1)] public bool predicate_notEqual;
			[FieldOffset(2)] public byte predicate_value;
			[FieldOffset(1)] public byte assignment_value;
			[FieldOffset(0)] public int raw;
		}

		public string Field { get; }
		private readonly string callee;
		private readonly Union union;
		
		public XedBlot(in XedBitsBlot bits) => throw new NotImplementedException();
		public XedBlot(in XedPredicateBlot predicate) => throw new NotImplementedException();
		public XedBlot(in XedAssignmentBlot assignment) => throw new NotImplementedException();

		private XedBlot(string callee)
		{
			this.Field = null;
			this.callee = callee ?? throw new ArgumentNullException(nameof(callee));
			this.union = new Union { type = XedBlotType.Call };
		}

		public XedBlotType Type => union.type;
		public XedBitsBlot Bits => throw new NotImplementedException();
		public XedPredicateBlot Predicate => throw new NotImplementedException();
		public XedAssignmentBlot Assignment => throw new NotImplementedException();

		public bool Equals(XedBlot other) => Field == other.Field
			&& callee == other.callee && union.raw == other.union.raw;
		public override bool Equals(object obj) => obj is XedBlot && Equals((XedBlot)obj);
		public override int GetHashCode() => (Field?.GetHashCode()).GetValueOrDefault()
			^ union.raw;
		public static bool Equals(XedBlot lhs, XedBlot rhs) => lhs.Equals(rhs);
		public static bool operator ==(XedBlot lhs, XedBlot rhs) => Equals(lhs, rhs);
		public static bool operator !=(XedBlot lhs, XedBlot rhs) => !Equals(lhs, rhs);

		public static XedBlot MakeCall(string callee) => new XedBlot(callee);

		private static readonly Regex parseRegex = new Regex(
			@"^\s*(
				(?<byte>0x[0-9a-f]{2})
				| (?<field>[a-z][a-z0-9]*)(
					(?<ne>!)?=(?<value>\d+)
					| \[(?<value>0b[01]+)\]
					| \[(?<bitsvar>(?<l>[a-z])\k<l>*)\]
					| \[(?<bitsvar>[a-z])/(?<bitcount>\d+)\])
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
