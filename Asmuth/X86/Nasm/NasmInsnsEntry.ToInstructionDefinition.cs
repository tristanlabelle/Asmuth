using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Nasm
{
	partial class NasmInsnsEntry
	{
		public InstructionDefinition ToInstructionDefinition()
		{
			if (IsPseudo) throw new InvalidOperationException();

			var data = new InstructionDefinition.Data();
			data.Mnemonic = mnemonic;

			EncodingParser.Parse(encodingTokens, vexEncoding, out data.Opcode, out data.Encoding);

			var operandDefs = new OperandDefinition[operands.Count];
			for (int i = 0; i < operands.Count; ++i)
				operandDefs[i] = operands[i].ToOperandDefinition();

			return new InstructionDefinition(ref data, operandDefs);
		}

		private struct EncodingParser
		{
			private enum State
			{
				Prefixes,
				PostSimdPrefix,
				Map0F,
				PostMap,
				PreOpcode = PostMap,
				PostOpcode,
				PostModRM,
				Immediates
			}

			private Opcode opcode;
			private InstructionEncoding encoding;
			private State state;

			public static void Parse(IEnumerable<NasmEncodingToken> tokens, VexOpcodeEncoding vexEncoding,
				out Opcode opcode, out InstructionEncoding encoding)
			{
				Contract.Requires(tokens != null);

				var parser = new EncodingParser();
				parser.Parse(tokens, vexEncoding);
				opcode = parser.opcode;
				encoding = parser.encoding;
			}

			#region ConvertEncodingTokens
			private void Parse(IEnumerable<NasmEncodingToken> tokens, VexOpcodeEncoding vexEncoding)
			{
				state = State.Prefixes;

				bool hasVex = false;
				NasmEncodingTokenType addressSize = 0;
				foreach (var token in tokens)
				{
					switch (token.Type)
					{
						case NasmEncodingTokenType.Vex:
							Contract.Assert(!hasVex);
							SetVex(vexEncoding);
							hasVex = true;
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
							SetOperandSize(InstructionEncoding.OperandSize_Fixed16);
							break;

						case NasmEncodingTokenType.OperandSize_32:
							SetOperandSize(InstructionEncoding.OperandSize_Fixed32);
							break;

						case NasmEncodingTokenType.OperandSize_64:
							SetOperandSize(InstructionEncoding.OperandSize_Fixed64);
							break;
							
						case NasmEncodingTokenType.OperandSize_64WithoutW:
							{
								InstructionEncoding newOperandSize;
								switch (encoding & InstructionEncoding.OperandSize_Mask)
								{
									case 0:
										newOperandSize = InstructionEncoding.OperandSize_16Or32Or64;
										break;

									case InstructionEncoding.OperandSize_Fixed16:
										newOperandSize = InstructionEncoding.OperandSize_16Or32Or64;
										break;

									case InstructionEncoding.OperandSize_Fixed32:
										newOperandSize = InstructionEncoding.OperandSize_32Or64;
										break;

									default: throw new InvalidDataException();
								}

								encoding &= ~InstructionEncoding.OperandSize_Mask;
								SetOperandSize(newOperandSize);
							}
							break;

						case NasmEncodingTokenType.OperandSize_NoOverride:
							// TODO: What does this mean for the instruction encoding?
							SetOperandSize(InstructionEncoding.OperandSize_16Or32);
							break;

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

							if (state < State.PostOpcode)
							{
								if (!hasVex)
								{
									if ((opcode & Opcode.Map_Mask) == Opcode.Map_Default
										&& token.Byte == 0x0F)
									{
										opcode = opcode.WithMap(OpcodeMap.Escape0F);
										AdvanceTo(State.Map0F);
										continue;
									}

									if ((opcode & Opcode.Map_Mask) == Opcode.Map_0F
										&& (token.Byte == 0x38 || token.Byte == 0x3A))
									{
										opcode = opcode.WithMap(
											token.Byte == 0x38 ? OpcodeMap.Escape0F38 : OpcodeMap.Escape0F3A);
										AdvanceTo(State.PostMap);
										continue;
									}
								}

								SetOpcode(InstructionEncoding.OpcodeFormat_FixedByte, token.Byte);
								continue;
							}

							if (state == State.PostOpcode)
							{
								SetModRM(InstructionEncoding.ModRM_Fixed, token.Byte);
								continue;
							}

							Contract.Assert(state == State.PostModRM);
							opcode = opcode.WithExtraByte(token.Byte);
							AddImmediate(ImmediateSize.Fixed8); // Opcode extension byte
							break;

						case NasmEncodingTokenType.Byte_PlusRegister:
							Contract.Assert((token.Byte & 7) == 0);
							if (state < State.PostOpcode)
							{
								SetOpcode(InstructionEncoding.OpcodeFormat_EmbeddedRegister, token.Byte);
								continue;
							}

							if (state < State.PostModRM)
							{
								SetModRM(InstructionEncoding.ModRM_FixedModReg, token.Byte);
								continue;
							}

							throw new FormatException();

						case NasmEncodingTokenType.Byte_PlusConditionCode:
							// TODO: figure out what this means: [i:	71+c jlen e9 rel]
							Contract.Assert(state < State.PostOpcode);
							SetOpcode(InstructionEncoding.OpcodeFormat_EmbeddedConditionCode, (byte)(token.Byte & 0xF0));
							continue;

						case NasmEncodingTokenType.ModRM:
							SetModRM(InstructionEncoding.ModRM_Any);
							break;

						case NasmEncodingTokenType.ModRM_FixedReg:
							SetModRM(InstructionEncoding.ModRM_FixedReg, (byte)(token.Byte << 3));
							break;

						// Immediates
						case NasmEncodingTokenType.Immediate_Byte:
						case NasmEncodingTokenType.Immediate_Byte_Signed:
						case NasmEncodingTokenType.Immediate_Byte_Unsigned:
						case NasmEncodingTokenType.Immediate_RelativeOffset8:
						case NasmEncodingTokenType.Immediate_Is4:
							AddImmediate(ImmediateSize.Fixed8);
							break;

						case NasmEncodingTokenType.Immediate_Word:
						case NasmEncodingTokenType.Immediate_Segment: // TODO: Make sure this happens in the right order
							AddImmediate(ImmediateSize.Fixed16);
							break;

						case NasmEncodingTokenType.Immediate_Dword:
						case NasmEncodingTokenType.Immediate_Dword_Signed:
							AddImmediate(ImmediateSize.Fixed32);
							break;

						case NasmEncodingTokenType.Immediate_WordOrDword:
							AddImmediate(ImmediateSize.Operand16Or32);
							break;

						case NasmEncodingTokenType.Immediate_WordOrDwordOrQword:
							AddImmediate(ImmediateSize.Operand16Or32Or64);
							break;

						case NasmEncodingTokenType.Immediate_Qword:
							AddImmediate(ImmediateSize.Fixed64);
							break;

						case NasmEncodingTokenType.Immediate_RelativeOffset:
							switch (encoding & InstructionEncoding.OperandSize_Mask)
							{
								case InstructionEncoding.OperandSize_Ignored: AddImmediate(ImmediateSize.Operand16Or32); break;
								case InstructionEncoding.OperandSize_Fixed16: AddImmediate(ImmediateSize.Fixed16); break;
								case InstructionEncoding.OperandSize_Fixed32: AddImmediate(ImmediateSize.Fixed32); break;
								case InstructionEncoding.OperandSize_Fixed64: AddImmediate(ImmediateSize.Fixed64); break;
								case InstructionEncoding.OperandSize_16Or32: AddImmediate(ImmediateSize.Operand16Or32); break;
								case InstructionEncoding.OperandSize_16Or32Or64: AddImmediate(ImmediateSize.Operand16Or32Or64); break;
								default: throw new InvalidDataException();
							}
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
						case NasmEncodingTokenType.VectorSib_ZmmDwordIndices:
						case NasmEncodingTokenType.VectorSib_ZmmQwordIndices:
							break; // doesn't impact encoding

						case NasmEncodingTokenType.Misc_NoHigh8Register:
						case NasmEncodingTokenType.Misc_AssembleWaitPrefix: // Implicit WAIT prefix when assembling instruction
						case NasmEncodingTokenType.Misc_Resb:
							break;

						default:
							throw new NotImplementedException("Handling NASM encoding tokens of type '{0}'".FormatInvariant(token.Type));
					}
				}
			}

			private void SetOperandSize(InstructionEncoding encoding)
			{
				Contract.Assert(state <= State.PostSimdPrefix);
				Contract.Assert((this.encoding & InstructionEncoding.OperandSize_Mask) == 0);
				this.encoding |= encoding;
			}

			private void SetVex(VexOpcodeEncoding vexEncoding)
			{
				Contract.Requires((opcode & Opcode.XexType_Mask) == Opcode.XexType_LegacyOrRex);
				Contract.Requires((opcode & Opcode.SimdPrefix_Mask) == Opcode.SimdPrefix_None);

				Opcode opcodeFromVex;
				InstructionEncoding encodingFromVex;
				vexEncoding.ToOpcodeEncoding(out opcodeFromVex, out encodingFromVex);

				// TODO: Make sure these or's are safe
				opcode |= opcodeFromVex;
				encoding |= encodingFromVex;

				AdvanceTo(State.PostMap);
			}

			private void SetSimdPrefix(SimdPrefix prefix)
			{
				Contract.Requires(opcode.GetSimdPrefix() == SimdPrefix.None);
				opcode = opcode.WithSimdPrefix(prefix);
				AdvanceTo(State.PostSimdPrefix);
			}

			private void SetOpcode(InstructionEncoding encoding, byte @byte)
			{
				Contract.Requires((encoding & ~InstructionEncoding.OpcodeFormat_Mask) == 0);
				Contract.Assert((this.encoding & InstructionEncoding.OpcodeFormat_Mask) == 0);
				this.encoding |= encoding;
				opcode = opcode.WithMainByte(@byte);
				AdvanceTo(State.PostOpcode);
			}

			private void SetModRM(InstructionEncoding encoding, byte @byte = 0)
			{
				Contract.Requires(state == State.PostOpcode);
				Contract.Requires((encoding & ~InstructionEncoding.ModRM_Mask) == 0);
				Contract.Requires((this.encoding & InstructionEncoding.ModRM_Mask) == InstructionEncoding.ModRM_None);
				this.encoding = this.encoding.WithModRM(encoding);
				if (encoding != InstructionEncoding.ModRM_None && encoding != InstructionEncoding.ModRM_Any)
					opcode = opcode.WithExtraByte(@byte);
				AdvanceTo(State.PostModRM);
			}

			private void AddImmediate(ImmediateSize size)
			{
				Contract.Requires(state >= State.PostOpcode);

				int immediateCount = encoding.GetImmediateCount();
				Contract.Requires(immediateCount < 2);
				encoding = (immediateCount == 0)
					? encoding.WithFirstImmediateSize(size)
					: encoding.WithSecondImmediateSize(size);

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
