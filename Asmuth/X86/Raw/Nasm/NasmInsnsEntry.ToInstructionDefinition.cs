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
				Initial,
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
				state = State.Initial;

				builder.Mnemonic = entry.Mnemonic;

				foreach (var token in entry.EncodingTokens)
				{
					switch (token.Type)
					{
						case NasmEncodingTokenType.LegacyPrefix_F2:
							SetSimdPrefix(InstructionEncoding.SimdPrefix_F2);
							AdvanceTo(State.PostSimdPrefix);
							continue;

						case NasmEncodingTokenType.LegacyPrefix_F3:
							SetSimdPrefix(InstructionEncoding.SimdPrefix_F3);
							AdvanceTo(State.PostSimdPrefix);
							continue;

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
									builder.Encoding = (builder.Encoding & ~InstructionEncoding.OpcodeMap_Mask) | InstructionEncoding.OpcodeMap_0F;
									continue;
								}

								if ((builder.Encoding & InstructionEncoding.OpcodeMap_Mask) == InstructionEncoding.OpcodeMap_0F
									&& (token.Byte == 0x38 || token.Byte == 0x3A))
								{
									var map = token.Byte == 0x38 ? InstructionEncoding.OpcodeMap_0F38 : InstructionEncoding.OpcodeMap_0F3A;
									builder.Encoding = (builder.Encoding & ~InstructionEncoding.OpcodeMap_Mask) | map;
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

						case NasmEncodingTokenType.ModRM: SetModRM(InstructionEncoding.OpcodeForm_OneByte_WithModRM); break;
						case NasmEncodingTokenType.ModRM_FixedReg: SetModRM(InstructionEncoding.OpcodeForm_ExtendedByModReg); break;

						case NasmEncodingTokenType.Immediate_Byte: SetImmediateSize(InstructionEncoding.ImmediateSize_8); break;
						case NasmEncodingTokenType.Immediate_Byte_Signed: SetImmediateSize(InstructionEncoding.ImmediateSize_16); break;
						case NasmEncodingTokenType.Immediate_Byte_Unsigned: SetImmediateSize(InstructionEncoding.ImmediateSize_32); break;

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
				Contract.Assert((builder.Encoding & InstructionEncoding.ImmediateSize_Mask) == InstructionEncoding.ImmediateSize_0);
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
