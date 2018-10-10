using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Asmuth.X86.Xed
{
	public sealed class XedEngine
	{
		public XedDatabase Database { get; }
		private readonly Dictionary<XedField, long> fieldValues = new Dictionary<XedField, long>();
		private XedInstruction encodingInstruction;
		private int encodingInstructionFormIndex = -1;

		public XedEngine(XedDatabase database)
		{
			this.Database = database ?? throw new ArgumentNullException(nameof(database));
		}

		private bool IsEncoding => encodingInstruction != null;
		private XedInstructionForm EncodingInstructionForm => encodingInstruction?.Forms[encodingInstructionFormIndex];

		public byte[] Encode(XedInstruction instruction, int formIndex, bool x64, IntegerSize operandSize)
		{
			encodingInstruction = instruction;
			encodingInstructionFormIndex = formIndex;

			foreach (var blot in instruction.Forms[formIndex].Pattern)
			{
				if (blot.Type != XedBlotType.Equality || blot.Field.EncoderUsage != XedFieldUsage.Input)
					continue;
				fieldValues[blot.Field] = blot.Value.Constant;
			}

			if (!Database.EncodeSequences.TryFind("ISA_ENCODE", out XedSequence rootSequence))
				throw new InvalidOperationException();

			bool reset;
			do { reset = Execute(rootSequence); }
			while (reset);

			throw new NotImplementedException();
		}

		private bool Execute(XedSequence sequence)
		{
			foreach (var entry in sequence.Entries)
			{
				if (entry.Type == XedSequenceEntryType.Sequence)
				{
					if (!Database.EncodeSequences.TryFind(entry.TargetName, out var targetSequence))
						throw new InvalidOperationException();
					if (Execute(targetSequence)) return true;
				}
				else if (entry.Type == XedSequenceEntryType.Pattern)
				{
					var pattern = Database.FindPattern(entry.TargetName, encode: IsEncoding);
					if (pattern == null) throw new InvalidOperationException();
					if (Execute(pattern)) throw new InvalidOperationException(); // Can't reset encoder
				}
				else
					throw new InvalidOperationException();
			}

			return false; // Don't reset
		}

		private bool Execute(XedPattern pattern)
		{
			bool reset;
			if (pattern is XedRulePattern rulePattern)
				reset = Execute(rulePattern);
			else if (pattern is XedInstructionTable instructionTable)
				reset = Execute(instructionTable);
			else
				throw new InvalidOperationException();
			return reset;
		}

		private bool Execute(XedRulePattern rulePattern)
		{
			if (rulePattern.ReturnsRegister) throw new InvalidOperationException();
			ushort? outReg = null;
			return Execute(rulePattern, ref outReg);
		}

		private bool Execute(XedRulePattern rulePattern, ref ushort? register)
		{
			if (rulePattern.IsEncode && !IsEncoding) throw new InvalidOperationException();
			if (register.HasValue != (IsEncoding && rulePattern.ReturnsRegister))
				throw new InvalidOperationException();

			foreach (var @case in rulePattern.Cases)
			{
				if (!TryMatchRuleCaseCondition(@case.Conditions, IsEncoding ? register : null))
					continue;

				var outReg = ExecuteRuleCaseActions(@case.Conditions);
				if (outReg.HasValue)
				{
					if (!rulePattern.ReturnsRegister || IsEncoding)
						throw new InvalidOperationException();
					register = outReg;
				}

				if (@case.ControlFlow == XedRulePatternControlFlow.Break) break;
				if (@case.ControlFlow == XedRulePatternControlFlow.Continue) continue;
				if (@case.ControlFlow == XedRulePatternControlFlow.Reset) return true;
				throw new UnreachableException();
			}

			return false;
		}

		private bool TryMatchRuleCaseCondition(ImmutableArray<XedBlot> blots, ushort? outReg)
		{
			foreach (var blot in blots)
			{
				switch (blot.Type)
				{
					case XedBlotType.Bits:
						throw new NotImplementedException();

					case XedBlotType.Equality:
					case XedBlotType.Inequality:
						if (!MatchPredicateBlot(blot.Field, blot.Value, isEquals: (blot.Type == XedBlotType.Equality), outReg))
							return false;
						break;

					case XedBlotType.Call: throw new InvalidOperationException();
					default: throw new UnreachableException();
				}
			}

			return true;
		}

		private bool MatchPredicateBlot(XedField field, XedBlotValue value, bool isEquals, ushort? outReg)
		{
			// Load field value
			long fieldValue;
			if (field.Name == "OUTREG")
			{
				if (!outReg.HasValue) throw new InvalidOperationException();
				fieldValue = outReg.Value;
			}
			else fieldValue = fieldValues[field];

			// Load comparison value
			long comparisonValue;
			if (value.Kind == XedBlotValueKind.Constant)
				comparisonValue = value.Constant;
			else if (value.Kind == XedBlotValueKind.CallResult)
			{
				var callee = Database.FindPattern(value.Callee, encode: IsEncoding) as XedRulePattern;
				if (callee == null) throw new InvalidOperationException();
				if (Execute(callee, ref outReg)) throw new InvalidOperationException(); // Can't reset here
				comparisonValue = outReg.Value;
			}
			else if (value.Kind == XedBlotValueKind.Bits)
				throw new InvalidOperationException();
			else
				throw new UnreachableException();

			return (fieldValue == comparisonValue) == isEquals;
		}

		private ushort? ExecuteRuleCaseActions(ImmutableArray<XedBlot> blots)
		{
			return ExecuteActionBlots(blots, b => true);
		}

		private ushort? ExecuteActionBlots(ImmutableArray<XedBlot> blots, Predicate<XedBlot> filter)
		{
			throw new NotImplementedException();
		}
		
		private bool Execute(XedInstructionTable instructionTable)
		{
			if (IsEncoding)
			{
				if (!instructionTable.Instructions.Contains(encodingInstruction))
					throw new InvalidOperationException();

				var outRegister = ExecuteActionBlots(EncodingInstructionForm.Pattern,
					b => b.Field.EncoderUsage == XedFieldUsage.Output);
				if (outRegister != null) throw new InvalidOperationException();
				return false;
			}
			else
			{
				throw new NotImplementedException();
			}
		}
	}
}
