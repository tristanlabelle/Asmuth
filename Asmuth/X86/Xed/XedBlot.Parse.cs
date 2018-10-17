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
						| 0b(?<val2>[01][01_]*)
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
				0b(?<base2>[01][01_]*)
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
			if (highLevelMatch.Groups.TryGetValue("field", out var fieldStr))
			{
				var field = fieldResolver(fieldStr);

				XedBlot? bitsBlot = null;
				if (highLevelMatch.Groups.TryGetValue("bits", out var bitsStr))
				{
					var bitPattern = ParseBits(bitsStr);
					bitsBlot = MakeBits(field, bitPattern);
				}

				XedBlot? predicateBlot = null;
				if (highLevelMatch.Groups.TryGetValue("op", out var opStr))
				{
					XedBlotValue value;
					if (highLevelMatch.Groups.TryGetValue("val10", out var val10Str))
						value = XedBlotValue.MakeConstant(Convert.ToUInt16(val10Str, 10));
					else if (highLevelMatch.Groups.TryGetValue("val2", out var val2Str))
						value = XedBlotValue.MakeConstant(Convert.ToByte(val2Str.Replace("_", ""), 2));
					else if (highLevelMatch.Groups.TryGetValue("val16", out var val16Str))
						value = XedBlotValue.MakeConstant(Convert.ToByte(val16Str, 16));
					else if (highLevelMatch.Groups.TryGetValue("vale", out var enumValueStr))
					{
						var enumType = (XedEnumFieldType)field.Type;
						int enumerant = enumType.GetValue(enumValueStr);
						value = XedBlotValue.MakeConstant(checked((ushort)enumerant));
					}
					else if (highLevelMatch.Groups.TryGetValue("valb", out var bitPatternValueStr))
						value = XedBlotValue.MakeBits(bitPatternValueStr);
					else if (highLevelMatch.Groups.TryGetValue("valc", out var calleeValueStr))
						value = XedBlotValue.MakeCallResult(calleeValueStr);
					else
						throw new UnreachableException();

					predicateBlot = opStr[0] == '!'
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

			if (match.Groups.TryGetValue("base2", out var base2Str))
				return base2Str.Replace("_", string.Empty);

			if (match.Groups.TryGetValue("base16", out var base16Str))
				return Convert.ToString(Convert.ToInt64(base16Str, 16), 2).PadLeft(base16Str.Length * 4, '0');

			if (match.Groups.TryGetValue("letter", out var letterStr))
			{
				int length = int.Parse(match.Groups["length"].Value, CultureInfo.InvariantCulture);
				return new string(letterStr[0], length);
			}

			return XedBitPattern.Normalize(match.Groups["pattern"].Value);
		}
	}
}
