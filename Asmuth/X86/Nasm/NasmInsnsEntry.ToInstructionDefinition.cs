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

			return new InstructionDefinitionConverter().Convert(this);
		}

		private struct InstructionDefinitionConverter
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

			private InstructionDefinition.Data instructionData;
			private State state;

			public InstructionDefinition Convert(NasmInsnsEntry entry)
			{
				Contract.Requires(entry != null);
				
				instructionData.Mnemonic = entry.Mnemonic;
				ConvertEncodingTokens(entry);
				var operands = new List<OperandDefinition>();
				ConvertOperands(entry, operands);

				return new InstructionDefinition(ref instructionData, operands);
			}

			#region ConvertEncodingTokens
			private void ConvertEncodingTokens(NasmInsnsEntry entry)
			{
				state = State.Prefixes;

				bool hasVex = false;
				NasmEncodingTokenType addressSize = 0;
				foreach (var token in entry.EncodingTokens)
				{
					switch (token.Type)
					{
						case NasmEncodingTokenType.Vex:
							Contract.Assert(!hasVex);
							SetVex(entry.VexEncoding);
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

						case NasmEncodingTokenType.OperandSize_Fixed16:
							SetOperandSize(InstructionEncoding.OperandSize_Fixed16);
							break;

						case NasmEncodingTokenType.OperandSize_Fixed32:
							SetOperandSize(InstructionEncoding.OperandSize_Fixed32);
							break;

						case NasmEncodingTokenType.OperandSize_Fixed64:
							SetOperandSize(InstructionEncoding.OperandSize_Fixed64);
							break;
							
						case NasmEncodingTokenType.OperandSize_Fixed64_RexExtensionsOnly:
							{
								InstructionEncoding newOperandSize;
								switch (instructionData.Encoding & InstructionEncoding.OperandSize_Mask)
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

								instructionData.Encoding &= ~InstructionEncoding.OperandSize_Mask;
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

						case NasmEncodingTokenType.LegacyPrefix_None:
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
									if ((instructionData.Opcode & Opcode.Map_Mask) == Opcode.Map_Default
										&& token.Byte == 0x0F)
									{
										instructionData.Opcode = instructionData.Opcode.WithMap(OpcodeMap.Escape0F);
										AdvanceTo(State.Map0F);
										continue;
									}

									if ((instructionData.Opcode & Opcode.Map_Mask) == Opcode.Map_0F
										&& (token.Byte == 0x38 || token.Byte == 0x3A))
									{
										instructionData.Opcode = instructionData.Opcode.WithMap(
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
							instructionData.Opcode = instructionData.Opcode.WithExtraByte(token.Byte);
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
							switch (instructionData.Encoding & InstructionEncoding.OperandSize_Mask)
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
						case NasmEncodingTokenType.Misc_VM32x:
						case NasmEncodingTokenType.Misc_VM64x:
						case NasmEncodingTokenType.Misc_VM32y:
						case NasmEncodingTokenType.Misc_VM64y:
						case NasmEncodingTokenType.Misc_Vsiby:
						case NasmEncodingTokenType.Misc_Vsibz:
							break; // TODO: do something with those VM(32|64)[xy]?

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
				Contract.Assert((instructionData.Encoding & InstructionEncoding.OperandSize_Mask) == 0);
				instructionData.Encoding |= encoding;
			}

			private void SetVex(VexOpcodeEncoding vexEncoding)
			{
				Contract.Requires((instructionData.Opcode & Opcode.XexType_Mask) == Opcode.XexType_LegacyOrRex);
				Contract.Requires((instructionData.Opcode & Opcode.SimdPrefix_Mask) == Opcode.SimdPrefix_None);

				Opcode opcode;
				InstructionEncoding encoding;
				vexEncoding.ToOpcodeEncoding(out opcode, out encoding);

				// TODO: Make sure these or's are safe
				instructionData.Opcode |= opcode;
				instructionData.Encoding |= encoding;

				AdvanceTo(State.PostMap);
			}

			private void SetSimdPrefix(SimdPrefix prefix)
			{
				Contract.Requires(instructionData.Opcode.GetSimdPrefix() == SimdPrefix.None);
				instructionData.Opcode = instructionData.Opcode.WithSimdPrefix(prefix);
				AdvanceTo(State.PostSimdPrefix);
			}

			private void SetOpcode(InstructionEncoding formatEncoding, byte @byte)
			{
				Contract.Requires((formatEncoding & ~InstructionEncoding.OpcodeFormat_Mask) == 0);
				Contract.Assert((instructionData.Encoding & InstructionEncoding.OpcodeFormat_Mask) == 0);
				instructionData.Encoding |= formatEncoding;
				instructionData.Opcode = instructionData.Opcode.WithMainByte(@byte);
				AdvanceTo(State.PostOpcode);
			}

			private void SetModRM(InstructionEncoding format, byte @byte = 0)
			{
				Contract.Requires(state == State.PostOpcode);
				Contract.Requires((instructionData.Encoding & InstructionEncoding.ModRM_Mask) == InstructionEncoding.ModRM_None);
				instructionData.Encoding = instructionData.Encoding.WithModRM(format);
				if (format != InstructionEncoding.ModRM_None && format != InstructionEncoding.ModRM_Any)
					instructionData.Opcode = instructionData.Opcode.WithExtraByte(@byte);
				AdvanceTo(State.PostModRM);
			}

			private void AddImmediate(ImmediateSize size)
			{
				Contract.Requires(state >= State.PostOpcode);

				int immediateCount = instructionData.Encoding.GetImmediateCount();
				Contract.Requires(immediateCount < 2);
				instructionData.Encoding = (immediateCount == 0)
					? instructionData.Encoding.WithFirstImmediateSize(size)
					: instructionData.Encoding.WithSecondImmediateSize(size);

				AdvanceTo(State.Immediates);
			}

			private void AdvanceTo(State newState)
			{
				Contract.Requires(newState >= state);
				state = newState;
			}
			#endregion

			#region ConvertOperands
			private void ConvertOperands(NasmInsnsEntry entry, ICollection<OperandDefinition> operands)
			{
				foreach (var nasmOperand in entry.Operands)
				{
					// TODO: Convert operand encoding
					var operand = new OperandDefinition(nasmOperand.Field, default(OperandEncoding), AccessType.ReadWrite);
					operands.Add(operand);
				}
			}
			#endregion
		}
	}
}
