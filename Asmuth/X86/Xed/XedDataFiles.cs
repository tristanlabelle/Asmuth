using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
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
			@"^\s* (?<name>\w+) \s+ (?<xtype>\w+) \s+
			(?<size16>\d+)(?<bits16>bits)?
			(
				\s+(?<size32>\d+)(?<bits32>bits)?
				\s+(?<size64>\d+)(?<bits64>bits)?
			)?
			\s*$", RegexOptions.IgnorePatternWhitespace);

		public static KeyValuePair<string, XedOperandWidth> ParseOperandWidth(
			Match lineMatch, Func<string, XedXType> xtypeLookup)
		{
			var xtypeStr = lineMatch.Groups["xtype"].Value;
			var xtype = xtypeStr == "INVALID" ? new XedXType(XedBaseType.Struct, 0) : xtypeLookup(xtypeStr);

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

			var key = lineMatch.Groups["name"].Value;
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

		#region Fields
		private static readonly Regex fieldLineRegex = new Regex(
			@"^\s* (?<name>\w+) \s+ SCALAR \s+ (?<type>\w+) \s+ (?<bits>\d+) (\s+(?<flag>\w+))* \s*$",
			RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

		private static XedField ParseField(Match lineMatch, Func<string, XedEnumFieldType> enumLookup)
		{
			var name = lineMatch.Groups["name"].Value;
			var typeName = lineMatch.Groups["type"].Value;
			int sizeInBits = byte.Parse(lineMatch.Groups["bits"].Value, CultureInfo.InvariantCulture);
			var type = XedFieldType.FromNameInCodeAndSizeInBits(typeName, sizeInBits, enumLookup);
			XedFieldFlags flags = XedFieldFlags.None;
			foreach (Capture flagCapture in lineMatch.Groups["flag"].Captures)
				flags |= XedEnumNameAttribute.GetEnumerantOrNull<XedFieldFlags>(flagCapture.Value).Value;
			return new XedField(name, type, flags);
		}

		public static XedField ParseField(string line, Func<string, XedEnumFieldType> enumLookup, bool allowComments = true)
			=> ParseField(MatchLine(line, fieldLineRegex, allowComments), enumLookup);

		public static IEnumerable<XedField> ParseFields(TextReader reader, Func<string, XedEnumFieldType> enumLookup)
			=> ParseLineBased(reader, fieldLineRegex, m => ParseField(m, enumLookup));
		#endregion

		#region Patterns
		private static readonly Regex sequenceDeclarationLineRegex = new Regex(
			@"^\s*SEQUENCE\s+(?<name>\w+)\s*$");
		private static readonly Regex sequenceEntryLineRegex = new Regex(
			@"^\s*(?<name>\w+)(?<parens>\s*\(\s*\))\s*$");
		private static readonly Regex rulePatternDeclarationLineRegex = new Regex(
			@"^\s*(?:(?<reg>xed_reg_enum_t)\s+)?(?<name>\w+)\(\)::\s*$");
		// Workaround bad format "mode16 | OUTREG=YMM_R_32():"
		private static readonly Regex rulePatternCaseLineRegex = new Regex(
			@"^\s*(\S.*?)\s*\|\s*(\S.*?)?\s*:?\s*$");

		public static IEnumerable<XedPattern> ParsePatterns(
			TextReader reader, Func<string, string> stateMacroResolver,
			Func<string, XedField> fieldResolver)
		{
			string sequenceName = null;
			string rulePatternName = null;
			bool returnsRegister = false;
			var conditionsBuilder = ImmutableArray.CreateBuilder<XedBlot>();
			var actionsBuilder = ImmutableArray.CreateBuilder<XedBlot>();
			var ruleBuilder = ImmutableArray.CreateBuilder<XedRulePatternCase>();
			var sequenceEntryBuilder = ImmutableArray.CreateBuilder<XedSequenceEntry>();

			while (true)
			{
				var line = reader.ReadLine();
				bool flush = false;
				Match newRulePatternMatch = null;
				Match newSequenceMatch = null;
				if (line == null)
				{
					flush = true;
				}
				else
				{
					line = commentRegex.Replace(line, string.Empty);
					if (line.Length == 0) continue;

					// Match declarations
					newRulePatternMatch = rulePatternDeclarationLineRegex.Match(line);
					newSequenceMatch = sequenceDeclarationLineRegex.Match(line);
					flush = newRulePatternMatch.Success || newSequenceMatch.Success;
				}

				if (flush)
				{
					if (rulePatternName != null)
					{
						yield return new XedRulePattern(rulePatternName, returnsRegister, ruleBuilder.ToImmutable());
						ruleBuilder.Clear();
						rulePatternName = null;
					}
					else if (sequenceName != null)
					{
						yield return new XedSequence(sequenceName, sequenceEntryBuilder.ToImmutable());
						sequenceEntryBuilder.Clear();
						sequenceName = null;
					}

					if (line == null) break;
				}

				if (newRulePatternMatch.Success)
				{
					rulePatternName = newRulePatternMatch.Groups["name"].Value;
					returnsRegister = newRulePatternMatch.Groups["reg"].Success;
					continue;
				}

				if (newSequenceMatch.Success)
				{
					sequenceName = newSequenceMatch.Groups["name"].Value;
					continue;
				}

				if (rulePatternName != null)
				{
					// Expand macros
					line = Regex.Replace(line, @"\w+", m => stateMacroResolver(m.Value) ?? m.Value);

					var rulePatternCaseMatch = rulePatternCaseLineRegex.Match(line);
					if (!rulePatternCaseMatch.Success) throw new FormatException();

					if (rulePatternCaseMatch.Groups[1].Value != "otherwise")
						foreach (var blotStr in Regex.Split(rulePatternCaseMatch.Groups[1].Value, @"\s+"))
							AddBlots(conditionsBuilder, blotStr, fieldResolver);

					bool reset = false;
					var lhsGroup = rulePatternCaseMatch.Groups[2];
					if (lhsGroup.Success && lhsGroup.Value != "nothing")
					{
						foreach (var blotStr in Regex.Split(lhsGroup.Value, @"\s+"))
						{
							if (blotStr == "XED_RESET") reset = true;
							else AddBlots(actionsBuilder, blotStr, fieldResolver);
						}
					}

					ruleBuilder.Add(new XedRulePatternCase(conditionsBuilder.ToImmutable(),
						actionsBuilder.ToImmutable(), reset));
					conditionsBuilder.Clear();
					actionsBuilder.Clear();
				}
				else if (sequenceName != null)
				{
					var sequenceEntryMatch = sequenceEntryLineRegex.Match(line);
					if (!sequenceEntryMatch.Success) throw new FormatException();

					var targetName = sequenceEntryMatch.Groups["name"].Value;
					var type = sequenceEntryMatch.Groups["parens"].Success
						? XedSequenceEntryType.Pattern : XedSequenceEntryType.Sequence;
					sequenceEntryBuilder.Add(new XedSequenceEntry(targetName, type));
				}
				else throw new NotImplementedException();
			}
		}

		private static void AddBlots(ImmutableArray<XedBlot>.Builder builder, string str,
			Func<string, XedField> fieldResolver)
		{
			var blots = XedBlot.Parse(str, fieldResolver);
			builder.Add(blots.Item1);
			if (blots.Item2.HasValue) builder.Add(blots.Item2.Value);
		}
		#endregion

		#region Instructions
		private static readonly Regex declarationLineRegex = new Regex(
			@"^\s*([\w_]+)\s*\(\)::\s*$");

		private static readonly Regex udeleteLineRegex = new Regex(
			@"^\s*UDELETE\s*\:\s*([\w_]+)\s*$");

		private static readonly Regex instructionFieldLineRegex = new Regex(
			@"^\s*([\w_]+)\s*\:\s*(.*?)\s*$");
	
		private enum InstructionParseState : byte
		{
			TopLevel,
			PrePatternField,
			PostPatternField,
			PostOperandsField
		}

		public enum InstructionsFileEntryType : byte
		{
			Instruction,
			DeleteInstruction
		}

		public readonly struct InstructionsFileEntry
		{
			public string PatternName { get; }
			private readonly object value;
			public InstructionsFileEntryType Type { get; }

			private InstructionsFileEntry(string patternName, InstructionsFileEntryType type, object value)
			{
				this.PatternName = patternName ?? throw new ArgumentNullException(nameof(patternName));
				this.value = value;
				this.Type = type;
			}

			public XedInstruction Instruction => Type == InstructionsFileEntryType.Instruction
				? (XedInstruction)value : throw new InvalidOperationException();
			public string DeleteTarget => Type == InstructionsFileEntryType.DeleteInstruction
				? (string)value : throw new InvalidOperationException();

			public static InstructionsFileEntry MakeInstruction(string patternName, XedInstruction instruction)
				=> new InstructionsFileEntry(patternName, InstructionsFileEntryType.Instruction,
					instruction ?? throw new ArgumentNullException(nameof(instruction)));

			public static InstructionsFileEntry MakeDeleteInstruction(string patternName, string target)
				=> new InstructionsFileEntry(patternName, InstructionsFileEntryType.DeleteInstruction,
					target ?? throw new ArgumentNullException(nameof(target)));
		}

		public static IEnumerable<InstructionsFileEntry> ParseInstructions(
			TextReader reader, XedInstructionStringResolvers resolvers)
		{
			string patternName = null;
			var state = InstructionParseState.TopLevel;
			var fields = new Dictionary<string, string>();
			var formStrings = new List<XedInstructionForm.Strings>();
			while (true)
			{
				var line = reader.ReadLine();
				if (line == null) break;

				line = commentRegex.Replace(line, string.Empty);
				if (line.Length == 0) continue;
				
				while (line.EndsWith("\\"))
					line = line.Substring(0, line.Length - 1) + (reader.ReadLine() ?? throw new FormatException());

				line = line.Trim();

				if (state == InstructionParseState.TopLevel)
				{
					var declarationLineMatch = declarationLineRegex.Match(line);
					if (declarationLineMatch.Success)
					{
						patternName = declarationLineMatch.Groups[1].Value;
						continue;
					}

					if (patternName == null) throw new FormatException();

					if (line == "{")
					{
						state = InstructionParseState.PrePatternField;
						continue;
					}

					var udeleteLineMatch = udeleteLineRegex.Match(line);
					if (udeleteLineMatch.Success)
					{
						yield return InstructionsFileEntry.MakeDeleteInstruction(
							patternName, udeleteLineMatch.Groups[1].Value);
						continue;
					}

					throw new FormatException();
				}

				if (line == "}")
				{
					if (state == InstructionParseState.PostPatternField)
						throw new FormatException();

					var instruction = XedInstruction.FromStrings(fields, formStrings, resolvers);
					fields.Clear();
					formStrings.Clear();

					yield return InstructionsFileEntry.MakeInstruction(patternName, instruction);
					state = InstructionParseState.TopLevel;
					continue;
				}

				var fieldLineMatch = instructionFieldLineRegex.Match(line);
				if (!fieldLineMatch.Success) throw new FormatException();

				string fieldName = fieldLineMatch.Groups[1].Value;
				string fieldValue = fieldLineMatch.Groups[2].Value;

				if (fieldName == "PATTERN")
				{
					if (state == InstructionParseState.PostPatternField)
						throw new FormatException();

					formStrings.Add(new XedInstructionForm.Strings { Pattern = fieldValue });
					state = InstructionParseState.PostPatternField;
					continue;
				}

				if (fieldName == "OPERANDS")
				{
					if (state != InstructionParseState.PostPatternField)
						throw new FormatException();

					var temp = formStrings[formStrings.Count - 1];
					temp.Operands = fieldValue;
					formStrings[formStrings.Count - 1] = temp;
					state = InstructionParseState.PostOperandsField;
					continue;
				}

				if (fieldName == "IFORM")
				{
					if (state != InstructionParseState.PostOperandsField)
						throw new FormatException();

					var temp = formStrings[formStrings.Count - 1];
					temp.Name = fieldValue;
					formStrings[formStrings.Count - 1] = temp;
					state = InstructionParseState.PrePatternField;
					continue;
				}

				// Other field
				fields.Add(fieldName, fieldValue);
			}

			if (state != InstructionParseState.TopLevel) throw new FormatException();
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
