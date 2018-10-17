using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Asmuth.X86.Nasm
{
	partial class NasmInsnsEntry
	{
		public struct OpcodeEncodingConversionParams
		{
			public bool? X64 { get; set; } // Unspecified by encoding tokens
			public AddressSize? AddressSize { get; set; } // For mem_offs
			public ConditionCode? ConditionCode { get; set; } // For +cc opcodes
			public IntegerSize? OperandSize { get; set; } // For iwd/iwdq immediates
			public bool HasMOffs { get; set; }
			public ModRMModEncoding ModEncoding { get; set; } // For /r disambiguation

			public void SetRMFlagsFromOperandType(NasmOperandType type)
			{
				var baseRegOpType = type & NasmOperandType.OpType_Mask;
				if (baseRegOpType == NasmOperandType.OpType_Register)
					ModEncoding = ModRMModEncoding.Register;
				else if (baseRegOpType == NasmOperandType.OpType_Memory)
					ModEncoding = ModRMModEncoding.Memory;
				else
					ModEncoding = ModRMModEncoding.RegisterOrMemory;
			}
		}

		public bool CanConvertToOpcodeEncoding
		{
			get
			{
				return !IsPseudo && !IsAssembleOnly
					// Instructions specific to the (obscure) CYRIX processors,
					// and which clash with saner intel/amd ones.
					&& !Flags.Contains("CYRIX", StringComparer.InvariantCultureIgnoreCase)
					// Undocumented instructions somehow may clash with documented ones
					&& !Flags.Contains(NasmInstructionFlags.Undocumented, StringComparer.InvariantCultureIgnoreCase)
					// This thing is weird, but it looks always accompanied with fixed operand size variants,
					// so we can ignore it.
					&& !EncodingTokens.Contains(NasmEncodingTokenType.OperandSize_NoOverride)
					// Wait prefix is a separate prefix opcode so we don't handle it,
					// it is for macro-like FINIT instructions which expand to FWAIT, FNINIT
					&& (!EncodingTokens.Contains(NasmEncodingTokenType.Misc_WaitPrefix) || EncodingTokens.Count == 1)
					// (Apparently) all FPU opcodes implicitly referencing ST0 appear twice,
					// once with the explicit ST0 operand and once without (ie one can omit it when assembling).
					// To avoid duplicates, ignore it here. (TODO: better ignore the other one...)
					&& Operands.All(o => o.Type != NasmOperandType.Fpu0);
			}
		}

		// Jcc, SETcc, MOVcc
		public bool HasConditionCodeVariants
			=> EncodingTokens.Any(t => t.Type == NasmEncodingTokenType.Byte_PlusConditionCode);

		public bool GetAddressAndOperandSizeVariantCounts(
			out int addressSizeVariantCount, out int operandSizeVariantCount)
		{
			addressSizeVariantCount = 1;
			operandSizeVariantCount = 1;

			// Do a first pass to locate the iwd/iwdq immediates
			// and sort them as address or operand size-dependent.
			OperandField immediateField = OperandField.Immediate;
			for (int i = 0; i < EncodingTokens.Count; ++i)
			{
				var tokenType = EncodingTokens[i].Type;
				if ((tokenType & NasmEncodingTokenType.Category_Mask) != NasmEncodingTokenType.Category_Immediate
					|| tokenType == NasmEncodingTokenType.Immediate_Segment)
					continue;

				int variantCount = 1;
				if (tokenType == NasmEncodingTokenType.Immediate_WordOrDword) variantCount = 2;
				else if (tokenType == NasmEncodingTokenType.Immediate_WordOrDwordOrQword) variantCount = 3;

				if (variantCount > 1)
				{
					Debug.Assert(immediateField <= OperandField.SecondImmediate);
					bool isMOffs = FindOperand(immediateField).Value.Type == NasmOperandType.Mem_Offs;
					if (isMOffs) addressSizeVariantCount = Math.Max(addressSizeVariantCount, variantCount);
					else operandSizeVariantCount = Math.Max(operandSizeVariantCount, variantCount);
				}

				++immediateField;
			}

			// Do a first pass to find explicit address/operand size tokens
			for (int i = 0; i < EncodingTokens.Count; ++i)
			{
				var tokenType = EncodingTokens[i].Type;
				if (tokenType == NasmEncodingTokenType.AddressSize_16
					|| tokenType == NasmEncodingTokenType.AddressSize_32
					|| tokenType == NasmEncodingTokenType.AddressSize_64)
					addressSizeVariantCount = 1;
				else if (tokenType == NasmEncodingTokenType.OperandSize_16
					|| tokenType == NasmEncodingTokenType.OperandSize_32
					|| tokenType == NasmEncodingTokenType.OperandSize_64
					|| tokenType == NasmEncodingTokenType.OperandSize_64WithoutW)
					operandSizeVariantCount = 1;
			}

			return addressSizeVariantCount * operandSizeVariantCount > 1;
		}

		public OpcodeEncoding GetOpcodeEncoding(
			AddressSize? addressSize = null,
			IntegerSize? operandSize = null,
			ConditionCode? conditionCode = null)
		{
			if (!CanConvertToOpcodeEncoding) throw new InvalidOperationException();

			var @params = new OpcodeEncodingConversionParams
			{
				AddressSize = addressSize,
				OperandSize = operandSize,
				ConditionCode = conditionCode,
				HasMOffs = Operands.Any(o => o.Type == NasmOperandType.Mem_Offs)
			};
			
			// Fill up the long mode flag from the instruction flags
			var longMode = HasFlag(NasmInstructionFlags.LongMode);
			var noLongMode = HasFlag(NasmInstructionFlags.NoLongMode);
			if (longMode && noLongMode) throw new FormatException("Long and no-long mode specified.");
			if (longMode) @params.X64 = true;
			else if (noLongMode) @params.X64 = false;
			
			// Fill up the RM reg vs mem from the operands
			foreach (var operand in Operands)
			{
				if (operand.Field == OperandField.BaseReg)
				{
					@params.SetRMFlagsFromOperandType(operand.Type);
					break;
				}
			}

			return ToOpcodeEncoding(EncodingTokens, VexEncoding, in @params);
		}

		public static OpcodeEncoding ToOpcodeEncoding(
			IEnumerable<NasmEncodingToken> encodingTokens, VexEncoding? vexEncoding,
			in OpcodeEncodingConversionParams @params)
		{
			if (encodingTokens == null) throw new ArgumentNullException(nameof(encodingTokens));

			var parser = new EncodingParser();
			return parser.Parse(encodingTokens, vexEncoding, in @params);
		}

		private struct EncodingParser
		{
			private enum State
			{
				Prefixes,
				PostSimdPrefix,
				PostEscape0F,
				PreOpcode,
				PostOpcode,
				PostModRM,
				Immediates
			}

			private OpcodeEncoding.Builder builder;
			private IntegerSize? operandSize;
			private State state;

			public OpcodeEncoding Parse(IEnumerable<NasmEncodingToken> tokens, VexEncoding? vexEncoding,
				in OpcodeEncodingConversionParams @params)
			{
				state = State.Prefixes;

				if (@params.X64.HasValue) SetX64(@params.X64.Value);
				if (@params.AddressSize.HasValue) SetAddressSize(@params.AddressSize.Value);
				if (@params.OperandSize.HasValue) SetOperandSize(@params.OperandSize.Value);

				foreach (var token in tokens)
				{
					switch (token.Type)
					{
						case NasmEncodingTokenType.Vex: SetVex(vexEncoding); break;

						case NasmEncodingTokenType.AddressSize_16: SetAddressSize(AddressSize._16); break;
						case NasmEncodingTokenType.AddressSize_32: SetAddressSize(AddressSize._32); break;
						case NasmEncodingTokenType.AddressSize_64: SetAddressSize(AddressSize._64); break;
						case NasmEncodingTokenType.AddressSize_NoOverride: break; // ?

						case NasmEncodingTokenType.OperandSize_16: SetOperandSize(IntegerSize.Word); break;
						case NasmEncodingTokenType.OperandSize_32: SetOperandSize(IntegerSize.Dword); break;
						case NasmEncodingTokenType.OperandSize_64: SetOperandSize(IntegerSize.Qword); break;

						// W-agnostic, like JMP: o64nw e9 rel64
						case NasmEncodingTokenType.OperandSize_64WithoutW: SetOperandSize(IntegerSize.Qword, optPromotion: true); break;

						// Legacy prefixes
						case NasmEncodingTokenType.LegacyPrefix_NoSimd:
							SetSimdPrefix(SimdPrefix.None);
							break;

						case NasmEncodingTokenType.LegacyPrefix_F2:
							SetSimdPrefix(SimdPrefix._F2);
							break;

						case NasmEncodingTokenType.LegacyPrefix_MustRep:
						case NasmEncodingTokenType.LegacyPrefix_F3:
							SetSimdPrefix(SimdPrefix._F3);
							break;

						case NasmEncodingTokenType.LegacyPrefix_NoF3:
						case NasmEncodingTokenType.LegacyPrefix_HleAlways:
						case NasmEncodingTokenType.LegacyPrefix_HleWithLock:
						case NasmEncodingTokenType.LegacyPrefix_XReleaseAlways:
						case NasmEncodingTokenType.LegacyPrefix_DisassembleRepAsRepE:
						case NasmEncodingTokenType.LegacyPrefix_NoRep:
							break;

						// Rex
						case NasmEncodingTokenType.Rex_NoB:
						case NasmEncodingTokenType.Rex_NoW: // TODO: handle this?
						case NasmEncodingTokenType.Rex_LockAsRexR: // TODO: implies operand size 32
							break;

						case NasmEncodingTokenType.Byte:
							if (state < State.PostSimdPrefix)
							{
								switch (token.Byte)
								{
									case 0x66: SetSimdPrefix(SimdPrefix._66); continue;
									case 0xF2: SetSimdPrefix(SimdPrefix._F2); continue;
									case 0xF3: SetSimdPrefix(SimdPrefix._F3); continue;
								}
							}

							if (state < State.PostEscape0F && builder.VexType == VexType.None && token.Byte == 0x0F)
							{
								builder.Map = OpcodeMap.Escape0F;
								AdvanceTo(State.PostEscape0F);
								break;
							}

							if (state == State.PostEscape0F && (token.Byte == 0x38 || token.Byte == 0x3A))
							{
								builder.Map = token.Byte == 0x38 ? OpcodeMap.Escape0F38 : OpcodeMap.Escape0F3A;
								AdvanceTo(State.PreOpcode);
								break;
							}

							if (state < State.PostOpcode)
							{
								SetMainOpcodeByte(token.Byte, plusR: false);
								break;
							}

							// Heuristic: if it's of the form 0b11000000,
							// it's probably a fixed ModRM, otherwise a fixed imm8
							if (state == State.PostOpcode && builder.AddressingForm.IsNone
								&& (token.Byte >> 6) == 3)
							{
								builder.AddressingForm = ModRMEncoding.FromFixedValue((ModRM)token.Byte);
								break;
							}

							// Opcode extension byte
							Debug.Assert(state == State.PostOpcode || state == State.PostModRM);
							if (builder.ImmediateSize.IsNonZero)
								throw new FormatException("Imm8 extension with other intermediates.");
							builder.ImmediateSize = ImmediateSizeEncoding.Byte;
							builder.Imm8Ext = token.Byte;
							break;

						case NasmEncodingTokenType.Byte_PlusRegister:
							Debug.Assert((token.Byte & 7) == 0);
							if (state < State.PostOpcode)
							{
								SetMainOpcodeByte(token.Byte, plusR: true);
								break;
							}

							if (state < State.PostModRM)
							{
								SetAddressingForm(new ModRMEncoding(
									ModRMModEncoding.Register,
									reg: ((ModRM)token.Byte).Reg));
								break;
							}

							throw new FormatException();

						case NasmEncodingTokenType.Byte_PlusConditionCode:
							SetMainOpcodeBytePlusConditionCode(token.Byte, @params.ConditionCode);
							break;

						case NasmEncodingTokenType.ModRM:
							SetAddressingForm(new ModRMEncoding(@params.ModEncoding));
							break;

						case NasmEncodingTokenType.ModRM_FixedReg:
							SetAddressingForm(new ModRMEncoding(@params.ModEncoding, reg: token.Byte));
							break;

						// Immediates
						case NasmEncodingTokenType.Immediate_Byte:
						case NasmEncodingTokenType.Immediate_Byte_Signed:
						case NasmEncodingTokenType.Immediate_Byte_Unsigned:
						case NasmEncodingTokenType.Immediate_RelativeOffset8:
							AddImmediate(ImmediateSizeEncoding.Byte);
							break;

						case NasmEncodingTokenType.Immediate_Word:
						case NasmEncodingTokenType.Immediate_Segment: // TODO: Make sure this happens in the right order
							AddImmediate(ImmediateSizeEncoding.Word);
							break;

						case NasmEncodingTokenType.Immediate_Dword:
						case NasmEncodingTokenType.Immediate_Dword_Signed:
							AddImmediate(ImmediateSizeEncoding.Dword);
							break;

						case NasmEncodingTokenType.Immediate_WordOrDword:
							AddVariableSizedImmediate(ImmediateVariableSize.WordOrDword_OperandSize);
							break;

						case NasmEncodingTokenType.Immediate_WordOrDwordOrQword:
							AddVariableSizedImmediate(@params.HasMOffs
								? ImmediateVariableSize.WordOrDwordOrQword_AddressSize
								: ImmediateVariableSize.WordOrDwordOrQword_OperandSize);
							break;

						case NasmEncodingTokenType.Immediate_Qword:
							AddImmediate(ImmediateSizeEncoding.Qword);
							break;

						case NasmEncodingTokenType.Immediate_RelativeOffset:
							if (builder.X64 == true || builder.OperandSize == OperandSizeEncoding.Dword)
								AddImmediate(ImmediateSizeEncoding.Dword);
							else if (builder.OperandSize == OperandSizeEncoding.Word)
								AddImmediate(ImmediateSizeEncoding.Word);
							else
								throw new FormatException("Ambiguous relative offset size.");
							break;

						case NasmEncodingTokenType.Immediate_Is4:
							if (builder.ImmediateSize.IsNonZero)
								throw new FormatException("Imm8 extension must be the only immediate.");
							AddImmediate(ImmediateSizeEncoding.Byte);
							break;

						// Jump, it's not clear what additional info these provides so skip
						case NasmEncodingTokenType.Jump_8:
						case NasmEncodingTokenType.Jump_Conditional8:
						case NasmEncodingTokenType.Jump_Length:
							break;

						// Misc
						case NasmEncodingTokenType.VectorSib_XmmDwordIndices:
						case NasmEncodingTokenType.VectorSib_XmmQwordIndices:
						case NasmEncodingTokenType.VectorSib_YmmDwordIndices:
						case NasmEncodingTokenType.VectorSib_YmmQwordIndices:
						case NasmEncodingTokenType.VectorSib_Xmm:
						case NasmEncodingTokenType.VectorSib_Ymm:
						case NasmEncodingTokenType.VectorSib_Zmm:
							break; // doesn't impact encoding

						case NasmEncodingTokenType.Misc_WaitPrefix:
							if (tokens.Count() == 1)
							{
								// Special case for FWAIT since the NASM file
								// doesn't explicitly specify the main opcode byte.
								SetMainOpcodeByte(0x9B, plusR: false);
							}
							else
							{
								// The wait prefix is a separate instruction,
								// so we don't account for it in the encoding.
							}
							break;

						case NasmEncodingTokenType.Misc_NoHigh8Register:
						case NasmEncodingTokenType.Misc_Resb:
							break;

						default:
							throw new NotImplementedException($"Unimplemented NASM encoding token handling: {token.Type}.");
					}
				}
				
				return builder.Build();
			}

			private void SetMainOpcodeBytePlusConditionCode(byte value, ConditionCode? conditionCode)
			{
				if (!conditionCode.HasValue)
					throw new FormatException("Cannot convert to opcode encoding without condition code.");
				if ((value & 0xF) != 0) throw new FormatException();

				SetMainOpcodeByte((byte)(value | (byte)conditionCode.Value), plusR: false);
			}

			private void SetVex(VexEncoding? vexEncoding)
			{
				if (!vexEncoding.HasValue)
					throw new FormatException("VEX-encoded NASM instruction without a provided VEX encoding.");
				if (builder.VexType != VexType.None)
					throw new FormatException("VEX may only be the first token.");

				builder.VexType = vexEncoding.Value.Type;
				builder.VectorSize = vexEncoding.Value.VectorSize;
				builder.SimdPrefix = vexEncoding.Value.SimdPrefix;
				builder.OperandSizePromotion = vexEncoding.Value.OperandSizePromotion;
				builder.Map = vexEncoding.Value.OpcodeMap;
				AdvanceTo(State.PreOpcode);
			}
			
			private void AddVariableSizedImmediate(ImmediateVariableSize variableSize)
			{
				var size = new ImmediateSizeEncoding(variableSize);

				// See if we can resolve the variable size
				if (variableSize.IsAddressSizeDependent() && builder.AddressSize.HasValue)
				{
					int inBytes = variableSize.InBytes(builder.AddressSize.Value, default);
					size = ImmediateSizeEncoding.FromBytes(inBytes);
				}
				else if (variableSize.IsOperandSizeDependent())
				{
					if (builder.OperandSize == OperandSizeEncoding.Word)
						size = ImmediateSizeEncoding.Word;
					else if (builder.OperandSize == OperandSizeEncoding.Dword)
						size = ImmediateSizeEncoding.Dword;
					else if (builder.OperandSizePromotion.HasValue)
					{
						int inBytes = variableSize.InBytes(default, IntegerSize.Qword);
						size = ImmediateSizeEncoding.FromBytes(inBytes);
					}
				}

				AddImmediate(size);
			}

			private void SetAddressSize(AddressSize size)
			{
				if (state != State.Prefixes) throw new FormatException("Out-of-order address size token.");
				if (builder.AddressSize.HasValue) throw new FormatException("Multiple address size tokens.");

				builder.AddressSize = size;
				if (size == AddressSize._16) SetX64(false);
				else if (size == AddressSize._64) SetX64(true);
			}

			private void SetX64(bool value)
			{
				if (builder.X64.GetValueOrDefault(value) != value)
					throw new FormatException("Conflicting long mode flag.");
				builder.X64 = value;
			}
			
			private void SetOperandSize(IntegerSize size, bool optPromotion = false)
			{
				if (operandSize.HasValue) throw new FormatException("Multiple operand size specification.");

				operandSize = size;
				switch (size)
				{
					case IntegerSize.Word:
						builder.OperandSize = OperandSizeEncoding.Word;
						SetX64(false);
						break;

					case IntegerSize.Dword:
						builder.OperandSize = OperandSizeEncoding.Dword;
						break;

					case IntegerSize.Qword:
						if (!optPromotion) SetOperandSizePromotion(true);
						SetX64(true);
						break;

					default: throw new ArgumentException();
				}
			}

			private void SetOperandSizePromotion(bool set)
			{
				if (builder.OperandSizePromotion.GetValueOrDefault(set) != set)
					throw new FormatException("Conflicting operand size promotion encoding.");

				builder.OperandSizePromotion = set;
				if (set) SetX64(true);
			}

			private void SetSimdPrefix(SimdPrefix prefix)
			{
				if (state >= State.PostSimdPrefix) throw new FormatException("Out-of-order SIMD prefix.");
				Debug.Assert(!builder.SimdPrefix.HasValue);
				builder.SimdPrefix = prefix;
				AdvanceTo(State.PostSimdPrefix);
			}

			private void SetMainOpcodeByte(byte value, bool plusR)
			{
				if (state >= State.PostOpcode) throw new FormatException("Out-of-order opcode token.");
				builder.MainByte = value;
				if (plusR) builder.AddressingForm = AddressingFormEncoding.MainByteEmbeddedRegister;
				AdvanceTo(State.PostOpcode);
			}
			
			private void SetAddressingForm(AddressingFormEncoding encoding)
			{
				if (state != State.PostOpcode) throw new FormatException("Out-of-order ModRM token.");
				if (!builder.AddressingForm.IsNone) throw new FormatException("Multiple ModRMs.");
				builder.AddressingForm = encoding;
				AdvanceTo(State.PostModRM);
			}

			private void AddImmediate(ImmediateSizeEncoding size)
			{
				if (state < State.PostOpcode) throw new FormatException("Out-of-order immediate token.");
				if (builder.Imm8Ext.HasValue) throw new FormatException();
				builder.ImmediateSize = ImmediateSizeEncoding.Combine(builder.ImmediateSize, size);
				AdvanceTo(State.Immediates);
			}

			private void AdvanceTo(State newState)
			{
				Debug.Assert(newState >= state);
				state = newState;
			}
		}
	}
}
