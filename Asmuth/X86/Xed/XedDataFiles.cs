using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Asmuth.X86.Xed
{
	public static class XedDataFiles
	{
		private static readonly Regex commentRegex = new Regex(@"\s*#.*$");

		#region XTypes
		private static readonly Regex xtypesLineRegex = new Regex(@"^\s*(\w+)\s+(\w+)\s+(\d+)\s*$");

		private static KeyValuePair<string, XedXType> ParseXType(Match lineMatch)
		{
			if (!Enum.TryParse<XedBaseType>(lineMatch.Groups[2].Value, ignoreCase: true, out var type))
				throw new FormatException();
			if (!ushort.TryParse(lineMatch.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var bitsPerElement))
				throw new FormatException();
			return new KeyValuePair<string, XedXType>(
				lineMatch.Groups[1].Value, new XedXType(type, bitsPerElement));
		}

		public static KeyValuePair<string, XedXType> ParseXType(string line, bool allowComments = true)
			=> ParseXType(MatchLine(line, xtypesLineRegex, allowComments));

		public static IEnumerable<KeyValuePair<string, XedXType>> ParseXTypes(TextReader reader)
			=> ParseLineBased(reader, xtypesLineRegex, ParseXType);
		#endregion

		#region OperandWidths
		private static readonly Regex widthsLineRegex = new Regex(
			@"^\s*(\w+)\s+(\w+)\s+
			(?<size16>\d+)(?<bits16>bits)?
			(
				\s+(?<size32>\d+)(?<bits32>bits)?
				\s+(?<size64>\d+)(?<bits64>bits)?
			)?
			\s*$", RegexOptions.IgnorePatternWhitespace);

		public static KeyValuePair<string, XedOperandWidth> ParseOperandWidth(
			Match lineMatch, Func<string, XedXType> xtypeLookup)
		{
			var xtype = xtypeLookup(lineMatch.Groups[2].Value);

			if (!ushort.TryParse(lineMatch.Groups["size16"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var width16))
				throw new FormatException();
			if (!lineMatch.Groups["bits16"].Success) width16 *= 8;

			XedOperandWidth width;
			if (lineMatch.Groups["size32"].Success)
			{
				if (!ushort.TryParse(lineMatch.Groups["size32"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var width32))
					throw new FormatException();
				if (!lineMatch.Groups["bits32"].Success) width32 *= 8;

				if (!ushort.TryParse(lineMatch.Groups["size64"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var width64))
					throw new FormatException();
				if (!lineMatch.Groups["bits64"].Success) width64 *= 8;

				width = new XedOperandWidth(xtype, width16, width32, width64);
			}
			else
			{
				width = new XedOperandWidth(xtype, width16);
			}

			var key = lineMatch.Groups[1].Value;
			return new KeyValuePair<string, XedOperandWidth>(key, width);
		}

		public static KeyValuePair<string, XedOperandWidth> ParseOperandWidth(
			string line, Func<string, XedXType> xtypeLookup, bool allowComments = true)
			=> ParseOperandWidth(MatchLine(line, widthsLineRegex, allowComments), xtypeLookup);

		public static IEnumerable<KeyValuePair<string, XedOperandWidth>> ParseOperandWidths(
			TextReader reader, Func<string, XedXType> xtypeLookup)
			=> ParseLineBased(reader, widthsLineRegex, match => ParseOperandWidth(match, xtypeLookup));
		#endregion

		#region StateMacros
		private static readonly Regex stateMacroLineRegex = new Regex(@"^\s*(\w+)\s+(.+?)\s*$");

		private static KeyValuePair<string, string> ParseStateMacro(Match lineMatch)
			=> new KeyValuePair<string, string>(
				lineMatch.Groups[1].Value, lineMatch.Groups[2].Value);

		public static KeyValuePair<string, string> ParseStateMacro(string line, bool allowComments = true)
			=> ParseStateMacro(MatchLine(line, stateMacroLineRegex, allowComments));


		public static IEnumerable<KeyValuePair<string, string>> ParseStateMacros(TextReader reader)
			=> ParseLineBased(reader, stateMacroLineRegex, ParseStateMacro);
		#endregion

		#region Registers
		public readonly struct RegisterEntry
		{
			// name class width max-enclosing-reg-64b/32b-mode regid [h]
			public string Name { get; }
			public string Class { get; }
			public string MaxEnclosingRegName_IA32 { get; }
			public string MaxEnclosingRegName_X64 { get; }
			private readonly ushort widthInBits_IA32;
			private readonly ushort widthInBits_X64;
			private readonly byte idPlusOne;
			public bool IsHighByte { get; }

			public RegisterEntry(
				string name, string @class,
				int widthInBits_IA32, int widthInBits_X64,
				string maxEnclosingRegName_IA32, string maxEnclosingRegName_X64,
				int? id, bool isHighByte)
			{
				this.Name = name;
				this.Class = @class;
				this.MaxEnclosingRegName_IA32 = maxEnclosingRegName_IA32;
				this.MaxEnclosingRegName_X64 = maxEnclosingRegName_X64;
				this.widthInBits_IA32 = checked((ushort)widthInBits_IA32);
				this.widthInBits_X64 = checked((ushort)widthInBits_X64);
				this.idPlusOne = id.HasValue ? checked((byte)(id.Value + 1)) : (byte)0;
				this.IsHighByte = isHighByte;
			}

			public int WidthInBits_IA32 => widthInBits_IA32;
			public int WidthInBits_X64 => widthInBits_X64;
			public int? ID => idPlusOne == 0 ? null : (int?)(idPlusOne - 1);
		}

		// name class width max-enclosing-reg-64b/32b-mode regid [h]
		private static readonly Regex registerLineRegex = new Regex(
			@"^\s*(?<name>\w+)\s+(?<class>\w+)
			\s+((?<width32>\d+)(/(?<width64>\d+))?|NA)
			(
				\s+(?<parent64>\w+)(/(?<parent32>\w+))?
				(
					\s+(?<id>\d+)
					(\s+((?<h>h)|-\s*st\(\d\)))?
				)?
			)?\s*$",
			RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

		private static RegisterEntry ParseRegister(Match lineMatch)
		{
			ushort width32 = 0;
			ushort width64 = 0;
			if (lineMatch.Groups["width32"].Success)
			{
				width32 = ushort.Parse(lineMatch.Groups["width32"].Value, CultureInfo.InvariantCulture);
				if (lineMatch.Groups["width64"].Success)
					width64 = ushort.Parse(lineMatch.Groups["width64"].Value, CultureInfo.InvariantCulture);
				else
					width64 = width32;
			}

			var name = lineMatch.Groups["name"].Value;
			string parent32 = name;
			string parent64 = name;
			if (lineMatch.Groups["parent64"].Success)
			{
				parent64 = lineMatch.Groups["parent64"].Value;
				parent32 = lineMatch.Groups["parent32"].Success
					? lineMatch.Groups["parent32"].Value : parent64;
			}

			byte? id = null;
			if (lineMatch.Groups["id"].Success)
				id = byte.Parse(lineMatch.Groups["id"].Value, CultureInfo.InvariantCulture);

			return new RegisterEntry(name, lineMatch.Groups["class"].Value,
				width32, width64, parent32, parent64, id, lineMatch.Groups["h"].Success);
		}

		public static RegisterEntry ParseRegister(string line, bool allowComments = true)
			=> ParseRegister(MatchLine(line, registerLineRegex, allowComments));

		public static IEnumerable<RegisterEntry> ParseRegisters(TextReader reader)
			=> ParseLineBased(reader, registerLineRegex, ParseRegister);
		
		public static XedRegisterTable ParseRegisterTable(TextReader reader)
		{
			var table = new XedRegisterTable();
			foreach (var register in ParseRegisters(reader))
				table.AddOrUpdate(in register);
			return table;
		}
		#endregion

		#region PatternRules
		private static readonly Regex patternRuleNameLineRegex = new Regex(
			@"^\s*(?:(?<reg>xed_reg_enum_t)\s+)?(?<name>\w+)\(\)::\s*$");
		private static readonly Regex patternRuleCaseLineRegex = new Regex(
			@"^\s*(.*?)\s*\|\s*(.*?)\s*$");

		public static IEnumerable<XedPatternRule> ParsePatternRules(
			TextReader reader, Func<string, string> stateMacroResolver)
		{
			string ruleName = null;
			bool returnsRegister = false;
			var conditionsBuilder = ImmutableArray.CreateBuilder<XedBlot>();
			var actionsBuilder = ImmutableArray.CreateBuilder<XedBlot>();
			var caseBuilder = ImmutableArray.CreateBuilder<XedPatternRuleCase>();

			while (true)
			{
				var line = reader.ReadLine();
				if (line == null) break;

				line = commentRegex.Replace(line, string.Empty);
				if (line.Length == 0) continue;

				// Match rules
				var newRuleMatch = patternRuleNameLineRegex.Match(line);
				if (newRuleMatch.Success)
				{
					if (ruleName != null)
					{
						yield return new XedPatternRule(ruleName, returnsRegister,
							caseBuilder.ToImmutable());
					}

					ruleName = newRuleMatch.Groups["name"].Value;
					returnsRegister = newRuleMatch.Groups["reg"].Success;
					continue;
				}

				// Expand macros
				line = Regex.Replace(line, @"\w+", m => stateMacroResolver(m.Value) ?? m.Value);

				var ruleCaseMatch = patternRuleCaseLineRegex.Match(line);
				if (!ruleCaseMatch.Success) throw new FormatException();

				if (ruleCaseMatch.Groups[1].Value != "otherwise")
					foreach (var blotStr in Regex.Split(ruleCaseMatch.Groups[1].Value, @"\s+"))
						conditionsBuilder.Add(XedBlot.Parse(blotStr, condition: true));

				bool reset = false;
				if (ruleCaseMatch.Groups[2].Value != "nothing")
				{
					foreach (var blotStr in Regex.Split(ruleCaseMatch.Groups[2].Value, @"\s+"))
					{
						if (blotStr == "XED_RESET") reset = true;
						else actionsBuilder.Add(XedBlot.Parse(blotStr, condition: false));
					}
				}

				caseBuilder.Add(new XedPatternRuleCase(conditionsBuilder.ToImmutable(),
					actionsBuilder.ToImmutable(), reset));
			}

			if (ruleName != null)
			{
				yield return new XedPatternRule(ruleName, returnsRegister,
					caseBuilder.ToImmutable());
			}
		}
		#endregion

		private static Match MatchLine(string line, Regex regex, bool allowComments = true)
		{
			if (allowComments) line = commentRegex.Replace(line, string.Empty);

			var match = regex.Match(line);
			if (!match.Success) throw new FormatException("Badly formatted XED data.");

			return match;
		}

		private static IEnumerable<T> ParseLineBased<T>(
			TextReader reader, Regex lineRegex, Func<Match, T> parser)
		{
			while (true)
			{
				var line = reader.ReadLine();
				if (line == null) yield break;

				line = commentRegex.Replace(line, string.Empty);
				if (line.Length == 0) continue;
				
				yield return parser(MatchLine(line, lineRegex, allowComments: false));
			}
		}
	}
}
