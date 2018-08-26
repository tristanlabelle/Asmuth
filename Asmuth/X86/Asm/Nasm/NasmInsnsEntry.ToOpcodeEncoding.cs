using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Asmuth.X86.Asm.Nasm
{
    partial class NasmInsnsEntry
	{
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

		public OpcodeEncoding GetOpcodeEncoding()
		{
			if (!CanConvertToOpcodeEncoding) throw new InvalidOperationException();

			NasmOperandType? baseRegOperandType = null;
			foreach (var operand in Operands)
			{
				if (operand.Field == OperandField.BaseReg)
				{
					baseRegOperandType = operand.Type;
					break;
				}
			}

			return ToOpcodeEncoding(EncodingTokens, VexEncoding.GetValueOrDefault(),
				GetLongMode(Flags.Contains), baseRegOperandType);
		}

		public static bool? GetLongMode(Predicate<string> flagTester)
		{
			var longMode = flagTester(NasmInstructionFlags.LongMode);
			var noLongMode = flagTester(NasmInstructionFlags.NoLongMode);
			if (longMode && noLongMode) throw new FormatException("Long and no-long mode specified.");
			if (noLongMode) return false;
			if (longMode) return true;
			return null;
		}

		public static OpcodeEncoding ToOpcodeEncoding(
			IEnumerable<NasmEncodingToken> encodingTokens,
			VexEncoding? vexEncoding, bool? longMode,
			NasmOperandType? rmOperandType = null)
		{
			if (encodingTokens == null) throw new ArgumentNullException(nameof(encodingTokens));

			var parser = new EncodingParser(longMode);
			return parser.Parse(encodingTokens, vexEncoding.GetValueOrDefault(),
				rmOperandType.GetValueOrDefault(NasmOperandType.OpType_RegisterOrMemory));
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
			private State state;

			public EncodingParser(bool? longMode)
			{
				builder = new OpcodeEncoding.Builder { LongMode = longMode };
				state = State.Prefixes;
			}

			public OpcodeEncoding Parse(IEnumerable<NasmEncodingToken> tokens, VexEncoding vexEncoding,
				NasmOperandType rmOperandType)
			{
				state = State.Prefixes;

				foreach (var token in tokens)
				{
					switch (token.Type)
					{
						case NasmEncodingTokenType.Vex:
							if (builder.VexType != VexType.None)
								throw new FormatException("VEX may only be the first token.");
							builder.VexType = vexEncoding.Type;
							builder.VectorSize = vexEncoding.VectorSize;
							builder.SimdPrefix = vexEncoding.SimdPrefix;
							builder.RexW = vexEncoding.RexW;
							builder.Map = vexEncoding.OpcodeMap;
							AdvanceTo(State.PreOpcode);
							break;

						case NasmEncodingTokenType.AddressSize_Fixed16:
							SetAddressSize(AddressSize._16);
							break;

						case NasmEncodingTokenType.AddressSize_Fixed32:
							SetAddressSize(AddressSize._32);
							break;

						case NasmEncodingTokenType.AddressSize_Fixed64:
							SetAddressSize(AddressSize._64);
							break;

						// ?
						case NasmEncodingTokenType.AddressSize_NoOverride: break;

						case NasmEncodingTokenType.OperandSize_16:
							SetOperandSize(IntegerSize.Word);
							break;

						case NasmEncodingTokenType.OperandSize_32:
							SetOperandSize(IntegerSize.Dword);
							break;

						case NasmEncodingTokenType.OperandSize_64:
							SetOperandSize(IntegerSize.Qword);
							break;

						case NasmEncodingTokenType.OperandSize_64WithoutW:
							// W-agnostic, like JMP: o64nw e9 rel64
							SetLongMode(true);
							break;

						case NasmEncodingTokenType.OperandSize_NoOverride:
							throw new FormatException();

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
							if (state == State.PostOpcode && builder.ModRM == ModRMEncoding.None
								&& (token.Byte >> 6) == 3)
							{
								builder.ModRM = ModRMEncoding.FromFixedValue((ModRM)token.Byte);
								break;
							}

							// Opcode extension byte
							Debug.Assert(state == State.PostOpcode || state == State.PostModRM);
							if (builder.ImmediateSizeInBytes > 0) throw new FormatException("Imm8 extension with other intermediates.");
							builder.Imm8Ext = token.Byte;
							builder.ImmediateSizeInBytes = 1;
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
								SetModRM(ModRMEncoding.FromFixedRegDirectRM(
									reg: ((ModRM)token.Byte).Reg));
								break;
							}

							throw new FormatException();

						case NasmEncodingTokenType.Byte_PlusConditionCode:
							// TODO: figure out what this means: [i:	71+c jlen e9 rel]
							if (state >= State.PostOpcode)
								throw new FormatException("Out-of-order opcode byte.");
							throw new NotImplementedException();

						case NasmEncodingTokenType.ModRM:
							SetModRM(reg: null, rmOperandType);
							break;

						case NasmEncodingTokenType.ModRM_FixedReg:
							SetModRM(reg: token.Byte, rmOperandType);
							break;

						// Immediates
						case NasmEncodingTokenType.Immediate_Byte:
						case NasmEncodingTokenType.Immediate_Byte_Signed:
						case NasmEncodingTokenType.Immediate_Byte_Unsigned:
						case NasmEncodingTokenType.Immediate_RelativeOffset8:
							AddImmediateWithSizeInBytes(1);
							break;

						case NasmEncodingTokenType.Immediate_Word:
						case NasmEncodingTokenType.Immediate_Segment: // TODO: Make sure this happens in the right order
							AddImmediateWithSizeInBytes(2);
							break;

						case NasmEncodingTokenType.Immediate_Dword:
						case NasmEncodingTokenType.Immediate_Dword_Signed:
							AddImmediateWithSizeInBytes(4);
							break;

						case NasmEncodingTokenType.Immediate_WordOrDword:
						case NasmEncodingTokenType.Immediate_WordOrDwordOrQword:
							throw new NotImplementedException("Operand size-dependent immediates not implemented.");

						case NasmEncodingTokenType.Immediate_Qword:
							AddImmediateWithSizeInBytes(8);
							break;

						case NasmEncodingTokenType.Immediate_RelativeOffset:
							if (builder.LongMode == true || builder.OperandSize == OperandSizeEncoding.Dword)
								AddImmediateWithSizeInBytes(sizeof(int));
							else if (builder.OperandSize == OperandSizeEncoding.Word)
								AddImmediateWithSizeInBytes(sizeof(short));
							else
								throw new FormatException("Ambiguous relative offset size.");
							break;

						case NasmEncodingTokenType.Immediate_Is4:
							if (builder.ImmediateSizeInBytes > 0)
								throw new FormatException("Imm8 extension must be the only immediate.");
							AddImmediateWithSizeInBytes(1);
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

			private void SetAddressSize(AddressSize size)
			{
				if (state != State.Prefixes) throw new FormatException("Out-of-order address size token.");
				if (builder.AddressSize.HasValue) throw new FormatException("Multiple address size tokens.");

				builder.AddressSize = size;
				if (size == AddressSize._16) SetLongMode(false);
				else if (size == AddressSize._64) SetLongMode(true);
			}

			private void SetLongMode(bool value)
			{
				if (builder.LongMode.GetValueOrDefault(value) != value)
					throw new FormatException("Conflicting long mode flag.");
				builder.LongMode = value;
			}

			private void SetOperandSize(IntegerSize size)
			{
				if (state > State.PostSimdPrefix)
					throw new FormatException("Out-of-order operand size prefix.");
				if (builder.OperandSize != OperandSizeEncoding.Any || builder.RexW.HasValue)
					throw new FormatException("Multiple operand size prefixes.");

				switch (size)
				{
					case IntegerSize.Word:
						builder.OperandSize = OperandSizeEncoding.Word;
						SetRexW(false); // TODO: Is this always the case?
						SetLongMode(false);
						break;

					case IntegerSize.Dword:
						builder.OperandSize = OperandSizeEncoding.Dword;
						SetRexW(false); // TODO: Is this always the case?
						break;

					case IntegerSize.Qword:
						SetRexW(true); // TODO: Is this always the case?
						SetLongMode(true);
						break;

					default: throw new ArgumentException();
				}
			}

			private void SetRexW(bool set)
			{
				if (builder.RexW.GetValueOrDefault(set) != set)
					throw new FormatException("Conflicting REX.W encoding.");

				builder.RexW = set;
				if (set) SetLongMode(true);
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
				if (plusR) builder.ModRM = ModRMEncoding.MainByteReg;
				AdvanceTo(State.PostOpcode);
			}

			private void SetModRM(byte? reg, NasmOperandType baseRegOperandType)
			{
				var baseRegOpType = baseRegOperandType & NasmOperandType.OpType_Mask;
				bool allowsMemOnly = baseRegOpType == NasmOperandType.OpType_Memory;
				bool allowsAny = baseRegOpType == NasmOperandType.OpType_RegisterOrMemory;

				SetModRM(new ModRMEncoding(reg, rmRegAllowed: !allowsMemOnly,
					rmMemAllowed: allowsMemOnly || allowsAny));
			}

			private void SetModRM(ModRMEncoding encoding)
			{
				if (state != State.PostOpcode) throw new FormatException("Out-of-order ModRM token.");
				if (builder.ModRM != ModRMEncoding.None) throw new FormatException("Multiple ModRMs.");
				builder.ModRM = encoding;
				AdvanceTo(State.PostModRM);
			}

			private void AddImmediateWithSizeInBytes(int count)
			{
				if (state < State.PostOpcode) throw new FormatException("Out-of-order immediate token.");
				if (builder.Imm8Ext.HasValue) throw new FormatException();
				builder.ImmediateSizeInBytes += (byte)count;
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
