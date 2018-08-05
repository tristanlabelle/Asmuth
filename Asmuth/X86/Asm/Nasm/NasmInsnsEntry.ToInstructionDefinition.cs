using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Asm.Nasm
{
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
			
			var data = new InstructionDefinition.Data
			{
				Mnemonic = mnemonic,
				Encoding = EncodingParser.Parse(encodingTokens, vexEncoding),
				Operands = NasmOperand.ToOperandFormat((IReadOnlyList<NasmOperand>)operands, Flags)
			};

			return new InstructionDefinition(in data);
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

			private OpcodeEncodingFlags flags;
			private byte mainByte;
			private byte modRM;
			private byte imm8;
			private State state;

			public static OpcodeEncoding Parse(
				IEnumerable<NasmEncodingToken> tokens, VexEncoding vexEncoding)
			{
				Contract.Requires(tokens != null);

				var parser = new EncodingParser();
				return parser.DoParse(tokens, vexEncoding);
			}

			#region ConvertEncodingTokens
			private OpcodeEncoding DoParse(IEnumerable<NasmEncodingToken> tokens, VexEncoding vexEncoding)
			{
				state = State.Prefixes;
				
				NasmEncodingTokenType addressSize = 0;
				foreach (var token in tokens)
				{
					switch (token.Type)
					{
						case NasmEncodingTokenType.Vex:
							Contract.Assert(flags == 0); // Should be the first thing we encounter
							SetVex(vexEncoding);
							break;

						case NasmEncodingTokenType.AddressSize_Fixed16:
						case NasmEncodingTokenType.AddressSize_Fixed32:
						case NasmEncodingTokenType.AddressSize_Fixed64:
						case NasmEncodingTokenType.AddressSize_NoOverride:
							Contract.Assert(state == State.Prefixes);
							Contract.Assert(addressSize == 0);
							addressSize = token.Type;
							break;

						case NasmEncodingTokenType.OperandSize_16:
							SetOperandSize(OpcodeEncodingFlags.OperandSize_Word);
							SetLongCodeSegment(false);
							break;

						case NasmEncodingTokenType.OperandSize_32:
							SetOperandSize(OpcodeEncodingFlags.OperandSize_Dword);
							break;

						case NasmEncodingTokenType.OperandSize_64:
							SetOperandSize(OpcodeEncodingFlags.OperandSize_Ignored);
							SetRexW(true);
							break;
							
						// TODO: Not too clear what this means/implies
						case NasmEncodingTokenType.OperandSize_64WithoutW:
							SetLongCodeSegment(true);
							break;

						case NasmEncodingTokenType.OperandSize_NoOverride:
							throw new FormatException();

						// Legacy prefixes
						case NasmEncodingTokenType.LegacyPrefix_F2:
							SetSimdPrefix(SimdPrefix._F2);
							continue;

						case NasmEncodingTokenType.LegacyPrefix_MustRep:
						case NasmEncodingTokenType.LegacyPrefix_F3:
							SetSimdPrefix(SimdPrefix._F3);
							continue;

						case NasmEncodingTokenType.LegacyPrefix_NoSimd:
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

							if (state < State.OpcodeMap0F && flags.HasEscapeXex() && token.Byte == 0x0F)
							{
								flags = flags.WithMap(OpcodeMap.Escape0F);
								AdvanceTo(State.OpcodeMap0F);
								continue;
							}

							if (state == State.OpcodeMap0F && (token.Byte == 0x38 || token.Byte == 0x3A))
							{
								flags = flags.WithMap(
									token.Byte == 0x38 ? OpcodeMap.Escape0F38 : OpcodeMap.Escape0F3A);
								AdvanceTo(State.PostOpcodeMap);
								continue;
							}

							if (state < State.PostOpcode)
							{
								SetOpcode(token.Byte, plusR: false);
								continue;
							}

							if (state == State.PostOpcode)
							{
								SetModRM(OpcodeEncodingFlags.ModRM_Fixed | OpcodeEncodingFlags.FixedModReg,
									token.Byte);
								continue;
							}

							// Opcode extension byte
							Contract.Assert(state == State.PostModRM);
							Contract.Assert((flags & OpcodeEncodingFlags.FixedImm8) == 0);
							flags |= OpcodeEncodingFlags.FixedImm8;
							imm8 = token.Byte;
							AddImmediateWithSizeInBytes(1);
							break;

						case NasmEncodingTokenType.Byte_PlusRegister:
							Contract.Assert((token.Byte & 7) == 0);
							if (state < State.PostOpcode)
							{
								SetOpcode(token.Byte, plusR: false);
								continue;
							}

							if (state < State.PostModRM)
							{
								SetModRM(OpcodeEncodingFlags.ModRM_Direct | OpcodeEncodingFlags.FixedModReg,
									token.Byte);
								continue;
							}

							throw new FormatException();

						case NasmEncodingTokenType.Byte_PlusConditionCode:
							// TODO: figure out what this means: [i:	71+c jlen e9 rel]
							Contract.Assert(state < State.PostOpcode);
							throw new NotImplementedException();

						case NasmEncodingTokenType.ModRM:
							SetModRM(OpcodeEncodingFlags.ModRM_Any);
							break;

						case NasmEncodingTokenType.ModRM_FixedReg:
							SetModRM(OpcodeEncodingFlags.FixedModReg, (byte)(token.Byte << 3));
							break;

						// Immediates
						case NasmEncodingTokenType.Immediate_Byte:
						case NasmEncodingTokenType.Immediate_Byte_Signed:
						case NasmEncodingTokenType.Immediate_Byte_Unsigned:
						case NasmEncodingTokenType.Immediate_RelativeOffset8:
						case NasmEncodingTokenType.Immediate_Is4:
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
							if ((flags & OpcodeEncodingFlags.CodeSegment_Mask) == OpcodeEncodingFlags.CodeSegment_Long
								|| (flags & OpcodeEncodingFlags.OperandSize_Mask) == OpcodeEncodingFlags.OperandSize_Dword)
								AddImmediateWithSizeInBytes(sizeof(int));
							else if ((flags & OpcodeEncodingFlags.OperandSize_Mask) == OpcodeEncodingFlags.OperandSize_Word)
								AddImmediateWithSizeInBytes(sizeof(short));
							else
								throw new FormatException();
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

				return new OpcodeEncoding(flags, mainByte, modRM, imm8);
			}

			private void SetLongCodeSegment(bool @long)
			{
				Contract.Assert((this.flags & OpcodeEncodingFlags.CodeSegment_Mask)
					!= (@long ? OpcodeEncodingFlags.CodeSegment_IA32 : OpcodeEncodingFlags.CodeSegment_Long));
				flags |= @long ? OpcodeEncodingFlags.CodeSegment_Long : OpcodeEncodingFlags.CodeSegment_IA32;
			}

			private void SetOperandSize(OpcodeEncodingFlags flags)
			{
				Contract.Assert(state <= State.PostSimdPrefix);
				Contract.Assert((this.flags & OpcodeEncodingFlags.OperandSize_Mask) == 0);
				this.flags |= flags;
			}

			private void SetRexW(bool set)
			{
				Contract.Assert((this.flags & OpcodeEncodingFlags.RexW_Mask)
					!= (set ? OpcodeEncodingFlags.RexW_0 : OpcodeEncodingFlags.RexW_1));

				if (set)
				{
					SetLongCodeSegment(true);
					flags |= OpcodeEncodingFlags.RexW_1;
				}
				else
				{
					flags |= OpcodeEncodingFlags.RexW_0;
				}
			}

			private void SetVex(VexEncoding vexEncoding)
			{
				Contract.Assert(flags == 0);
				flags |= vexEncoding.AsOpcodeEncodingFlags();
				AdvanceTo(State.PostOpcodeMap);
			}

			private void SetSimdPrefix(SimdPrefix prefix)
			{
				Contract.Requires(flags.GetSimdPrefix() == SimdPrefix.None);
				flags = flags.WithSimdPrefix(prefix);
				AdvanceTo(State.PostSimdPrefix);
			}

			private void SetOpcode(byte @byte, bool plusR)
			{
				Contract.Requires(this.mainByte == 0);
				this.mainByte = @byte;
				if (plusR) this.flags |= OpcodeEncodingFlags.MainByteHasEmbeddedReg;
				AdvanceTo(State.PostOpcode);
			}

			private void SetModRM(OpcodeEncodingFlags flags, byte @byte = 0)
			{
				Contract.Requires(state == State.PostOpcode);
				Contract.Requires((this.flags & OpcodeEncodingFlags.HasModRM) == 0);
				Contract.Requires((flags & ~(OpcodeEncodingFlags.FixedModReg | OpcodeEncodingFlags.ModRM_Mask)) == 0);
				this.flags |= OpcodeEncodingFlags.HasModRM | flags;
				this.modRM = @byte;
				AdvanceTo(State.PostModRM);
			}

			private void AddImmediateWithSizeInBytes(int count)
			{
				Contract.Requires(state >= State.PostOpcode);

				flags = flags.WithImmediateSizeInBytes(
					flags.GetImmediateSizeInBytes() + count);
				AdvanceTo(State.Immediates);
			}

			private void AdvanceTo(State newState)
			{
				Contract.Requires(newState >= state);
				state = newState;
			}
			#endregion
		}
	}
}
