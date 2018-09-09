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

		public static IEnumerable<KeyValuePair<string, XedXType>> ParseXTypes(TextReader reader)
		{
			foreach (var lineMatch in ParseLineBased(reader, xtypesLineRegex))
			{
				if (!Enum.TryParse<XedType>(lineMatch.Groups[2].Value, ignoreCase: true, out var type))
					throw new FormatException();
				if (!ushort.TryParse(lineMatch.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var bitsPerElement))
					throw new FormatException();
				yield return new KeyValuePair<string, XedXType>(
					lineMatch.Groups[1].Value, new XedXType(type, bitsPerElement));
			}
		}
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

		public static IEnumerable<KeyValuePair<string, XedOperandWidth>> ParseOperandWidths(
			TextReader reader, Func<string, XedXType> xtypeLookup)
		{
			foreach (var lineMatch in ParseLineBased(reader, widthsLineRegex))
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
				yield return new KeyValuePair<string, XedOperandWidth>(key, width);
			}
		}
		#endregion

		#region StateMacros
		private static readonly Regex stateMacroLineRegex = new Regex(@"^\s*(\w+)\s+(.+?)\s*$");

		public static IEnumerable<KeyValuePair<string, ImmutableArray<XedPatternBlot>>>
			ParseStateMacros(TextReader reader)
		{
			var blotsBuilder = ImmutableArray.CreateBuilder<XedPatternBlot>();
			foreach (var lineMatch in ParseLineBased(reader, stateMacroLineRegex))
			{
				var blotStrs = Regex.Split(lineMatch.Groups[2].Value, @"\s+");
				blotsBuilder.Capacity = blotStrs.Length;
				for (int i = 0; i < blotStrs.Length; ++i)
					blotsBuilder[i] = XedPatternBlot.Parse(blotStrs[i]);

				var key = lineMatch.Groups[1].Value;
				yield return new KeyValuePair<string, ImmutableArray<XedPatternBlot>>(
					key, blotsBuilder.MoveToImmutable());
			}
		}
		#endregion

		#region Registers
		public readonly struct RegisterEntry
		{
			// name class width max-enclosing-reg-64b/32b-mode regid [h]
			public string Name { get; }
			public string Class { get; }
			public string MaxEnclosingRegName_IA32 { get; }
			public string MaxEnclosingRegName_LongMode { get; }
			private readonly ushort widthInBits_IA32;
			private readonly ushort widthInBits_LongMode;
			private readonly byte idPlusOne;
			public bool IsHighByte { get; }

			public RegisterEntry(
				string name, string @class,
				int widthInBits_IA32, int widthInBits_LongMode,
				string maxEnclosingRegName_IA32, string maxEnclosingRegName_LongMode,
				int? id, bool isHighByte)
			{
				this.Name = name;
				this.Class = @class;
				this.MaxEnclosingRegName_IA32 = maxEnclosingRegName_IA32;
				this.MaxEnclosingRegName_LongMode = maxEnclosingRegName_LongMode;
				this.widthInBits_IA32 = checked((ushort)widthInBits_IA32);
				this.widthInBits_LongMode = checked((ushort)widthInBits_LongMode);
				this.idPlusOne = id.HasValue ? checked((byte)(id.Value + 1)) : (byte)0;
				this.IsHighByte = isHighByte;
			}

			public int WidthInBits_IA32 => widthInBits_IA32;
			public int WidthInBits_LongMode => widthInBits_LongMode;
			public byte? ID => idPlusOne == 0 ? null : (byte?)(idPlusOne - 1);
		}

		// name class width max-enclosing-reg-64b/32b-mode regid [h]
		private static readonly Regex registerLineRegex = new Regex(
			@"^\s*(?<name>\w+)\s+(?<class>\w+)
			\s+((?<width32>\d+)(/(?<width64>\d+))?|NA)
			(
				\s+(?<parent64>\d+)(/(?<parent32>\d+))?
				\s+(?<id>\d+)
				(\s+(?<h>h))?
				(\s+-\s+st\(\d\))?
			)?\s*$",
			RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

		public static IEnumerable<RegisterEntry> ParseRegisters(TextReader reader)
		{
			foreach (var lineMatch in ParseLineBased(reader, registerLineRegex))
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

				yield return new RegisterEntry(name, lineMatch.Groups["class"].Value,
					width32, width64, parent32, parent64, id, lineMatch.Groups["h"].Success);
			}
		}
		#endregion

			#region PatternRules
		private static readonly Regex patternRuleNameLineRegex = new Regex(
			@"^\s*(?:(?<reg>xed_reg_enum_t)\s+)?(?<name>\w+)\(\)::\s*$");
		private static readonly Regex patternRuleCaseLineRegex = new Regex(@"^\s*(.*?)\s*\|\s*(.*?)\s*$");

		public static IEnumerable<XedPatternRule> ParsePatternRules(
			TextReader reader, Func<string, ImmutableArray<XedPatternBlot>> stateMacroResolver)
		{
			string ruleName = null;
			bool returnsRegister = false;
			var conditionsBuilder = ImmutableArray.CreateBuilder<XedPatternBlot>();
			var actionssBuilder = ImmutableArray.CreateBuilder<XedPatternBlot>();

			while (true)
			{
				var line = reader.ReadLine();
				if (line == null) yield break;

				line = commentRegex.Replace(line, string.Empty);
				if (line.Length == 0) continue;

				var newRuleMatch = patternRuleNameLineRegex.Match(line);
				if (newRuleMatch.Success)
				{
					if (ruleName != null) throw new NotImplementedException();
					ruleName = newRuleMatch.Groups["name"].Value;
					returnsRegister = newRuleMatch.Groups["reg"].Success;
					continue;
				}

				var ruleCaseMatch = patternRuleCaseLineRegex.Match(line);
				if (!ruleCaseMatch.Success) throw new FormatException();

				throw new NotImplementedException();
			}
		}
		#endregion

		private static IEnumerable<Match> ParseLineBased(TextReader reader, Regex lineRegex)
		{
			while (true)
			{
				var line = reader.ReadLine();
				if (line == null) yield break;

				line = commentRegex.Replace(line, string.Empty);
				if (line.Length == 0) continue;

				var match = lineRegex.Match(line);
				if (!match.Success) throw new FormatException("Badly formatted xed data file.");

				yield return match;
			}
		}
	}
}
