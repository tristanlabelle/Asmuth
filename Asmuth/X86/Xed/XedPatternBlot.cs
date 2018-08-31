using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Asmuth.X86.Xed
{
	public enum XedPatternBlotType : byte
	{
		Byte, // 0x00 - first because zero-initialization of XedPatternBlot makes the key null
		Term, // W1 (?)
		Equal, // MOD=3 or MOD[0b11]
		NotEqual, // MOD!=3
		VariableBits // MOD[mm]
		// TODO: UIMM0[i/16] 
	}

	public static class XedPatternBlotTypeEnum
	{
		public static bool IsComparison(this XedPatternBlotType type)
			=> type == XedPatternBlotType.Equal || type == XedPatternBlotType.NotEqual;
	}

	public readonly struct XedPatternBlot : IEquatable<XedPatternBlot>
	{
		public string Key { get; }
		public XedPatternBlotType Type { get; }
		private readonly byte value;
		
		private XedPatternBlot(string key, XedPatternBlotType type, byte value)
		{
			this.Key = key;
			this.Type = type;
			this.value = value;
		}

		public byte? Value => Type == XedPatternBlotType.Byte || Type.IsComparison()
			? value : (byte?)null;
		public char? Char => Type == XedPatternBlotType.VariableBits ? (char?)value : null;

		public bool Equals(XedPatternBlot other) => Key == other.Key && Type == other.Type && Value == other.Value;
		public override bool Equals(object obj) => obj is XedPatternBlot && Equals((XedPatternBlot)obj);
		public override int GetHashCode() => Key.GetHashCode() ^ (((int)Type << 16) | value);
		public static bool Equals(XedPatternBlot lhs, XedPatternBlot rhs) => lhs.Equals(rhs);
		public static bool operator ==(XedPatternBlot lhs, XedPatternBlot rhs) => Equals(lhs, rhs);
		public static bool operator !=(XedPatternBlot lhs, XedPatternBlot rhs) => !Equals(lhs, rhs);

		public override string ToString()
		{
			switch (Type)
			{
				case XedPatternBlotType.Byte: return "0x" + value.ToString("x2", CultureInfo.InvariantCulture);
				case XedPatternBlotType.Term: return Key;
				case XedPatternBlotType.Equal: return Key + "=" + value.ToString("x2", CultureInfo.InvariantCulture);
				case XedPatternBlotType.NotEqual: return Key + "!=" + value.ToString("x2", CultureInfo.InvariantCulture);
				case XedPatternBlotType.VariableBits: return Key + "[" + Char.Value + "]";
				default: throw new UnreachableException();
			}
		}

		public static XedPatternBlot Byte(byte value)
			=> new XedPatternBlot(null, XedPatternBlotType.Byte, 0x00);

		public static XedPatternBlot Term(string key, byte value)
			=> new XedPatternBlot(key, XedPatternBlotType.Term, 0);

		public static XedPatternBlot Equal(string key, byte value)
			=> new XedPatternBlot(key, XedPatternBlotType.Equal, value);

		public static XedPatternBlot NotEqual(string key, byte value)
			=> new XedPatternBlot(key, XedPatternBlotType.NotEqual, value);

		public static XedPatternBlot VariableBits(string key, char value)
			=> new XedPatternBlot(key, XedPatternBlotType.VariableBits, (byte)value);

		private static readonly Regex parseRegex = new Regex(
			@"^\s*(
				(?<byte>0x[0-9A-D]{2})
				| (?<key>\w+)(
					(?<ne>!)?=(?<value>\d+)
					| \[(?<value>0b[01]+)\]
					| \[(?<bitsvar>(?<l>[a-z])\k<l>*)\])?
			)\s*$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

		public static XedPatternBlot Parse(string str)
		{
			var match = parseRegex.Match(str);
			if (!match.Success) throw new FormatException();

			if (match.Groups["byte"].Success)
				return Byte(byte.Parse(match.Groups["byte"].Value, CultureInfo.InvariantCulture));

			var key = match.Groups["key"].Value;
			if (match.Groups["bitsvar"].Success)
				return VariableBits(key, match.Groups["bitsvar"].Value[0]);

			var value = byte.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
			return match.Groups["ne"].Success ? NotEqual(key, value) : Equal(key, value);
		}
	}
}
