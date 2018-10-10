using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Asmuth.X86.Xed
{
	public sealed class XedDatabase
	{
		public XedRegisterTable RegisterTable { get; } = new XedRegisterTable();
		public EmbeddedKeyCollection<XedField, string> Fields { get; }
			= new EmbeddedKeyCollection<XedField, string>(f => f.Name);
		public EmbeddedKeyCollection<XedSequence, string> EncodeSequences { get; }
			= new EmbeddedKeyCollection<XedSequence, string>(p => p.Name);
		public EmbeddedKeyCollection<XedPattern, string> EncodePatterns { get; }
			= new EmbeddedKeyCollection<XedPattern, string>(p => p.Name);
		public EmbeddedKeyCollection<XedPattern, string> EncodeDecodePatterns { get; }
			= new EmbeddedKeyCollection<XedPattern, string>(p => p.Name);
		public EmbeddedKeyCollection<XedPattern, string> DecodePatterns { get; }
			= new EmbeddedKeyCollection<XedPattern, string>(p => p.Name);

		public XedPattern FindPattern(string name, bool encode)
		{
			XedPattern pattern;
			if (!(encode ? EncodePatterns : DecodePatterns).TryFind(name, out pattern))
				EncodeDecodePatterns.TryFind(name, out pattern);
			return pattern;
		}

		public static XedDatabase LoadDirectory(string path)
		{
			var database = new XedDatabase();

			// Load registers
			using (var reader = new StreamReader(Path.Combine(path, "all-registers.txt")))
				foreach (var entry in XedDataFiles.ParseRegisters(reader))
					database.RegisterTable.AddOrUpdate(entry);

			var registerFieldType = new XedRegisterFieldType(database.RegisterTable);
			Func<string, XedEnumFieldType> enumLookup = s =>
				s == "reg" ? registerFieldType
				: s == "error" ? XedEnumFieldType.Error
				: s == "chip" ? XedEnumFieldType.DummyChip
				: s == "iclass" ? XedEnumFieldType.DummyIClass
				: throw new KeyNotFoundException();

			// Load fields
			using (var reader = new StreamReader(Path.Combine(path, "all-fields.txt")))
				foreach (var field in XedDataFiles.ParseFields(reader, enumLookup))
					database.Fields.Add(field);
			Func<string, XedField> fieldResolver = s => database.Fields.TryFind(s, out var f)
				? f : throw new KeyNotFoundException();

			// Load state macros
			var states = new Dictionary<string, string>();
			using (var reader = new StreamReader(Path.Combine(path, "all-state.txt")))
			{
				foreach (var entry in XedDataFiles.ParseStateMacros(reader))
				{
					// Ignore if redundant
					if (states.TryGetValue(entry.Key, out var value))
					{
						if (value != entry.Value) throw new FormatException();
						continue;
					}

					states.Add(entry.Key, entry.Value);
				}
			}
			Func<string, string> stateResolver = s => states.TryGetValue(s, out var r) ? r : null;

			// Load XTypes
			var xtypes = new Dictionary<string, XedXType>();
			using (var reader = new StreamReader(Path.Combine(path, "all-element-types.txt")))
				foreach (var entry in XedDataFiles.ParseXTypes(reader))
					xtypes.Add(entry.Key, entry.Value);
			Func<string, XedXType> xtypeResolver = s => xtypes[s];

			// Load widths
			var widths = new Dictionary<string, XedOperandWidth>();
			using (var reader = new StreamReader(Path.Combine(path, "all-widths.txt")))
			{
				foreach (var entry in XedDataFiles.ParseOperandWidths(reader, xtypeResolver))
				{
					// Ignore if redundant
					if (widths.TryGetValue(entry.Key, out var value))
					{
						if (value != entry.Value) throw new FormatException();
						continue;
					}

					widths.Add(entry.Key, entry.Value);
				}
			}
			Func<string, XedOperandWidth> operandWidthResolver = s => widths[s];

			// Load patterns
			LoadPatterns(Path.Combine(path, "all-enc-dec-patterns.txt"), fieldResolver, stateResolver, database.EncodeDecodePatterns);
			LoadPatterns(Path.Combine(path, "all-dec-patterns.txt"), fieldResolver, stateResolver, database.DecodePatterns);
			LoadPatterns(Path.Combine(path, "all-enc-patterns.txt"), fieldResolver, stateResolver,
				database.EncodePatterns, database.EncodeSequences);

			// Load instructions
			using (var reader = new StreamReader(Path.Combine(path, "all-enc-instructions.txt")))
			{
				var resolver = new XedInstructionStringResolvers
				{
					State = stateResolver,
					Field = fieldResolver,
					OperandWidth = operandWidthResolver,
					XType = xtypeResolver
				};
				foreach (var entry in XedDataFiles.ParseInstructions(reader, resolver))
				{
					XedInstructionTable table;
					if (database.EncodeDecodePatterns.TryFind(entry.PatternName, out var callable))
						table = (XedInstructionTable)callable;
					else
					{
						table = new XedInstructionTable(entry.PatternName);
						database.EncodeDecodePatterns.Add(table);
					}

					if (entry.Type == XedDataFiles.InstructionsFileEntryType.Instruction)
						table.Instructions.Add(entry.Instruction);
					else if (entry.Type == XedDataFiles.InstructionsFileEntryType.DeleteInstruction)
					{
						for (int i = 0; i < table.Instructions.Count; ++i)
						{
							if (table.Instructions[i].UniqueName == entry.DeleteTarget)
							{
								table.Instructions.RemoveAt(i);
								break;
							}
						}
					}
					else throw new UnreachableException();
				}
			}

			return database;
		}

		private static void LoadPatterns(string path,
			Func<string, XedField> fieldResolver, Func<string, string> stateResolver,
			IEmbeddedKeyCollection<XedPattern, string> patterns,
			IEmbeddedKeyCollection<XedSequence, string> sequences = null)
		{
			using (var reader = new StreamReader(path))
			{
				foreach (var symbol in XedDataFiles.ParsePatterns(reader, stateResolver, fieldResolver))
				{
					if (symbol is XedSequence sequence)
					{
						if (sequences == null) throw new FormatException();

						// Only keep the latest sequence
						if (sequences.TryFind(sequence.Name, out var existingSequence))
							sequences.Remove(existingSequence);
						sequences.Add(sequence);
						continue;
					}

					var pattern = (XedPattern)symbol;
					if (patterns.TryFind(symbol.Name, out var existingPattern))
					{
						if (symbol.GetType() != existingPattern.GetType()) throw new FormatException();

						if (symbol is XedRulePattern)
						{
							// Merge rule patterns with the same name
							var newRulePattern = (XedRulePattern)symbol;
							var exisingRulePattern = (XedRulePattern)existingPattern;
							if (newRulePattern.ReturnsRegister != exisingRulePattern.ReturnsRegister)
								throw new FormatException();

							foreach (var @case in newRulePattern.Cases)
								exisingRulePattern.Cases.Add(@case);
							continue;
						}
					}

					patterns.Add(pattern);
				}
			}
		}
	}
}
