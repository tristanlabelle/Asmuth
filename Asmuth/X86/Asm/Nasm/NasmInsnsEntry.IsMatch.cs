using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Asm.Nasm
{
	partial class NasmInsnsEntry
	{
		private enum NasmEncodingParsingState
		{
			Prefixes,
			PostSimdPrefix,
			Escape0F,
			PostEscape,
			PreOpcode = PostEscape,
			PostOpcode,
			PostModRM,
			Immediates
		}

		public bool Match(
			CodeSegmentType codeSegmentType,
			ImmutableLegacyPrefixList legacyPrefixes, NonLegacyPrefixes nonLegacyPrefixes, byte mainByte,
			out bool hasModRM, out int immediateSize)
		{
			var partialInstruction = new Instruction.Builder
			{
				CodeSegmentType = codeSegmentType,
				LegacyPrefixes = legacyPrefixes,
				NonLegacyPrefixes = nonLegacyPrefixes,
				MainByte = mainByte
			}.Build();

			return Match(partialInstruction, upToOpcode: true, hasModRM: out hasModRM, immediateSize: out immediateSize);
		}

		public bool IsMatch(Instruction instruction)
		{
			return Match(instruction, upToOpcode: false,
				hasModRM: out var hasModRM,
				immediateSize: out var immediateSize);
		}

		private bool Match(Instruction instruction, bool upToOpcode,
			out bool hasModRM, out int immediateSize)
		{
			hasModRM = false;
			immediateSize = 0;
			if (IsAssembleOnly || IsPseudo) return false;

			var expectedNonLegacyPrefixForm = NonLegacyPrefixesForm.Escapes;
			var expectedOpcodeMap = OpcodeMap.Default;
			var state = NasmEncodingParsingState.Prefixes;
			foreach (var token in EncodingTokens)
			{
				switch (token.Type)
				{
					// Address size
					case NasmEncodingTokenType.AddressSize_16:
						if (instruction.EffectiveAddressSize != AddressSize._16) return false;
						break;

					case NasmEncodingTokenType.AddressSize_32:
						if (instruction.EffectiveAddressSize != AddressSize._32) return false;
						break;

					case NasmEncodingTokenType.AddressSize_64:
						if (instruction.EffectiveAddressSize != AddressSize._64) return false;
						break;

					case NasmEncodingTokenType.AddressSize_NoOverride:
						if (instruction.LegacyPrefixes.HasAddressSizeOverride) return false;
						break;

					// Operand size
					case NasmEncodingTokenType.OperandSize_16:
						if (GetIntegerOperandSize(instruction) != IntegerSize.Word) return false;
						break;

					case NasmEncodingTokenType.OperandSize_32:
						if (GetIntegerOperandSize(instruction) != IntegerSize.Dword) return false;
						break;

					case NasmEncodingTokenType.OperandSize_64:
						if (GetIntegerOperandSize(instruction) != IntegerSize.Qword) return false;
						break;

					case NasmEncodingTokenType.OperandSize_NoOverride:
						if (instruction.LegacyPrefixes.HasOperandSizeOverride) return false;
						break;

					case NasmEncodingTokenType.OperandSize_64WithoutW:
						if (!instruction.CodeSegmentType.IsLongMode()
							|| instruction.LegacyPrefixes.HasOperandSizeOverride) return false;
						break;

					// Legacy prefixes
					case NasmEncodingTokenType.LegacyPrefix_F2:
						if (!instruction.LegacyPrefixes.Contains(LegacyPrefix.RepeatNotEqual)) return false;
						break;

					case NasmEncodingTokenType.LegacyPrefix_F3:
						if (!instruction.LegacyPrefixes.Contains(LegacyPrefix.RepeatEqual)) return false;
						break;

					case NasmEncodingTokenType.LegacyPrefix_NoF3:
						if (instruction.LegacyPrefixes.Contains(LegacyPrefix.RepeatEqual)) return false;
						break;

					case NasmEncodingTokenType.LegacyPrefix_NoSimd:
						if (instruction.LegacyPrefixes.ContainsFromGroup(LegacyPrefixGroup.Repeat)
							|| instruction.LegacyPrefixes.ContainsFromGroup(LegacyPrefixGroup.OperandSizeOverride))
							return false;
						break;

					case NasmEncodingTokenType.LegacyPrefix_MustRep:
						if (instruction.LegacyPrefixes.Contains(LegacyPrefix.Repeat)) return false;
						break;

					case NasmEncodingTokenType.LegacyPrefix_NoRep:
						if (instruction.LegacyPrefixes.ContainsFromGroup(LegacyPrefixGroup.Repeat)) return false;
						break;

					case NasmEncodingTokenType.LegacyPrefix_DisassembleRepAsRepE:
					case NasmEncodingTokenType.LegacyPrefix_HleAlways:
					case NasmEncodingTokenType.LegacyPrefix_HleWithLock:
					case NasmEncodingTokenType.LegacyPrefix_XReleaseAlways:
						break;

					// Vex
					case NasmEncodingTokenType.Vex:
						if (instruction.NonLegacyPrefixes.Form.GetVexType() != VexType) return false;
						throw new NotImplementedException();

					// Rex
					case NasmEncodingTokenType.Rex_NoB:
						if (instruction.NonLegacyPrefixes.BaseRegExtension) return false;
						break;

					case NasmEncodingTokenType.Rex_NoW:
						if (instruction.NonLegacyPrefixes.OperandSize64) return false;
						break;

					case NasmEncodingTokenType.Rex_LockAsRexR: break;

					// Byte
					case NasmEncodingTokenType.Byte:
						if (state < NasmEncodingParsingState.PostSimdPrefix)
						{
							if (token.Byte == 0x66 || token.Byte == 0xF2 || token.Byte == 0xF3)
							{
								var legacyPrefix = LegacyPrefixEnum.TryFromEncodingByte(token.Byte).Value;
								if (!instruction.LegacyPrefixes.EndsWith(legacyPrefix)) return false;
								state = NasmEncodingParsingState.PostSimdPrefix;
								continue;
							}
						}

						if (state < NasmEncodingParsingState.Escape0F && token.Byte == 0x0F)
						{
							if (!instruction.NonLegacyPrefixes.Form.AllowsEscapes()) return false;
							expectedOpcodeMap = OpcodeMap.Escape0F;
							state = NasmEncodingParsingState.Escape0F;
							continue;
						}

						if (state == NasmEncodingParsingState.Escape0F && (token.Byte == 0x38 || token.Byte == 0x3A))
						{
							expectedOpcodeMap = token.Byte == 0x38 ? OpcodeMap.Escape0F38 : OpcodeMap.Escape0F3A;
							state = NasmEncodingParsingState.PostEscape;
							continue;
						}

						if (state < NasmEncodingParsingState.PostOpcode)
						{
							if (instruction.MainOpcodeByte != token.Byte) return false;
							state = NasmEncodingParsingState.PostOpcode;
							continue;
						}

						if (state == NasmEncodingParsingState.PostOpcode)
						{
							if (!upToOpcode && (byte?)instruction.ModRM != token.Byte) return false;
							hasModRM = true;
							state = NasmEncodingParsingState.PostModRM;
							continue;
						}

						// Constant imm?
						throw new NotImplementedException();

					case NasmEncodingTokenType.Byte_PlusConditionCode:
					case NasmEncodingTokenType.Byte_PlusRegister:
					{
						if (state > NasmEncodingParsingState.PostOpcode)
							throw new NotImplementedException();

						byte mask = token.Type == NasmEncodingTokenType.Byte_PlusConditionCode
							? (byte)0xF0 : (byte)0xF8;
						if (((byte)instruction.MainOpcodeByte & mask) != token.Byte) return false;
						state = NasmEncodingParsingState.PostOpcode;

						break;
					}

					// ModRM
					case NasmEncodingTokenType.ModRM:
						if (!upToOpcode && !instruction.ModRM.HasValue) return false;
						hasModRM = true;
						state = NasmEncodingParsingState.PostModRM;
						break;

					case NasmEncodingTokenType.ModRM_FixedReg:
						if (!upToOpcode && (!instruction.ModRM.HasValue || instruction.ModRM.Value.Reg != token.Byte)) return false;
						hasModRM = true;
						state = NasmEncodingParsingState.PostModRM;
						break;

					// VectorSib
					case NasmEncodingTokenType.VectorSib_XmmDwordIndices:
					case NasmEncodingTokenType.VectorSib_XmmQwordIndices:
					case NasmEncodingTokenType.VectorSib_YmmDwordIndices:
					case NasmEncodingTokenType.VectorSib_YmmQwordIndices:
					case NasmEncodingTokenType.VectorSib_Xmm:
					case NasmEncodingTokenType.VectorSib_Ymm:
					case NasmEncodingTokenType.VectorSib_Zmm:
						if (!instruction.Sib.HasValue) return false;
						break;

					// Immediates
					case NasmEncodingTokenType.Immediate_Byte:
					case NasmEncodingTokenType.Immediate_Byte_Signed:
					case NasmEncodingTokenType.Immediate_Byte_Unsigned:
					case NasmEncodingTokenType.Immediate_Is4:
					case NasmEncodingTokenType.Immediate_RelativeOffset8:
						immediateSize++;
						break;

					case NasmEncodingTokenType.Immediate_Word: immediateSize += 2; break;

					case NasmEncodingTokenType.Immediate_Dword:
					case NasmEncodingTokenType.Immediate_Dword_Signed:
						immediateSize += 4;
						break;

					case NasmEncodingTokenType.Immediate_Qword: immediateSize += 8; break;

					case NasmEncodingTokenType.Immediate_RelativeOffset:
						immediateSize += instruction.CodeSegmentType
							.GetWordOrDwordIntegerOperandSize(instruction.LegacyPrefixes, instruction.NonLegacyPrefixes)
							.InBytes();
						break;

					// Misc
					case NasmEncodingTokenType.Misc_WaitPrefix:
					case NasmEncodingTokenType.Misc_NoHigh8Register:
						break;

					default:
						throw new NotImplementedException($"Nasm token {token}");
				}
			}

			foreach (var operand in Operands)
			{
				if (operand.Field == OperandField.BaseReg)
				{
					var optype = operand.Type & NasmOperandType.OpType_Mask;
					var isReg = !instruction.ModRM.HasValue || instruction.ModRM.Value.IsDirect;
					if (optype == NasmOperandType.OpType_Register && !isReg) return false;
					if (optype == NasmOperandType.OpType_Memory && isReg) return false;
				}
			}
			
			return state >= NasmEncodingParsingState.PostOpcode
				&& (expectedNonLegacyPrefixForm == NonLegacyPrefixesForm.Escapes ? instruction.NonLegacyPrefixes.Form.AllowsEscapes() : instruction.NonLegacyPrefixes.Form == expectedNonLegacyPrefixForm)
				&& instruction.OpcodeMap == expectedOpcodeMap
				&& (upToOpcode || 
					(instruction.ModRM.HasValue == hasModRM
					&& instruction.ImmediateSizeInBytes == immediateSize));
		}

		private static IntegerSize GetIntegerOperandSize(Instruction instruction)
		{
			return instruction.CodeSegmentType.GetIntegerOperandSize(
				instruction.LegacyPrefixes, instruction.NonLegacyPrefixes);
		}
	}
}
