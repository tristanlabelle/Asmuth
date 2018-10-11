using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;

namespace Asmuth.X86.Xed
{
	public sealed class XedEngine
	{
		private readonly struct BitVariableValue
		{
			public ulong Bits { get; }
			public int Length { get; }

			public BitVariableValue(ulong bits, int length)
			{
				this.Bits = bits;
				this.Length = length;
			}
		}

		public XedDatabase Database { get; }
		private readonly Dictionary<XedField, long> fieldValues = new Dictionary<XedField, long>();
		private BitStream bitStream;
		private XedInstruction encodingInstruction;
		private byte encodingInstructionFormIndex;
		private bool isBinding;

		public XedEngine(XedDatabase database)
		{
			this.Database = database ?? throw new ArgumentNullException(nameof(database));
		}

		public event Action<string> TraceMessage;
		
		private bool IsEncoding => encodingInstruction != null;
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

			// Set up field values
			fieldValues.Add(Database.Fields.Get("MODE"), (int)codeSegmentType);
			fieldValues.Add(Database.Fields.Get("EASZ"),
				effectiveAddressSize.HasValue ? (int)effectiveAddressSize.Value + 1 : 0);
			fieldValues.Add(Database.Fields.Get("EOSZ"), (int)effectiveOperandSize.GetValueOrDefault());

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
			TraceMessage?.Invoke($"Executing sequence {sequence.Name}");

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
			TraceMessage?.Invoke($"Executing rule pattern {rulePattern.Name}");
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
				var controlFlow = TryExecuteRuleCase(@case, ref register);
				if (!rulePattern.ReturnsRegister && register.HasValue)
					throw new InvalidOperationException();
				if (controlFlow == XedRulePatternControlFlow.Break) return false;
				if (controlFlow == XedRulePatternControlFlow.Continue) continue;
				if (controlFlow == XedRulePatternControlFlow.Reset) return true;
				throw new UnreachableException();
			}

			// No case matched
			throw new InvalidOperationException();
		}

		private XedRulePatternControlFlow TryExecuteRuleCase(XedRulePatternCase @case, ref ushort? register)
		{
			var bitVars = new SmallDictionary<char, BitVariableValue>();

			if (!TryMatchRuleCaseCondition(@case.Conditions, IsEncoding ? register : null, bitVars))
				return XedRulePatternControlFlow.Continue;

			TraceMessage?.Invoke($"Matched case '{@case}'");

			var outReg = ExecuteRuleCaseActions(@case.Conditions, bitVars);
			if (outReg.HasValue)
			{
				if (IsEncoding) throw new InvalidOperationException();
				register = outReg;
			}

			return @case.ControlFlow;
		}

		private bool TryMatchRuleCaseCondition(ImmutableArray<XedBlot> blots, ushort? register,
			IDictionary<char, BitVariableValue> bitVars)
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
						DeconstructBits(blot.Field, blot.BitPattern, bitVars);
						isMatch = true;
					}
					else
					{
						isMatch = TryMatchBits(blot.BitPattern, blot.Field, bitVars);
					}
				}
				else if (blot.Type == XedBlotType.Equality)
					isMatch = MatchPredicateBlot(blot.Field, blot.Value, isEquals: true, register);
				else if (blot.Type == XedBlotType.Inequality)
					isMatch = MatchPredicateBlot(blot.Field, blot.Value, isEquals: false, register);
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

		private void DeconstructBits(XedField field, string pattern, IDictionary<char, BitVariableValue> bitVars)
		{
			if (!IsEncoding || field.EncoderUsage != XedFieldUsage.Input)
				throw new InvalidOperationException();

			var bitsFieldType = field.Type as XedBitsFieldType;
			if (bitsFieldType == null) throw new InvalidOperationException();
			if (bitsFieldType.SizeInBits != pattern.Length) throw new InvalidOperationException();

			var fieldBits = (ulong)fieldValues[field];

			int startIndex = 0;
			while (startIndex < pattern.Length)
			{
				var span = XedBitPattern.GetSpanAt(pattern, startIndex);
				if (span.IsConstant) throw new InvalidOperationException();

				var bitVarValue = (fieldBits >> (pattern.Length - span.EndIndex))
					& ((1UL << span.Length) - 1);
				bitVars.Add(span.Char, new BitVariableValue(bitVarValue, span.Length));

				startIndex = span.EndIndex;
			}
		}

		private bool TryMatchBits(string pattern, XedField field, IDictionary<char, BitVariableValue> bitVars)
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
					bitVars.Add(span.Char, new BitVariableValue(actualBits, span.Length));
				}

				startIndex = span.EndIndex;
			}

			// FIXME: we don't want to assign the field unless the entire blot condition passes
			fieldValues.Add(field, (long)fieldBits);
			return true;
		}
		
		private void ProduceBits(string pattern, IDictionary<char, BitVariableValue> bitVars)
		{
			var value = EvaluateBits(pattern, bitVars);
			if (value.Length != pattern.Length) throw new InvalidOperationException();
			bitStream.WriteRightAlignedBits(value.Bits, (byte)value.Length);
		}

		private BitVariableValue EvaluateBits(string pattern, IDictionary<char, BitVariableValue> bitVars)
		{
			ulong bits = 0;

			int startIndex = 0;
			while (startIndex < pattern.Length)
			{
				var span = XedBitPattern.GetSpanAt(pattern, startIndex);

				ulong spanBits;
				if (span.Char == '0') spanBits = 0;
				else if (span.Char == '1') spanBits = ulong.MaxValue;
				else
				{
					var bitVar = bitVars[span.Char];
					if (bitVar.Length != span.Length) throw new InvalidOperationException();
					spanBits = bitVar.Bits;
				}

				bits |= spanBits << (pattern.Length - span.EndIndex);
				startIndex = span.EndIndex;
			}

			return new BitVariableValue(bits, pattern.Length);
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
			IDictionary<char, BitVariableValue> bitVars = null)
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
				return (long)EvaluateBits(value.BitPattern, bitVars).Bits;
			}
			else
				throw new UnreachableException();
		}

		private ushort? ExecuteRuleCaseActions(ImmutableArray<XedBlot> blots,
			IDictionary<char, BitVariableValue> bitVars)
		{
			return ExecuteActionBlots(blots, b => true, bitVars);
		}

		private ushort? ExecuteActionBlots(ImmutableArray<XedBlot> blots, Predicate<XedBlot> filter,
			IDictionary<char, BitVariableValue> bitVars)
		{
			ushort? outReg = null;
			foreach (var blot in blots)
			{
				if (!filter(blot)) continue;

				switch (blot.Type)
				{
					case XedBlotType.Bits:
						if (blot.Field != null || !IsEncoding) throw new InvalidOperationException();
						ProduceBits(blot.BitPattern, bitVars);
						break;

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
			TraceMessage?.Invoke($"Using instruction table {instructionTable.Name}");
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
