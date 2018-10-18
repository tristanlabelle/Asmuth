using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Asmuth.X86.Xed
{
	partial class XedInstruction
	{
		public static XedInstruction FromStrings(
			IEnumerable<KeyValuePair<string, string>> fields,
			IEnumerable<XedInstructionForm.Strings> forms,
			in XedInstructionStringResolvers resolvers)
		{
			var instruction = new XedInstruction();
			foreach (var field in fields)
			{
				if (field.Key == "ATTRIBUTES")
				{
					foreach (var attribute in Regex.Split(field.Value, @"\s+"))
						if (attribute.Length > 0)
							instruction.attributes.Add(attribute);
				}
				else if (field.Key == "CPL")
				{
					instruction.privilegeLevel = byte.Parse(field.Value, CultureInfo.InvariantCulture);
				}
				else if (field.Key == "FLAGS")
				{
					foreach (var str in Regex.Split(field.Value, @"\s*,\s*"))
						instruction.flags.Add(XedFlagsRecord.Parse(str));
				}
				else if (field.Key == "VERSION")
				{
					instruction.version = byte.Parse(field.Value, CultureInfo.InvariantCulture);
				}
				else
				{
					instruction.stringFields.Add(field.Key, field.Value);
				}
			}

			foreach (var form in forms)
			{
				instruction.forms.Add(XedInstructionForm.Parse(form, resolvers));
			}

			return instruction;
		}
	}

	partial class XedInstructionForm
	{
		public static XedInstructionForm Parse(in Strings strings, XedInstructionStringResolvers resolvers)
		{
			// Preprocess and parse pattern
			string pattern = Regex.Replace(strings.Pattern, @"[a-zA-Z_][a-zA-Z0-9_]*",
				match => resolvers.State(match.Value) ?? match.Value);
			var blotStrs = Regex.Split(pattern, @"\s+");
			var patternBuilder = ImmutableArray.CreateBuilder<XedBlot>(blotStrs.Length);
			foreach (var blotStr in blotStrs)
			{
				var result = XedBlot.Parse(blotStr, resolvers.Field);
				patternBuilder.Add(result.Item1);
				if (result.Item2.HasValue) patternBuilder.Add(result.Item2.Value);
			}

			patternBuilder.Capacity = patternBuilder.Count;

			// Parse operands
			ImmutableArray<XedOperand> operandArray = ImmutableArray<XedOperand>.Empty;
			if (strings.Operands.Length > 0)
			{
				var operandsStrs = Regex.Split(strings.Operands, @"\s+");
				var builder = ImmutableArray.CreateBuilder<XedOperand>(operandsStrs.Length);
				foreach (var operandStr in operandsStrs)
				{
					var operand = XedOperand.Parse(operandStr, resolvers);
					builder.Add(operand);
				}

				builder.Capacity = builder.Count;
				operandArray = builder.MoveToImmutable();
			}

			return new XedInstructionForm(patternBuilder.MoveToImmutable(), operandArray, strings.Name);
		}
	}
}
