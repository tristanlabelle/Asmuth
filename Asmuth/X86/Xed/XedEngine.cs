using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;

namespace Asmuth.X86.Xed
{
	public sealed class XedEngine
	{
		public XedDatabase Database { get; }
		private readonly Dictionary<XedField, long> fieldValues = new Dictionary<XedField, long>();
		private BitStream bitStream;
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
			var memoryStream = new MemoryStream(capacity: 8);
			bitStream = new BitStream(memoryStream);

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

			Execute(rootSequence);

			bitStream.Flush();
			return memoryStream.ToArray();
		}

		private void Execute(XedSequence sequence)
		{
			foreach (var entry in sequence.Entries)
			{
				if (entry.Type == XedSequenceEntryType.Sequence)
				{
					if (!Database.EncodeSequences.TryFind(entry.TargetName, out var targetSequence))
						throw new InvalidOperationException();
					Execute(targetSequence);
				}
				else if (entry.Type == XedSequenceEntryType.Pattern)
				{
					if (ExecutePattern(entry.TargetName))
						throw new InvalidOperationException(); // Can't reset encoder
				}
				else
					throw new InvalidOperationException();
			}
		}

		private bool ExecutePattern(string name)
		{
			var pattern = Database.FindPattern(name, encode: IsEncoding);
			if (pattern == null) throw new InvalidOperationException();
			return Execute(pattern);
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
				var controlFlow = ExecuteRuleCase(@case, ref register);
				if (!rulePattern.ReturnsRegister && register.HasValue)
					throw new InvalidOperationException();
				if (controlFlow == XedRulePatternControlFlow.Break) break;
				if (controlFlow == XedRulePatternControlFlow.Continue) continue;
				if (controlFlow == XedRulePatternControlFlow.Reset) return true;
				throw new UnreachableException();
			}

			return false;
		}

		private XedRulePatternControlFlow ExecuteRuleCase(XedRulePatternCase @case, ref ushort? register)
		{
			var bitVars = new SmallDictionary<char, long>();

			if (!TryMatchRuleCaseCondition(@case.Conditions, IsEncoding ? register : null, bitVars))
				return XedRulePatternControlFlow.Continue;

			var outReg = ExecuteRuleCaseActions(@case.Conditions, bitVars);
			if (outReg.HasValue)
			{
				if (IsEncoding) throw new InvalidOperationException();
				register = outReg;
			}

			return @case.ControlFlow;
		}

		private bool TryMatchRuleCaseCondition(ImmutableArray<XedBlot> blots, ushort? register,
			IDictionary<char, long> bitVars)
		{
			foreach (var blot in blots)
			{
				switch (blot.Type)
				{
					case XedBlotType.Bits:
						throw new NotImplementedException();

					case XedBlotType.Equality:
					case XedBlotType.Inequality:
						if (!MatchPredicateBlot(blot.Field, blot.Value, isEquals: (blot.Type == XedBlotType.Equality), register))
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
			
			return (fieldValue == Evaluate(value, outReg)) == isEquals;
		}

		private long Evaluate(XedBlotValue value, ushort? outReg, IDictionary<char, long> bitVars = null)
		{
			if (value.Kind == XedBlotValueKind.Constant)
				return value.Constant;
			else if (value.Kind == XedBlotValueKind.CallResult)
			{
				var callee = Database.FindPattern(value.Callee, encode: IsEncoding) as XedRulePattern;
				if (callee == null) throw new InvalidOperationException();
				if (Execute(callee, ref outReg)) throw new InvalidOperationException(); // Can't reset here
				return outReg.Value;
			}
			else if (value.Kind == XedBlotValueKind.Bits)
			{
				if (bitVars == null) throw new InvalidOperationException();
				throw new NotImplementedException();
			}
			else
				throw new UnreachableException();
		}

		private ushort? ExecuteRuleCaseActions(ImmutableArray<XedBlot> blots, IDictionary<char, long> bitVars)
		{
			return ExecuteActionBlots(blots, b => true, bitVars);
		}

		private ushort? ExecuteActionBlots(ImmutableArray<XedBlot> blots, Predicate<XedBlot> filter,
			IDictionary<char, long> bitVars)
		{
			ushort? outReg = null;
			foreach (var blot in blots)
			{
				if (!filter(blot)) continue;

				switch (blot.Type)
				{
					case XedBlotType.Bits:
						if (blot.Field != null || !IsEncoding) throw new InvalidOperationException();
						throw new NotImplementedException();

					case XedBlotType.Equality:
						{
							var value = Evaluate(blot.Value, outReg: null, bitVars);
							if (blot.Field.Name == "OUTREG") outReg = (ushort)value;
							else fieldValues[blot.Field] = value;
							break;
						}

					case XedBlotType.Call:
						if (ExecutePattern(blot.Callee))
							throw new InvalidOperationException(); // Can't bubble reset from here
						break;

					case XedBlotType.Inequality: throw new InvalidOperationException();
					default: throw new UnreachableException();
				}
			}

			return outReg;
		}
		
		private bool Execute(XedInstructionTable instructionTable)
		{
			if (IsEncoding)
			{
				if (!instructionTable.Instructions.Contains(encodingInstruction))
					throw new InvalidOperationException();

				var outRegister = ExecuteActionBlots(EncodingInstructionForm.Pattern,
					b => b.Field.EncoderUsage == XedFieldUsage.Output, bitVars: null);
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
