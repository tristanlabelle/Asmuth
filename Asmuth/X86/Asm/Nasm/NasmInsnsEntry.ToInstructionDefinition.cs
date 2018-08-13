using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Asm.Nasm
{
	using OEF = OpcodeEncodingFlags;

	partial class NasmInsnsEntry
	{
		public bool CanConvertToOpcodeEncoding
		{
			get
			{
				return !IsPseudo
					&& !IsAssembleOnly
					&& mnemonic != "CALL" && mnemonic != "ENTER" // Can't yet handle imm:imm
					&& !encodingTokens.Contains(NasmEncodingTokenType.AddressSize_NoOverride)
					&& !encodingTokens.Contains(NasmEncodingTokenType.OperandSize_NoOverride);
			}
		}
		
		public InstructionDefinition ToInstructionDefinition()
		{
			if (!CanConvertToOpcodeEncoding) throw new InvalidOperationException();

			bool memOnlyModRM = false;
			foreach (var operand in operands)
			{
				if (operand.Field == OperandField.BaseReg
					&& (operand.Type & NasmOperandType.OpType_Mask) == NasmOperandType.OpType_Memory)
				{
					memOnlyModRM = true;
					break;
				}
			}

			var data = new InstructionDefinition.Data
			{
				Mnemonic = mnemonic,
				Encoding = ToOpcodeEncoding(encodingTokens, vexEncoding, GetLongMode(flags.Contains), memOnlyModRM),
				Operands = NasmOperand.ToOperandFormat((IReadOnlyList<NasmOperand>)operands, Flags)
			};

			return new InstructionDefinition(in data);
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
			bool memOnlyModRM = false)
		{
			if (encodingTokens == null) throw new ArgumentNullException(nameof(encodingTokens));
			
			OEF encodingFlags = OEF.CodeSegmentType_Any;
			if (longMode.HasValue)
				encodingFlags = longMode.Value ? OEF.CodeSegmentType_Long : OEF.CodeSegmentType_IA32;

			var parser = new EncodingParser();
			return parser.Parse(encodingTokens, vexEncoding.GetValueOrDefault(),
				encodingFlags, memOnlyModRM);
		}

		private struct EncodingParser
		{
			private enum State
			{
				Prefixes,
				PostSimdPrefix,
				OpcodeMap0F,
				PostOpcodeMap,
				PreOpcode = PostOpcodeMap,
				PostOpcode,
				PostModRM,
				Immediates
			}

			private OEF flags;
			private byte mainByte;
			private ModRM modRM;
			private byte fixedImm8;
			private State state;
			
			public OpcodeEncoding Parse(IEnumerable<NasmEncodingToken> tokens, VexEncoding vexEncoding,
				OEF codeSegmentTypeFlags, bool memOnlyModRM)
			{
				codeSegmentTypeFlags &= OEF.CodeSegmentType_Mask;
				state = State.Prefixes;
				
				NasmEncodingTokenType addressSize = 0;
				foreach (var token in tokens)
				{
					switch (token.Type)
					{
						case NasmEncodingTokenType.Vex:
							if (flags != 0) throw new FormatException("VEX may only be the first token.");
							flags |= vexEncoding.AsOpcodeEncodingFlags();
							AdvanceTo(State.PostOpcodeMap);
							break;

						case NasmEncodingTokenType.AddressSize_Fixed16:
						case NasmEncodingTokenType.AddressSize_Fixed32:
						case NasmEncodingTokenType.AddressSize_Fixed64:
						case NasmEncodingTokenType.AddressSize_NoOverride:
							if (state != State.Prefixes) throw new FormatException("Out-of-order address size token.");
							if (addressSize != 0) throw new FormatException("Multiple address size tokens.");
							addressSize = token.Type;
							break;

						case NasmEncodingTokenType.OperandSize_16:
							SetOperandSize(OEF.OperandSize_Word);
							SetLongCodeSegment(false);
							break;

						case NasmEncodingTokenType.OperandSize_32:
							SetOperandSize(OEF.OperandSize_Dword);
							break;

						case NasmEncodingTokenType.OperandSize_64:
							SetOperandSize(OEF.OperandSize_Ignored);
							SetRexW(true);
							break;
							
						// TODO: Not too clear what this means/implies
						case NasmEncodingTokenType.OperandSize_64WithoutW:
							SetLongCodeSegment(true);
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

							if (state < State.OpcodeMap0F && flags.IsEscapeXex() && token.Byte == 0x0F)
							{
								flags = flags.WithMap(OpcodeMap.Escape0F);
								AdvanceTo(State.OpcodeMap0F);
								break;
							}

							if (state == State.OpcodeMap0F && (token.Byte == 0x38 || token.Byte == 0x3A))
							{
								flags = flags.WithMap(
									token.Byte == 0x38 ? OpcodeMap.Escape0F38 : OpcodeMap.Escape0F3A);
								AdvanceTo(State.PostOpcodeMap);
								break;
							}

							if (state < State.PostOpcode)
							{
								SetMainOpcodeByte(token.Byte, plusR: false);
								break;
							}

							// Heuristic: if it's of the form 0b11000000,
							// it's probably a fixed ModRM, otherwise a fixed imm8
							if (state == State.PostOpcode && !flags.HasModRM()
								&& (token.Byte >> 6) == 3)
							{
								SetModRM(OEF.ModRM_RM_Fixed
									| OEF.ModRM_FixedReg,
									(ModRM)token.Byte);
								break;
							}

							// Opcode extension byte
							Debug.Assert(state == State.PostOpcode || state == State.PostModRM);
							if (flags.HasImm8Ext()) throw new FormatException("Multiple imm8 extension bytes.");
							flags |= OEF.Imm8Ext_Fixed;
							fixedImm8 = token.Byte;
							AddImmediateWithSizeInBytes(1);
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
								SetModRM(OEF.ModRM_RM_Direct
									| OEF.ModRM_FixedReg,
									(ModRM)token.Byte);
								break;
							}

							throw new FormatException();

						case NasmEncodingTokenType.Byte_PlusConditionCode:
							// TODO: figure out what this means: [i:	71+c jlen e9 rel]
							if (state >= State.PostOpcode)
								throw new FormatException("Out-of-order opcode byte.");
							throw new NotImplementedException();

						case NasmEncodingTokenType.ModRM:
							SetModRM(memOnlyModRM ? OEF.ModRM_RM_Indirect : OEF.ModRM_RM_Any);
							break;

						case NasmEncodingTokenType.ModRM_FixedReg:
							SetModRM((memOnlyModRM ? OEF.ModRM_RM_Indirect : OEF.ModRM_RM_Any) | OEF.ModRM_FixedReg,
								ModRMEnum.FromReg(token.Byte));
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
							throw new NotImplementedException();

						case NasmEncodingTokenType.Immediate_Qword:
							AddImmediateWithSizeInBytes(8);
							break;

						case NasmEncodingTokenType.Immediate_RelativeOffset:
							if ((flags & OEF.CodeSegmentType_Mask) == OEF.CodeSegmentType_Long
								|| (flags & OEF.OperandSize_Mask) == OEF.OperandSize_Dword)
								AddImmediateWithSizeInBytes(sizeof(int));
							else if ((flags & OEF.OperandSize_Mask) == OEF.OperandSize_Word)
								AddImmediateWithSizeInBytes(sizeof(short));
							else
								throw new FormatException("Ambiguous relative offset size.");
							break;

						case NasmEncodingTokenType.Immediate_Is4:
							if (flags.GetImmediateSizeInBytes() > 0)
								throw new FormatException("Imm8 extension must be the only immediate.");
							flags |= OEF.Imm8Ext_VexIS4;
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

						case NasmEncodingTokenType.Misc_NoHigh8Register:
						case NasmEncodingTokenType.Misc_AssembleWaitPrefix: // Implicit WAIT prefix when assembling instruction
						case NasmEncodingTokenType.Misc_Resb:
							break;

						default:
							throw new NotImplementedException("Handling NASM encoding tokens of type '{0}'".FormatInvariant(token.Type));
					}
				}

				return new OpcodeEncoding(flags, mainByte, modRM, fixedImm8);
			}

			private void SetLongCodeSegment(bool @long)
			{
				if ((this.flags & OEF.CodeSegmentType_Mask)
					== (@long ? OEF.CodeSegmentType_IA32 : OEF.CodeSegmentType_Long))
					throw new FormatException("Conflicting code segment type.");
				flags |= @long ? OEF.CodeSegmentType_Long : OEF.CodeSegmentType_IA32;
			}

			private void SetOperandSize(OEF flags)
			{
				if (state > State.PostSimdPrefix) throw new FormatException("Out-of-order operand size prefix.");
				if ((this.flags & OEF.OperandSize_Mask) != 0)
					throw new FormatException("Multiple operand size prefixes.");
				this.flags |= flags;
			}

			private void SetRexW(bool set)
			{
				if ((this.flags & OEF.RexW_Mask)
					== (set ? OEF.RexW_0 : OEF.RexW_1))
					throw new FormatException("Conflicting REX.W encoding.");

				if (set)
				{
					SetLongCodeSegment(true);
					flags |= OEF.RexW_1;
				}
				else
				{
					flags |= OEF.RexW_0;
				}
			}

			private void SetSimdPrefix(SimdPrefix prefix)
			{
				if (state >= State.PostSimdPrefix) throw new FormatException("Out-of-order SIMD prefix.");
				Debug.Assert(!flags.GetSimdPrefix().HasValue);
				flags = flags.WithSimdPrefix(prefix);
				AdvanceTo(State.PostSimdPrefix);
			}

			private void SetMainOpcodeByte(byte value, bool plusR)
			{
				if (state >= State.PostOpcode) throw new FormatException("Out-of-order opcode token.");
				this.mainByte = value;
				if (plusR) this.flags |= OEF.HasMainByteReg;
				AdvanceTo(State.PostOpcode);
			}

			private void SetModRM(OEF flags, ModRM value = default)
			{
				if (state != State.PostOpcode) throw new FormatException("Out-of-order ModRM token.");
				Debug.Assert((this.flags & OEF.ModRM_Present) == 0);
				Debug.Assert((flags & ~(OEF.ModRM_FixedReg | OEF.ModRM_RM_Mask)) == 0);
				this.flags |= OEF.ModRM_Present | flags;
				this.modRM = value;
				AdvanceTo(State.PostModRM);
			}

			private void AddImmediateWithSizeInBytes(int count)
			{
				if (state < State.PostOpcode) throw new FormatException("Out-of-order immediate token.");

				flags = flags.WithImmediateSizeInBytes(flags.GetImmediateSizeInBytes() + count);
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
