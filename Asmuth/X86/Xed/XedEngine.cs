using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;

namespace Asmuth.X86.Xed
{
	public sealed class XedEngine
	{
		public XedDatabase Database { get; }
		private readonly Dictionary<XedField, long> fieldValues = new Dictionary<XedField, long>();
		private BitStream bitStream;
		private XedInstruction encodingInstruction;
		private byte encodingInstructionFormIndex;
		private bool isBinding;
		private byte executionDepth;

		public XedEngine(XedDatabase database)
		{
			this.Database = database ?? throw new ArgumentNullException(nameof(database));
		}

		public event Action<int, string> TraceMessage;
		
		private bool IsEncoding => encodingInstruction != null;
		private bool IsDecoding => false;
		private XedInstructionForm EncodingInstructionForm => encodingInstruction?.Forms[encodingInstructionFormIndex];

		public byte[] Encode(XedInstruction instruction, int formIndex, CodeSegmentType codeSegmentType,
			AddressSize? effectiveAddressSize = null, IntegerSize? effectiveOperandSize = null)
		{
			if (effectiveOperandSize == IntegerSize.Byte)
				throw new ArgumentOutOfRangeException(nameof(effectiveOperandSize));

			var memoryStream = new MemoryStream(capacity: 8);
			bitStream = new BitStream(memoryStream);

			encodingInstruction = instruction;
			encodingInstructionFormIndex = checked((byte)formIndex);

			// Context fields
			fieldValues.Add(Database.Fields.Get("MODE"), (int)codeSegmentType);
			fieldValues.Add(Database.Fields.Get("EASZ"),
				effectiveAddressSize.HasValue ? (int)effectiveAddressSize.Value + 1 : 0);
			fieldValues.Add(Database.Fields.Get("EOSZ"), (int)effectiveOperandSize.GetValueOrDefault());

			// Instruction pattern fields
			foreach (var blot in instruction.Forms[formIndex].Pattern)
			{
				if (blot.Type != XedBlotType.Equality || blot.Field.EncoderUsage != XedFieldUsage.Input)
					continue;
				fieldValues[blot.Field] = blot.Value.Constant;
			}

			var traceListeners = TraceMessage;
			if (traceListeners != null)
			{
				var str = new StringBuilder();
				str.Append("Assign ");
				foreach (var entry in fieldValues.OrderBy(pair => pair.Key.Name))
					str.Append(entry.Key.Name).Append('=').Append(entry.Value).Append(' ');
				traceListeners(executionDepth, str.ToString());
			}

			// Operands
			foreach (var operand in instruction.Forms[formIndex].Operands)
			{
				throw new NotImplementedException();
			}

			if (!Database.EncodeSequences.TryFind("ISA_ENCODE", out XedSequence rootSequence))
				throw new InvalidOperationException();

			executionDepth = 0;
			try { Execute(rootSequence); }
			finally { executionDepth = 0; }

			bitStream.Flush();
			return memoryStream.ToArray();
		}

		private void Execute(XedSequence sequence)
		{
			string GetSuffixStr(bool isBinding) => isBinding ? "BIND" : "EMIT";
			TraceMessage?.Invoke(executionDepth, $"Sequence {sequence.Name} ({GetSuffixStr(isBinding)})");

			executionDepth++;
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
					string patternName = entry.TargetName;
					if (patternName.EndsWith("_BIND"))
					{
						patternName = patternName.Substring(0, patternName.Length - "_BIND".Length);
						isBinding = true;
					}
					else if (patternName.EndsWith("_EMIT"))
					{
						patternName = patternName.Substring(0, patternName.Length - "_EMIT".Length);
						isBinding = false;
					}

					if (ExecutePattern(patternName))
						throw new InvalidOperationException(); // Can't reset encoder
				}
				else
					throw new InvalidOperationException();
			}

			executionDepth--;
		}

		private bool ExecutePattern(string name)
		{
			var pattern = Database.FindPattern(name, encode: IsEncoding);
			if (pattern == null) throw new InvalidOperationException();
			return Execute(pattern);
		}

		private bool Execute(XedPattern pattern)
		{
			if (pattern is XedRulePattern rulePattern)
				return Execute(rulePattern);
			else if (pattern is XedInstructionTable instructionTable)
				return Execute(instructionTable);
			else
				throw new InvalidOperationException();
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

			TraceMessage?.Invoke(executionDepth, $"Rule pattern {rulePattern.Name}()");
			executionDepth++;

			bool? reset = null;
			foreach (var @case in rulePattern.Cases)
			{
				var controlFlow = TryExecuteRuleCase(@case, ref register);
				if (!rulePattern.ReturnsRegister && register.HasValue)
					throw new InvalidOperationException();

				if (controlFlow == XedRulePatternControlFlow.Continue) continue;

				if (controlFlow == XedRulePatternControlFlow.Break)
				{
					reset = false;
					break;
				}

				if (controlFlow == XedRulePatternControlFlow.Reset)
				{
					reset = true;
					break;
				}

				throw new UnreachableException();
			}
			
			if (!reset.HasValue) throw new InvalidOperationException();

			executionDepth--;
			return reset.Value;
		}

		private XedRulePatternControlFlow TryExecuteRuleCase(
			XedRulePatternCase @case, ref ushort? register)
		{
			var bitVars = new SmallDictionary<char, XedBitsValue>();

			if (IsDecoding ^ @case.IsEncode)
			{
				if (!TryMatchRuleCaseCondition(@case.Lhs, IsEncoding ? register : null, bitVars))
					return XedRulePatternControlFlow.Continue;

				TraceMessage?.Invoke(executionDepth, $"Case '{@case}'");
				executionDepth++;

				var outReg = ExecuteRuleCaseActions(@case.Rhs, bitVars);
				if (outReg.HasValue)
				{
					if (IsEncoding) throw new InvalidOperationException();
					register = outReg;
				}

				executionDepth--;
			}
			else if (IsEncoding && !@case.IsEncode)
			{
				// Right-to-left match
				if (!TryMatchRuleCaseCondition(@case.Rhs, register, bitVars)
					|| !TryMatchRuleCaseCondition(@case.Lhs, null, bitVars))
					return XedRulePatternControlFlow.Continue;

				TraceMessage?.Invoke(executionDepth, $"Case '{@case}'");
				executionDepth++;

				var outReg = ExecuteActionBlots(@case.Lhs,
					b => b.Type == XedBlotType.Equality && b.Field.EncoderUsage == XedFieldUsage.Output,
					bitVars);
				if (outReg.HasValue)
				{
					if (IsEncoding) throw new InvalidOperationException();
					register = outReg;
				}

				executionDepth--;
			}
			else throw new InvalidOperationException();

			return @case.ControlFlow;
		}

		private bool TryMatchRuleCaseCondition(ImmutableArray<XedBlot> blots, ushort? register,
			IDictionary<char, XedBitsValue> bitVars)
		{
			long? originalBitStreamPosition = null;
			foreach (var blot in blots)
			{
				bool isMatch;
				if (blot.Type == XedBlotType.Bits)
				{
					if (!originalBitStreamPosition.HasValue) originalBitStreamPosition = bitStream.Position;
					if (IsEncoding)
					{
						ulong bits = (ulong)fieldValues[blot.Field];
						AssignBitVars(blot.BitPattern, bits, bitVars);
						isMatch = true;
					}
					else
					{
						isMatch = TryMatchBits(blot.BitPattern, blot.Field, bitVars);
					}
				}
				else if (blot.Type == XedBlotType.Equality || blot.Type == XedBlotType.Inequality)
				{
					if (IsEncoding && blot.Field.EncoderUsage == XedFieldUsage.Output) continue;
					bool isEquals = blot.Type == XedBlotType.Equality;
					isMatch = MatchPredicateBlot(blot.Field, blot.Value, isEquals, register);
				}
				else if (blot.Type == XedBlotType.Call)
					throw new InvalidOperationException();
				else
					throw new UnreachableException();

				if (!isMatch)
				{
					if (originalBitStreamPosition.HasValue)
						bitStream.Position = originalBitStreamPosition.Value;
					return false;
				}
			}

			return true;
		}

		private void AssignBitVars(string pattern, ulong bits, IDictionary<char, XedBitsValue> bitVars)
		{
			int startIndex = 0;
			while (startIndex < pattern.Length)
			{
				var span = XedBitPattern.GetSpanAt(pattern, startIndex);
				if (span.IsConstant) throw new InvalidOperationException();

				var bitVarValue = (bits >> (pattern.Length - span.EndIndex))
					& ((1UL << span.Length) - 1);
				bitVars.Add(span.Char, new XedBitsValue(bitVarValue, span.Length));

				startIndex = span.EndIndex;
			}
		}

		private bool TryMatchBits(string pattern, XedField field, IDictionary<char, XedBitsValue> bitVars)
		{
			if (IsEncoding || field.DecoderUsage != XedFieldUsage.Output)
				throw new InvalidOperationException();

			var bitsFieldType = field.Type as XedBitsFieldType;
			if (bitsFieldType == null) throw new InvalidOperationException();
			if (bitsFieldType.SizeInBits != pattern.Length) throw new InvalidOperationException();

			ulong fieldBits = bitStream.ReadRightAlignedBits((byte)pattern.Length);

			// Match and store bitvars
			int startIndex = 0;
			while (startIndex < pattern.Length)
			{
				var span = XedBitPattern.GetSpanAt(pattern, startIndex);

				var actualBits = (fieldBits >> (pattern.Length - span.EndIndex))
					& ((1UL << span.Length) - 1);

				if (span.IsConstant)
				{
					var expectedBits = span.Char == '0' ? 0UL : ((1UL << span.Length) - 1);
					if (actualBits != expectedBits) return false;
				}
				else
				{
					bitVars.Add(span.Char, new XedBitsValue(actualBits, span.Length));
				}

				startIndex = span.EndIndex;
			}

			// FIXME: we don't want to assign the field unless the entire blot condition passes
			fieldValues.Add(field, (long)fieldBits);
			return true;
		}
		
		private void ProduceBits(XedField field, string pattern, IDictionary<char, XedBitsValue> bitVars)
		{
			ulong value;
			if (field == null || XedBitPattern.IsConstant(pattern))
			{
				value = XedBitPattern.Evaluate(pattern, bitVars.GetValue).Bits;
				if (field != null) fieldValues[field] = (long)value;
			}
			else
			{
				if (!(field.Type is XedBitsFieldType) || field.SizeInBits != pattern.Length)
					throw new InvalidOperationException();

				value = (ulong)fieldValues[field];
				AssignBitVars(pattern, value, bitVars);
			}

			bitStream.WriteRightAlignedBits(value, (byte)pattern.Length);
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

		private long Evaluate(XedBlotValue value, ushort? outReg,
			IDictionary<char, XedBitsValue> bitVars = null)
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
				return (long)XedBitPattern.Evaluate(value.BitPattern, bitVars.GetValue).Bits;
			}
			else
				throw new UnreachableException();
		}

		private ushort? ExecuteRuleCaseActions(ImmutableArray<XedBlot> blots,
			IDictionary<char, XedBitsValue> bitVars)
		{
			return IsDecoding ? ExecuteActionBlots(blots, b => true, bitVars)
				: isBinding ? ExecuteActionBlots(blots, b => b.Type != XedBlotType.Bits, bitVars)
				: ExecuteActionBlots(blots, b => b.Type == XedBlotType.Bits, bitVars);
		}

		private ushort? ExecuteActionBlots(ImmutableArray<XedBlot> blots, Predicate<XedBlot> filter,
			IDictionary<char, XedBitsValue> bitVars)
		{
			ushort? outReg = null;
			foreach (var blot in blots)
			{
				if (!filter(blot)) continue;

				switch (blot.Type)
				{
					case XedBlotType.Bits:
						if (!IsEncoding) throw new InvalidOperationException();
						ProduceBits(blot.Field, blot.BitPattern, bitVars);
						break;

					case XedBlotType.Equality:
						{
							var value = Evaluate(blot.Value, outReg: null, bitVars);
							TraceMessage?.Invoke(executionDepth, $"Assign {blot.Field.Name}={value}");
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
			TraceMessage?.Invoke(executionDepth, $"Instruction table {instructionTable.Name}()");
			executionDepth++;

			if (IsEncoding)
			{
				if (!instructionTable.Instructions.Contains(encodingInstruction))
					throw new InvalidOperationException();

				TraceMessage?.Invoke(executionDepth, $"Instruction {encodingInstruction.Class}");
				executionDepth++;

				var outRegister = ExecuteActionBlots(EncodingInstructionForm.Pattern,
					b => b.Field == null || b.Field.EncoderUsage == XedFieldUsage.Output, bitVars: null);
				if (outRegister != null) throw new InvalidOperationException();

				executionDepth--;
			}
			else
			{
				throw new NotImplementedException();
			}

			executionDepth--;
			return false;
		}
	}
}
