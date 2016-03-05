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

		public bool IsMatch(Instruction instruction)
		{
			if (IsAssembleOnly || IsPseudo) return false;

			var state = NasmEncodingParsingState.Prefixes;
			foreach (var token in encodingTokens)
			{
				switch (token.Type)
				{
					// Address size
					case NasmEncodingTokenType.AddressSize_Fixed16:
						if (instruction.EffectiveAddressSize != AddressSize._16) return false;
						break;

					case NasmEncodingTokenType.AddressSize_Fixed32:
						if (instruction.EffectiveAddressSize != AddressSize._32) return false;
						break;

					case NasmEncodingTokenType.AddressSize_Fixed64:
						if (instruction.EffectiveAddressSize != AddressSize._64) return false;
						break;

					case NasmEncodingTokenType.AddressSize_NoOverride:
						if (instruction.EffectiveAddressSize != instruction.DefaultAddressSize) return false;
						break;

					// Operand size
					case NasmEncodingTokenType.OperandSize_16:
						if (GetIntegerOperandSize(instruction) != OperandSize.Word) return false;
						break;

					case NasmEncodingTokenType.OperandSize_32:
						if (GetIntegerOperandSize(instruction) != OperandSize.Dword) return false;
						break;

					case NasmEncodingTokenType.OperandSize_64:
						if (GetIntegerOperandSize(instruction) != OperandSize.Qword) return false;
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

					case NasmEncodingTokenType.LegacyPrefix_NoRep:
						if (instruction.LegacyPrefixes.Contains(LegacyPrefix.RepeatNotEqual)
							|| instruction.LegacyPrefixes.Contains(LegacyPrefix.RepeatEqual)) return false;
						break;

					// Rex
					case NasmEncodingTokenType.Rex_NoB:
						if (instruction.Xex.BaseRegExtension) return false;
						break;

					case NasmEncodingTokenType.Rex_NoW:
						if (instruction.Xex.OperandSize64) return false;
						break;
					
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
							if (!instruction.Xex.Type.AllowsEscapes() || instruction.OpcodeMap == OpcodeMap.Default) return false;
							state = NasmEncodingParsingState.Escape0F;
							continue;
						}

						if (state == NasmEncodingParsingState.Escape0F && (token.Byte == 0x38 || token.Byte == 0x3A))
						{
							var map = token.Byte == 0x38 ? OpcodeMap.Escape0F38 : OpcodeMap.Escape0F3A;
							if (instruction.OpcodeMap != map) return false;
							state = NasmEncodingParsingState.PostEscape;
							continue;
						}

						if (state < NasmEncodingParsingState.PostOpcode)
						{
							if (instruction.MainByte != token.Byte) return false;
							state = NasmEncodingParsingState.PostOpcode;
						}

						throw new NotImplementedException();

					case NasmEncodingTokenType.Byte_PlusConditionCode:
					case NasmEncodingTokenType.Byte_PlusRegister:
					{
						if (state > NasmEncodingParsingState.PostOpcode)
							throw new NotImplementedException();

						byte mask = token.Type == NasmEncodingTokenType.Byte_PlusConditionCode ? (byte)0xF0 : (byte)0xF8;
						if ((instruction.MainByte & mask) != token.Byte) return false;
						state = NasmEncodingParsingState.PostOpcode;

						break;
					}

					// ModRM
					case NasmEncodingTokenType.ModRM:
						if (!instruction.ModRM.HasValue) return false;
						break;

					case NasmEncodingTokenType.ModRM_FixedReg:
						if (!instruction.ModRM.HasValue || instruction.ModRM.Value.GetReg() != token.Byte) return false;
						break;

					// VectorSib
					case NasmEncodingTokenType.VectorSib_XmmDwordIndices:
					case NasmEncodingTokenType.VectorSib_XmmQwordIndices:
					case NasmEncodingTokenType.VectorSib_YmmDwordIndices:
					case NasmEncodingTokenType.VectorSib_YmmQwordIndices:
					case NasmEncodingTokenType.VectorSib_ZmmDwordIndices:
					case NasmEncodingTokenType.VectorSib_ZmmQwordIndices:
						if (!instruction.Sib.HasValue) return false;
						break;

					default:
						throw new NotImplementedException($"Nasm token {token}");
				}
			}

			return true;
		}

		private static OperandSize GetIntegerOperandSize(Instruction instruction)
		{
			if (instruction.DefaultAddressSize == AddressSize._64 && instruction.Xex.OperandSize64)
				return OperandSize.Qword;
			var operandSize = instruction.DefaultAddressSize == AddressSize._16 ? OperandSize.Word : OperandSize.Dword;
			return operandSize.OverrideWordDword(instruction.LegacyPrefixes.HasOperandSizeOverride);
		}
	}
}
