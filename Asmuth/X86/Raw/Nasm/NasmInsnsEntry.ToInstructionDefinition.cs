using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw.Nasm
{
	partial class NasmInsnsEntry
	{
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

			private InstructionDefinition.Builder builder;
			private State state;

			public InstructionDefinition Convert(NasmInsnsEntry entry)
			{
				Contract.Requires(entry != null);

				builder = new InstructionDefinition.Builder();
				state = State.Prefixes;

				builder.Mnemonic = entry.Mnemonic;

				NasmEncodingTokenType addressSize = 0, operandSize = 0;
				foreach (var token in entry.EncodingTokens)
				{
					switch (token.Type)
					{
						case NasmEncodingTokenType.Vex:
							SetVex(builder, entry.VexEncoding);
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
								if ((builder.Opcode & Opcode.Map_Mask) == Opcode.Map_Default
									&& token.Byte == 0x0F)
								{
									builder.Opcode = builder.Opcode.WithMap(Opcode.Map_0F);
									AdvanceTo(State.Map0F);
									continue;
								}

								if ((builder.Opcode & Opcode.Map_Mask) == Opcode.Map_0F
									&& (token.Byte == 0x38 || token.Byte == 0x3A))
								{
									builder.Opcode = builder.Opcode.WithMap(
										token.Byte == 0x38 ? Opcode.Map_0F38 : Opcode.Map_0F3A);
									AdvanceTo(State.PostMap);
									continue;
								}

								SetOpcode(InstructionEncoding.OpcodeFormat_FixedByte, token.Byte);
								continue;
							}
							
							if (state == State.PostOpcode)
							{
								SetModRM(InstructionEncoding.ModRM_Fixed, token.Byte);
								continue;
							}
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
						case NasmEncodingTokenType.ModRM_FixedReg: SetModRM(InstructionEncoding.ModRM_FixedReg, token.Byte); break;

						// Immediates
						case NasmEncodingTokenType.Immediate_Byte:
						case NasmEncodingTokenType.Immediate_Byte_Signed:
						case NasmEncodingTokenType.Immediate_Byte_Unsigned:
						case NasmEncodingTokenType.Immediate_RelativeOffset8:
						case NasmEncodingTokenType.Immediate_Is4:
							SetImmediateSize(InstructionEncoding.ImmediateSize_8);
							break;

						case NasmEncodingTokenType.Immediate_Word: SetImmediateSize(InstructionEncoding.ImmediateSize_16); break;
						case NasmEncodingTokenType.Immediate_Dword: SetImmediateSize(InstructionEncoding.ImmediateSize_32); break;
						case NasmEncodingTokenType.Immediate_Dword_Signed: SetImmediateSize(InstructionEncoding.ImmediateSize_32); break;
						case NasmEncodingTokenType.Immediate_WordOrDword: SetImmediateSize(InstructionEncoding.ImmediateSize_16Or32); break;
						case NasmEncodingTokenType.Immediate_WordOrDwordOrQword: SetImmediateSize(InstructionEncoding.ImmediateSize_16Or32Or64); break;
						case NasmEncodingTokenType.Immediate_Qword: SetImmediateSize(InstructionEncoding.ImmediateSize_64); break;

						case NasmEncodingTokenType.Immediate_RelativeOffset:
							Contract.Assert(operandSize != 0);
							switch (operandSize)
							{
								case NasmEncodingTokenType.OperandSize_Fixed16: SetImmediateSize(InstructionEncoding.ImmediateSize_16); break;
								case NasmEncodingTokenType.OperandSize_Fixed32: SetImmediateSize(InstructionEncoding.ImmediateSize_32); break;
								case NasmEncodingTokenType.OperandSize_NoOverride: SetImmediateSize(InstructionEncoding.ImmediateSize_16Or32); break;

								case NasmEncodingTokenType.OperandSize_Fixed64:
								case NasmEncodingTokenType.OperandSize_Fixed64_RexExtensionsOnly:
									SetImmediateSize(InstructionEncoding.ImmediateSize_64);
									break;

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
						case NasmEncodingTokenType.Misc_AssembleWaitPrefix: // Implicit WAIT prefix when assembling instruction
							break;

						default:
							throw new NotImplementedException("Handling NASM encoding tokens of type '{0}'".FormatInvariant(token.Type));
					}
				}

				return builder.Build(reuse: false);
			}

			private void SetVex(InstructionDefinition.Builder builder, VexOpcodeEncoding vexEncoding)
			{
				Contract.Assert((builder.Opcode & Opcode.XexType_Mask) == Opcode.XexType_Legacy);
				Contract.Assert((builder.Opcode & Opcode.SimdPrefix_Mask) == Opcode.SimdPrefix_None);

				var vexType = vexEncoding & VexOpcodeEncoding.Type_Mask;
				switch (vexType)
				{
					case VexOpcodeEncoding.Type_Vex: builder.Opcode |= Opcode.XexType_Vex; break;
					case VexOpcodeEncoding.Type_Xop: builder.Opcode |= Opcode.XexType_Xop; break;
					case VexOpcodeEncoding.Type_EVex: builder.Opcode |= Opcode.XexType_EVex; break;
					default: throw new UnreachableException();
				}

				switch (vexEncoding & VexOpcodeEncoding.SimdPrefix_Mask)
				{
					case VexOpcodeEncoding.SimdPrefix_None: builder.Opcode |= Opcode.SimdPrefix_None; break;
					case VexOpcodeEncoding.SimdPrefix_66: builder.Opcode |= Opcode.SimdPrefix_66; break;
					case VexOpcodeEncoding.SimdPrefix_F2: builder.Opcode |= Opcode.SimdPrefix_F2; break;
					case VexOpcodeEncoding.SimdPrefix_F3: builder.Opcode |= Opcode.SimdPrefix_F3; break;
					default: throw new UnreachableException();
				}

				if (vexType == VexOpcodeEncoding.Type_Xop)
				{
					switch (vexEncoding & VexOpcodeEncoding.Map_Mask)
					{
						case VexOpcodeEncoding.Map_Xop8: builder.Opcode |= Opcode.Map_Xop8; break;
						case VexOpcodeEncoding.Map_Xop9: builder.Opcode |= Opcode.Map_Xop9; break;
						case VexOpcodeEncoding.Map_Xop10: builder.Opcode |= Opcode.Map_Xop10; break;
						default: throw new FormatException();
					}
				}
				else
				{
					switch (vexEncoding & VexOpcodeEncoding.Map_Mask)
					{
						case VexOpcodeEncoding.Map_0F: builder.Opcode |= Opcode.Map_0F; break;
						case VexOpcodeEncoding.Map_0F38: builder.Opcode |= Opcode.Map_0F38; break;
						case VexOpcodeEncoding.Map_0F3A: builder.Opcode |= Opcode.Map_0F3A; break;
						default: throw new FormatException();
					}
				}

				// TODO: Rex.W and VectorLength

				AdvanceTo(State.PostMap);
			}

			private void SetSimdPrefix(SimdPrefix prefix)
			{
				Contract.Assert(builder.Opcode.GetSimdPrefix() == SimdPrefix.None);
				builder.Opcode = builder.Opcode.WithSimdPrefix(prefix);
				AdvanceTo(State.PostSimdPrefix);
			}

			private void SetOpcode(InstructionEncoding encoding, byte @byte)
			{
				builder.Encoding = builder.Encoding.WithOpcodeFormat(InstructionEncoding.OpcodeFormat_FixedByte);
				builder.Opcode = builder.Opcode.WithMainByte(@byte);
				AdvanceTo(State.PostOpcode);
			}

			private void SetModRM(InstructionEncoding format, byte @byte = 0)
			{
				Contract.Assert(state == State.PostOpcode);
				Contract.Assert((builder.Encoding & InstructionEncoding.ModRM_Mask) == InstructionEncoding.ModRM_None);
				builder.Encoding = builder.Encoding.WithModRM(format);
				if (format != InstructionEncoding.ModRM_None && format != InstructionEncoding.ModRM_Any)
					builder.Opcode = builder.Opcode.WithExtraByte(@byte);
				AdvanceTo(State.PostModRM);
			}

			private void SetImmediateSize(InstructionEncoding size)
			{
				Contract.Assert(state >= State.PostOpcode);

				var currentSize = builder.Encoding & InstructionEncoding.ImmediateSize_Mask;
				if (currentSize == InstructionEncoding.ImmediateSize_8 && size == InstructionEncoding.ImmediateSize_8)
				{
					// ib ib
					size = InstructionEncoding.ImmediateSize_16;
				}
				else if ((currentSize == InstructionEncoding.ImmediateSize_16 && size == InstructionEncoding.ImmediateSize_8)
					|| (currentSize == InstructionEncoding.ImmediateSize_8 && size == InstructionEncoding.ImmediateSize_16))
				{
					// iw ib, ib iw
					size = InstructionEncoding.ImmediateSize_24;
				}
				else if (currentSize == InstructionEncoding.ImmediateSize_16 && size == InstructionEncoding.ImmediateSize_16)
				{
					// iw iw
					size = InstructionEncoding.ImmediateSize_32;
				}
				else if ((currentSize == InstructionEncoding.ImmediateSize_16Or32 && size == InstructionEncoding.ImmediateSize_16)
					|| (currentSize == InstructionEncoding.ImmediateSize_16 && size == InstructionEncoding.ImmediateSize_16Or32))
				{
					// iwd iw, iw iwd
					size = InstructionEncoding.ImmediateSize_32Or48;
				}
				else if ((currentSize == InstructionEncoding.ImmediateSize_32 && size == InstructionEncoding.ImmediateSize_16)
					|| (currentSize == InstructionEncoding.ImmediateSize_16 && size == InstructionEncoding.ImmediateSize_32))
				{
					// id iw, iw id
					size = InstructionEncoding.ImmediateSize_48;
				}
				else
				{
					Contract.Assert(currentSize == InstructionEncoding.ImmediateSize_0);
				}

				builder.Encoding = builder.Encoding.WithImmediateSize(size);
				AdvanceTo(State.Immediates);
			}

			private void AdvanceTo(State newState)
			{
				Contract.Requires(newState >= state);
				state = newState;
			}
		}

		public InstructionDefinition ToInstructionDefinition()
		{
			return new InstructionDefinitionConverter().Convert(this);
		}
	}
}
