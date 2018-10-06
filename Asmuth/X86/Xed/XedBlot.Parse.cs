using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Asmuth.X86.Xed
{
	partial struct XedBlot
	{
		private static readonly Regex highLevelRegex = new Regex(
			@"^(
				(?<field>[A-Z][A-Z0-9_]*)
				(\[ (?<bits>[^\]]+) \])?
				(
					(?<op>\!?=)
					(
						(?<val10>[0-9]+)
						| 0b(?<val2>[01]+)
						| 0x(?<val16>[0-9a-fA-F]+)
						| (?<vale>XED_[A-Z0-9_]+|@)
						| (?<valb>[a-z01_]+)
						| (?<valc>[\w_]+)\(\)
					)
				)?
				| (?<callee>[\w_]+)\(\)
				| (?<bits>.+)
			)$", RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

		private static readonly Regex bitsRegex = new Regex(
			@"^(
				0b(?<base2>[01]+)
				| 0x(?<base16>[0-9a-fA-F]+)
				| (?<letter>[a-z])/(?<length>\d+)
				| (?<pattern>[01a-z_]+)
			)$", RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

		public static (XedBlot, XedBlot?) Parse(string str, Func<string, XedField> fieldResolver)
		{
			var highLevelMatch = highLevelRegex.Match(str);
			if (!highLevelMatch.Success) throw new FormatException();

			XedBlot blot;
			XedBlot? secondBlot = null;
			if (highLevelMatch.Groups["field"].Success)
			{
				var field = fieldResolver(highLevelMatch.Groups["field"].Value);

				XedBlot? bitsBlot = null;
				if (highLevelMatch.Groups["bits"].Success)
				{
					var bitPattern = ParseBits(highLevelMatch.Groups["bits"].Value);
					bitsBlot = MakeBits(field, bitPattern);
				}

				XedBlot? predicateBlot = null;
				if (highLevelMatch.Groups["op"].Success)
				{
					XedBlotValue value;
					if (highLevelMatch.Groups["val10"].Success)
						value = XedBlotValue.MakeConstant(Convert.ToUInt16(
							highLevelMatch.Groups["val10"].Value, 10));
					else if (highLevelMatch.Groups["val2"].Success)
						value = XedBlotValue.MakeConstant(Convert.ToByte(
							highLevelMatch.Groups["val2"].Value, 2));
					else if (highLevelMatch.Groups["val16"].Success)
						value = XedBlotValue.MakeConstant(Convert.ToByte(
							highLevelMatch.Groups["val16"].Value, 16));
					else if (highLevelMatch.Groups["vale"].Success)
					{
						var enumType = (XedEnumFieldType)field.Type;
						int enumerant = enumType.GetValue(highLevelMatch.Groups["vale"].Value);
						value = XedBlotValue.MakeConstant(checked((ushort)enumerant));
					}
					else if (highLevelMatch.Groups["valb"].Success)
						value = XedBlotValue.MakeBits(highLevelMatch.Groups["valb"].Value);
					else if (highLevelMatch.Groups["valc"].Success)
						value = XedBlotValue.MakeCallResult(highLevelMatch.Groups["valc"].Value);
					else
						throw new UnreachableException();

					predicateBlot = highLevelMatch.Groups["op"].Value[0] == '!'
						? MakeInequality(field, value) : MakeEquality(field, value);
				}

				if (bitsBlot.HasValue && predicateBlot.HasValue)
				{
					blot = bitsBlot.Value;
					secondBlot = predicateBlot;
				}
				else if (bitsBlot.HasValue) blot = bitsBlot.Value;
				else if (predicateBlot.HasValue) blot = predicateBlot.Value;
				else throw new FormatException();
			}
			else if (highLevelMatch.Groups["callee"].Success)
			{
				blot = MakeCall(highLevelMatch.Groups["callee"].Value);
			}
			else if (highLevelMatch.Groups["bits"].Success)
			{
				blot = MakeBits(field: null, ParseBits(highLevelMatch.Groups["bits"].Value));
			}
			else throw new UnreachableException();
			
			return (blot, secondBlot);
		}

		private static string ParseBits(string str)
		{
			var match = bitsRegex.Match(str);
			if (!match.Success) throw new FormatException();

			if (match.Groups["base2"].Success)
				return match.Groups["base2"].Value;

			if (match.Groups["base16"].Success)
			{
				var hexDigits = match.Groups["base16"].Value;
				return Convert.ToString(Convert.ToInt64(hexDigits, 16), 2)
					.PadLeft(hexDigits.Length * 4, '0');
			}

			if (match.Groups["letter"].Success)
			{
				int length = int.Parse(match.Groups["length"].Value, CultureInfo.InvariantCulture);
				return new string(match.Groups["letter"].Value[0], length);
			}

			return XedBitPattern.Normalize(match.Groups["pattern"].Value);
		}
	}
}
