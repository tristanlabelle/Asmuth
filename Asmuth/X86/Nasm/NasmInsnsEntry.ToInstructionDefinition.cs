using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Nasm
{
	partial class NasmInsnsEntry
	{
		public InstructionDefinition ToInstructionDefinition()
		{
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
				NasmEncodingTokenType addressSize = 0, operandSize = 0;
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
						case NasmEncodingTokenType.OperandSize_Fixed32:
						case NasmEncodingTokenType.OperandSize_Fixed64:
						case NasmEncodingTokenType.OperandSize_Fixed64_RexExtensionsOnly:
						case NasmEncodingTokenType.OperandSize_NoOverride:
							Contract.Assert(state <= State.PostSimdPrefix);
							Contract.Assert(addressSize == 0);
							operandSize = token.Type;
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
						case NasmEncodingTokenType.Rex_NoW:	// TODO: handle this?
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
										instructionData.Opcode = instructionData.Opcode.WithMap(Opcode.Map_0F);
										AdvanceTo(State.Map0F);
										continue;
									}

									if ((instructionData.Opcode & Opcode.Map_Mask) == Opcode.Map_0F
										&& (token.Byte == 0x38 || token.Byte == 0x3A))
									{
										instructionData.Opcode = instructionData.Opcode.WithMap(
											token.Byte == 0x38 ? Opcode.Map_0F38 : Opcode.Map_0F3A);
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
							AddImmediate(ImmediateType.OpcodeExtension);
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
							Contract.Assert((token.Byte & 0xF) == 0);
							Contract.Assert(state < State.PostOpcode);
							SetOpcode(InstructionEncoding.OpcodeFormat_EmbeddedConditionCode, token.Byte);
							continue;

						case NasmEncodingTokenType.ModRM: SetModRM(InstructionEncoding.ModRM_Any); break;
						case NasmEncodingTokenType.ModRM_FixedReg: SetModRM(InstructionEncoding.ModRM_FixedReg, (byte)(token.Byte << 3)); break;

						// Immediates
						case NasmEncodingTokenType.Immediate_Byte:
						case NasmEncodingTokenType.Immediate_Byte_Signed:
						case NasmEncodingTokenType.Immediate_Byte_Unsigned:
							AddImmediate(ImmediateType.Imm8);
							break;

						case NasmEncodingTokenType.Immediate_RelativeOffset8: AddImmediate(ImmediateType.RelativeCodeOffset8); break;
						case NasmEncodingTokenType.Immediate_Is4: AddImmediate(ImmediateType.OpcodeExtension); break;
						case NasmEncodingTokenType.Immediate_Word: AddImmediate(ImmediateType.Imm16); break;
						case NasmEncodingTokenType.Immediate_Dword: AddImmediate(ImmediateType.Imm32); break;
						case NasmEncodingTokenType.Immediate_Dword_Signed: AddImmediate(ImmediateType.Imm32); break;
						case NasmEncodingTokenType.Immediate_WordOrDword: AddImmediate(ImmediateType.Imm16Or32); break;
						case NasmEncodingTokenType.Immediate_WordOrDwordOrQword: AddImmediate(ImmediateType.Imm16Or32Or64); break;
						case NasmEncodingTokenType.Immediate_Qword: AddImmediate(ImmediateType.Imm64); break;

						case NasmEncodingTokenType.Immediate_RelativeOffset:
							Contract.Assert(operandSize != 0);
							switch (operandSize)
							{
								case NasmEncodingTokenType.OperandSize_NoOverride: AddImmediate(ImmediateType.RelativeCodeOffset16Or32); break;
								case NasmEncodingTokenType.OperandSize_Fixed16: AddImmediate(ImmediateType.RelativeCodeOffset16); break;
								case NasmEncodingTokenType.OperandSize_Fixed32: AddImmediate(ImmediateType.RelativeCodeOffset32); break;
								case NasmEncodingTokenType.AddressSize_Fixed64: throw new NotImplementedException();
								case NasmEncodingTokenType.OperandSize_Fixed64_RexExtensionsOnly: AddImmediate(ImmediateType.RelativeCodeOffset64); break;
								default: throw new UnreachableException();
							}
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
						case NasmEncodingTokenType.Misc_AssembleWaitPrefix:	// Implicit WAIT prefix when assembling instruction
							break;

						default:
							throw new NotImplementedException("Handling NASM encoding tokens of type '{0}'".FormatInvariant(token.Type));
					}
				}
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

			private void SetOpcode(InstructionEncoding encoding, byte @byte)
			{
				instructionData.Encoding = instructionData.Encoding.WithOpcodeFormat(InstructionEncoding.OpcodeFormat_FixedByte);
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

			private void AddImmediate(ImmediateType type)
			{
				Contract.Requires(state >= State.PostOpcode);
				Contract.Requires((type & ImmediateType.Type_Mask) != ImmediateType.Type_None);

				int immediateCount = instructionData.Encoding.GetImmediateCount();
				Contract.Requires(immediateCount < 2);
				if (immediateCount == 0)
					instructionData.Encoding |= instructionData.Encoding.WithFirstImmediateType(type);
				else
					instructionData.Encoding |= instructionData.Encoding.WithSecondImmediateType(type);

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
				// TODO: Convert operands
			}
			#endregion
		}
	}
}
