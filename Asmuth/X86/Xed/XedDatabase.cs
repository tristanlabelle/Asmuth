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
		public EmbeddedKeyCollection<XedPattern, string> Patterns { get; }
			= new EmbeddedKeyCollection<XedPattern, string>(p => p.Name);

		public static XedDatabase LoadDirectory(string path)
		{
			var database = new XedDatabase();

			// Load registers
			using (var reader = new StreamReader(Path.Combine(path, "all-registers.txt")))
				foreach (var entry in XedDataFiles.ParseRegisters(reader))
					database.RegisterTable.AddOrUpdate(entry);

			var registerFieldType = new XedRegisterFieldType(database.RegisterTable);
			Func<string, XedEnumFieldType> enumLookup= s =>
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

			// Load decode patterns
			using (var reader = new StreamReader(Path.Combine(path, "all-dec-patterns.txt")))
			{
				foreach (var pattern in XedDataFiles.ParsePatterns(reader, stateResolver, fieldResolver))
				{
					// Merge rule patterns with the same name
					if (database.Patterns.TryFind(pattern.Name, out var existing)
						&& pattern is XedRulePattern && existing is XedRulePattern)
					{
						var newRulePattern = (XedRulePattern)pattern;
						var exisingRulePattern = (XedRulePattern)existing;
						if (newRulePattern.ReturnsRegister != exisingRulePattern.ReturnsRegister)
							throw new FormatException();

						foreach (var @case in newRulePattern.Cases)
							exisingRulePattern.Cases.Add(@case);
						continue;
					}

					database.Patterns.Add(pattern);
				}
			}

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
					if (database.Patterns.TryFind(entry.Key, out var callable))
						table = (XedInstructionTable)callable;
					else
					{
						table = new XedInstructionTable(entry.Key);
						database.Patterns.Add(table);
					}
					table.Instructions.Add(entry.Value);
				}
			}

			return database;
		}
	}
}
