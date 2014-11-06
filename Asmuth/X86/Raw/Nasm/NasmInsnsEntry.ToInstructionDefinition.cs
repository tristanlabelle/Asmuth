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
						case NasmEncodingTokenType.AddressSize_Fixed16:
						case NasmEncodingTokenType.AddressSize_Fixed32:
						case NasmEncodingTokenType.AddressSize_Fixed64:
							Contract.Assert(state == State.Prefixes);
							Contract.Assert(addressSize == 0);
							addressSize = token.Type;
							break;

						case NasmEncodingTokenType.OperandSize_Fixed16:
						case NasmEncodingTokenType.OperandSize_Fixed32:
						case NasmEncodingTokenType.OperandSize_Fixed64:
						case NasmEncodingTokenType.OperandSize_Fixed64_RexExtensionsOnly:
						case NasmEncodingTokenType.OperandSize_NoOverride:
							Contract.Assert(state == State.Prefixes);
							Contract.Assert(addressSize == 0);
							operandSize = token.Type;
							break;

						// Legacy prefixes
						case NasmEncodingTokenType.LegacyPrefix_F2:
							SetSimdPrefix(InstructionEncoding.SimdPrefix_F2);
							continue;

						case NasmEncodingTokenType.LegacyPrefix_F3:
							SetSimdPrefix(InstructionEncoding.SimdPrefix_F3);
							continue;

						case NasmEncodingTokenType.LegacyPrefix_NoF3:
						case NasmEncodingTokenType.LegacyPrefix_HleAlways:
						case NasmEncodingTokenType.LegacyPrefix_HleWithLock:
						case NasmEncodingTokenType.LegacyPrefix_DisassembleRepAsRepE:
							break;

						case NasmEncodingTokenType.Byte:
							if (state < State.PostSimdPrefix)
							{
								switch (token.Byte)
								{
									case 0x66: SetSimdPrefix(InstructionEncoding.SimdPrefix_66); AdvanceTo(State.PostSimdPrefix); continue;
									case 0xF2: SetSimdPrefix(InstructionEncoding.SimdPrefix_F2); AdvanceTo(State.PostSimdPrefix); continue;
									case 0xF3: SetSimdPrefix(InstructionEncoding.SimdPrefix_F3); AdvanceTo(State.PostSimdPrefix); continue;
								}
							}

							if (state < State.PostOpcode)
							{
								if ((builder.Encoding & InstructionEncoding.OpcodeMap_Mask) == InstructionEncoding.OpcodeMap_OneByte
									&& token.Byte == 0x0F)
								{
									builder.Encoding = builder.Encoding.WithOpcodeMap(InstructionEncoding.OpcodeMap_0F);
									continue;
								}

								if ((builder.Encoding & InstructionEncoding.OpcodeMap_Mask) == InstructionEncoding.OpcodeMap_0F
									&& (token.Byte == 0x38 || token.Byte == 0x3A))
								{
									builder.Encoding = builder.Encoding.WithOpcodeMap(
										token.Byte == 0x38 ? InstructionEncoding.OpcodeMap_0F38 : InstructionEncoding.OpcodeMap_0F3A);
									continue;
								}

								builder.Encoding = builder.Encoding
									.WithOpcodeForm(InstructionEncoding.OpcodeForm_OneByte)
									.WithOpcodeByte(token.Byte);
								AdvanceTo(State.PostOpcode);
								continue;
							}
							
							if (state == State.PostOpcode)
							{
								SetModRM(InstructionEncoding.OpcodeForm_ExtendedByModRM, token.Byte);
								continue;
							}
							break;

						case NasmEncodingTokenType.Byte_PlusRegister:
							Contract.Assert(state < State.PostOpcode);
							Contract.Assert((token.Byte & 7) == 0);
							builder.Encoding = builder.Encoding
								.WithOpcodeForm(InstructionEncoding.OpcodeForm_EmbeddedRegister)
								.WithOpcodeByte(token.Byte);
							AdvanceTo(State.PostOpcode);
							break;

						case NasmEncodingTokenType.ModRM: SetModRM(InstructionEncoding.OpcodeForm_OneByte_WithModRM); break;
						case NasmEncodingTokenType.ModRM_FixedReg: SetModRM(InstructionEncoding.OpcodeForm_ExtendedByModReg); break;

						// Immediates
						case NasmEncodingTokenType.Immediate_Byte: SetImmediateSize(InstructionEncoding.ImmediateSize_8); break;
						case NasmEncodingTokenType.Immediate_Byte_Signed: SetImmediateSize(InstructionEncoding.ImmediateSize_8); break;
						case NasmEncodingTokenType.Immediate_Byte_Unsigned: SetImmediateSize(InstructionEncoding.ImmediateSize_8); break;
						case NasmEncodingTokenType.Immediate_Word: SetImmediateSize(InstructionEncoding.ImmediateSize_16); break;
						case NasmEncodingTokenType.Immediate_Dword: SetImmediateSize(InstructionEncoding.ImmediateSize_32); break;
						case NasmEncodingTokenType.Immediate_Dword_Signed: SetImmediateSize(InstructionEncoding.ImmediateSize_32); break;
						case NasmEncodingTokenType.Immediate_WordOrDword: SetImmediateSize(InstructionEncoding.ImmediateSize_16Or32); break;
						case NasmEncodingTokenType.Immediate_WordOrDwordOrQword: SetImmediateSize(InstructionEncoding.ImmediateSize_16Or32Or64); break;

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

						default:
							throw new NotImplementedException("Handling NASM encoding tokens of type '{0}'".FormatInvariant(token.Type));
					}
				}

				return builder.Build(reuse: false);
			}

			private void SetSimdPrefix(InstructionEncoding prefix)
			{
				Contract.Assert((builder.Encoding & InstructionEncoding.SimdPrefix_Mask) == InstructionEncoding.SimdPrefix_None);
				builder.Encoding |= prefix;
				AdvanceTo(State.PostSimdPrefix);
			}

			private void SetModRM(InstructionEncoding opcodeForm, byte value = 0)
			{
				Contract.Assert(state == State.PostOpcode);
				Contract.Assert((builder.Encoding & InstructionEncoding.OpcodeForm_Mask) == InstructionEncoding.OpcodeForm_OneByte);
				builder.Encoding = builder.Encoding
					.WithOpcodeForm(InstructionEncoding.OpcodeForm_ExtendedByModRM)
					.WithOpcodeExtraByte(value);
				AdvanceTo(State.PostModRM);
			}

			private void SetImmediateSize(InstructionEncoding size)
			{
				Contract.Assert(state >= State.PostOpcode);

				var currentSize = builder.Encoding & InstructionEncoding.ImmediateSize_Mask;
				if (currentSize == InstructionEncoding.ImmediateSize_16 && size == InstructionEncoding.ImmediateSize_16)
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
