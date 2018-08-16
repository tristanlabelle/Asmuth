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

			NasmOperandType? baseRegOperandType = null;
			foreach (var operand in operands)
			{
				if (operand.Field == OperandField.BaseReg)
				{
					baseRegOperandType = operand.Type;
					break;
				}
			}

			var data = new InstructionDefinition.Data
			{
				Mnemonic = mnemonic,
				Encoding = ToOpcodeEncoding(encodingTokens, vexEncoding, GetLongMode(flags.Contains), baseRegOperandType),
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
			NasmOperandType? baseRegOperandType = null)
		{
			if (encodingTokens == null) throw new ArgumentNullException(nameof(encodingTokens));
			
			OEF encodingFlags = OEF.LongMode_Any;
			if (longMode.HasValue)
				encodingFlags = longMode.Value ? OEF.LongMode_Yes : OEF.LongMode_No;

			var parser = new EncodingParser();
			return parser.Parse(encodingTokens, vexEncoding.GetValueOrDefault(),
				encodingFlags, baseRegOperandType.GetValueOrDefault(NasmOperandType.OpType_RegisterOrMemory));
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
				OEF codeSegmentTypeFlags, NasmOperandType baseRegOperandType)
			{
				flags = codeSegmentTypeFlags & OEF.LongMode_Mask;
				state = State.Prefixes;
				
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
							SetAddressSize(AddressSize._16Bits);
							break;

						case NasmEncodingTokenType.AddressSize_Fixed32:
							SetAddressSize(AddressSize._32Bits);
							break;

						case NasmEncodingTokenType.AddressSize_Fixed64:
							SetAddressSize(AddressSize._64Bits);
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
							
						// TODO: Not too clear what this means/implies
						case NasmEncodingTokenType.OperandSize_64WithoutW:
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
							SetModRM(fixedReg: null, baseRegOperandType);
							break;

						case NasmEncodingTokenType.ModRM_FixedReg:
							SetModRM(fixedReg: token.Byte, baseRegOperandType);
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
							if ((flags & OEF.LongMode_Mask) == OEF.LongMode_Yes
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

			private void SetAddressSize(AddressSize size)
			{
				if (state != State.Prefixes) throw new FormatException("Out-of-order address size token.");
				if (flags.GetAddressSize().HasValue) throw new FormatException("Multiple address size tokens.");
				switch (size)
				{
					case AddressSize._16Bits: flags |= OEF.AddressSize_16; break;
					case AddressSize._32Bits: flags |= OEF.AddressSize_32; break;
					case AddressSize._64Bits: flags |= OEF.AddressSize_64; break;
					default: throw new ArgumentOutOfRangeException(nameof(size));
				}
			}

			private void SetLongMode(bool @long)
			{
				if (flags.GetLongMode().GetValueOrDefault(@long) != @long)
					throw new FormatException("Conflicting code segment type.");
				flags |= @long ? OEF.LongMode_Yes : OEF.LongMode_No;
			}

			private void SetOperandSize(IntegerSize size)
			{
				if (state > State.PostSimdPrefix)
					throw new FormatException("Out-of-order operand size prefix.");
				if ((flags & OEF.OperandSize_Mask) != 0)
					throw new FormatException("Multiple operand size prefixes.");
				if ((flags & OEF.RexW_Mask) != 0)
					throw new FormatException("Multiple operand size prefixes.");

				switch (size)
				{
					case IntegerSize.Word:
						flags |= OEF.OperandSize_Word;
						flags |= OEF.RexW_0;
						SetLongMode(false);
						break;

					case IntegerSize.Dword:
						flags |= OEF.OperandSize_Dword;
						flags |= OEF.RexW_0;
						break;
						
					case IntegerSize.Qword:
						flags |= OEF.RexW_1;
						SetLongMode(true);
						break;

					default: throw new ArgumentException();
				}
			}

			private void SetRexW(bool set)
			{
				if ((this.flags & OEF.RexW_Mask)
					== (set ? OEF.RexW_0 : OEF.RexW_1))
					throw new FormatException("Conflicting REX.W encoding.");

				if (set)
				{
					SetLongMode(true);
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

			private void SetModRM(byte? fixedReg, NasmOperandType baseRegOperandType)
			{
				OEF modRMFlags = 0;
				ModRM modRM = 0;

				if (fixedReg.HasValue)
				{
					modRMFlags |= OEF.ModRM_FixedReg;
					modRM = ModRMEnum.FromReg(fixedReg.Value);
				}

				var baseRegOpType = baseRegOperandType & NasmOperandType.OpType_Mask;
				if (baseRegOpType == NasmOperandType.OpType_Memory)
					modRMFlags |= OEF.ModRM_RM_Indirect;
				else if (baseRegOpType != NasmOperandType.OpType_RegisterOrMemory)
				{
					modRMFlags |= OEF.ModRM_RM_Direct;
					modRM |= ModRM.Mod_Direct;
				}

				SetModRM(modRMFlags, modRM);
			}

			private void SetModRM(OEF modRMFlags, ModRM value = default)
			{
				if (state != State.PostOpcode) throw new FormatException("Out-of-order ModRM token.");
				Debug.Assert((flags & OEF.ModRM_Present) == 0);
				Debug.Assert((modRMFlags & ~(OEF.ModRM_FixedReg | OEF.ModRM_RM_Mask)) == 0);
				flags |= OEF.ModRM_Present | modRMFlags;
				modRM = value;
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
