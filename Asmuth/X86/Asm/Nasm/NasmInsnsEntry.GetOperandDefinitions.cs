﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Asmuth.X86.Asm.Nasm
{
    partial class NasmInsnsEntry
    {
		public OperandDefinition[] GetOperandDefinitions(IntegerSize? operandSize = null)
		{
			var specs = GetOperandSpecs(operandSize);
			var defs = new OperandDefinition[specs.Length];
			for (int i = 0; i < specs.Length; ++i)
				defs[i] = new OperandDefinition(specs[i], Operands[i].Field);
			return defs;
		}

		public OperandSpec[] GetOperandSpecs(IntegerSize? operandSize = null)
		{
			if (operandSize.HasValue)
				return GetOperandSpecs(defaultSizeInBytes: operandSize.Value.InBytes(), sizeMatch: false);

			foreach (string flag in Flags)
			{
				if (flag == NasmInstructionFlags.SizeMatch)
					return GetOperandSpecs(defaultSizeInBytes: null, sizeMatch: true);
				else if (flag == NasmInstructionFlags.SizeMatchFirstTwo)
					throw new NotImplementedException("NASM size match first two.");
				else
				{
					var defaultSizeInBytes = NasmInstructionFlags.TryAsDefaultOperandSizeInBytes(flag);
					if (defaultSizeInBytes.HasValue)
						return GetOperandSpecs(defaultSizeInBytes.Value, sizeMatch: false);
				}
			}

			return GetOperandSpecs(defaultSizeInBytes: null, sizeMatch: false);
		}

		private OperandSpec[] GetOperandSpecs(int? defaultSizeInBytes, bool sizeMatch)
		{
			Debug.Assert(!defaultSizeInBytes.HasValue || !sizeMatch);

			// ADD  reg_al,imm      [-i:  04 ib]         8086,SM
			// ADD  rm8,imm         [mi:  hle 80 / 0 ib] 8086,SM,LOCK
			// CMP  mem,imm32       [mi:  o32 81 /7 id]  386,SM
			// IMUL reg64,mem,imm32 [rmi: o64 69 /r id]  X64,SM

			var operandFormats = new OperandSpec[Operands.Count];

			int? impliedSizeInBytes = null;
			for (int i = 0; i < Operands.Count; ++i)
			{
				var operandFormat = TryToOperandSpec(i, defaultSizeInBytes);
				if (operandFormat == null)
				{
					if (!sizeMatch) throw new FormatException("Unspecified operand size.");
					// If we're size matching, allow null as we'll do a second pass
				}
				else
				{
					operandFormats[i] = operandFormat;

					// Determine the size we'll match on the second pass
					if (sizeMatch && !impliedSizeInBytes.HasValue)
					{
						var operandSize = operandFormat.ImpliedIntegerOperandSize;
						if (operandSize.HasValue) impliedSizeInBytes = operandSize.Value.InBytes();
					}
				}
			}

			if (sizeMatch)
			{
				// Do a second pass with the implied size
				if (!impliedSizeInBytes.HasValue) throw new FormatException();
				return GetOperandSpecs(impliedSizeInBytes.Value, sizeMatch: false);
			}

			return operandFormats;
		}

		private OperandSpec TryToOperandSpec(int operandIndex, int? defaultSizeInBytes)
		{
			var operand = Operands[operandIndex];

			if (operand.Type == NasmOperandType.Imm && !defaultSizeInBytes.HasValue)
			{
				// Attempt to use the encoding tokens to determine the immediate size

				// Determine which immediate of potentially two immediates this is
				int immediateIndex;
				if (operand.Field == OperandField.Immediate) immediateIndex = 0;
				else if (operand.Field == OperandField.SecondImmediate) immediateIndex = 1;
				else throw new UnreachableException();

				int immediateCount = Operands.Any(o => o.Field == OperandField.SecondImmediate) ? 2 : 1;
				
				// Assume trailing encoding tokens map one-to-one to immediates
				var tokenType = EncodingTokens[EncodingTokens.Count - (immediateCount - immediateIndex)].Type;
				switch (tokenType)
				{
					case NasmEncodingTokenType.Immediate_Byte:
					case NasmEncodingTokenType.Immediate_Byte_Signed:
					case NasmEncodingTokenType.Immediate_Byte_Unsigned:
					case NasmEncodingTokenType.Immediate_RelativeOffset8:
					case NasmEncodingTokenType.Immediate_Is4:
						defaultSizeInBytes = 1;
						break;

					case NasmEncodingTokenType.Immediate_Word:
						defaultSizeInBytes = 2;
						break;

					case NasmEncodingTokenType.Immediate_Dword:
					case NasmEncodingTokenType.Immediate_Dword_Signed:
						defaultSizeInBytes = 4;
						break;

					case NasmEncodingTokenType.Immediate_Qword:
						defaultSizeInBytes = 8;
						break;
				}
			}

			return operand.TryToOperandSpec(defaultSizeInBytes);
		}
	}
}