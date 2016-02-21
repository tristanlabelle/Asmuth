using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	public sealed class InstructionDictionary : IInstructionDecoderLookup
	{
		private readonly MultiDictionary<string, InstructionDefinition> byMnemonic
			= new MultiDictionary<string, InstructionDefinition>();
		private readonly MultiDictionary<Opcode, InstructionDefinition> byOpcodeKey
			= new MultiDictionary<Opcode, InstructionDefinition>();

		public void Add(InstructionDefinition instruction)
		{
			Contract.Requires(instruction != null);
			byMnemonic.Add(instruction.Mnemonic, instruction);
			byOpcodeKey.Add(instruction.Opcode & Opcode.LookupKey_Mask, instruction);
		}

		public InstructionDefinition Find(Opcode opcode, bool explicitSimdPrefix)
		{
			var key = opcode & Opcode.LookupKey_Mask;
			foreach (var candidate in byOpcodeKey[key])
				if (candidate.IsMatch(opcode))
					return candidate;
			return explicitSimdPrefix ? null : Find(opcode.WithSimdPrefix(SimdPrefix.None), explicitSimdPrefix: true);
		}

		private static int GetOperandSizeMatchLevel(InstructionEncoding encoding, OperandSize size)
		{
			switch (encoding & InstructionEncoding.OperandSize_Mask)
			{
				case InstructionEncoding.OperandSize_Ignored: return 0;
				case InstructionEncoding.OperandSize_16Or32Or64: return 1;
				case InstructionEncoding.OperandSize_16Or32: return 2;
				case InstructionEncoding.OperandSize_32Or64: return 2;
				case InstructionEncoding.OperandSize_Fixed8: return 3;
				case InstructionEncoding.OperandSize_Fixed16: return size == OperandSize.Word ? 3 : -1;
				case InstructionEncoding.OperandSize_Fixed32: return size == OperandSize.Dword ? 3 : -1;
				case InstructionEncoding.OperandSize_Fixed64: return size == OperandSize.Qword ? 3 : -1;
				default: throw new ArgumentException();
			}
		}


		bool IInstructionDecoderLookup.TryLookup(
			InstructionDecodingMode mode, ImmutableLegacyPrefixList legacyPrefixes,
			Xex xex, byte opcode, out bool hasModRM, out int immediateSizeInBytes)
		{
			var operandSize = mode.GetEffectiveOperandSize(legacyPrefixes, xex);
			var addressSize = mode.GetEffectiveAddressSize(legacyPrefixes);
			InstructionDefinition bestMatch = null;
			int bestOperandSizeMatchLevel = -1;

			var lookupKey = OpcodeEnum.MakeLookupKey(xex.OpcodeMap, opcode);
			foreach (var instruction in byOpcodeKey[lookupKey])
			{
				var encoding = instruction.Encoding;

				// Ensure we (loosely) match the SIMD prefix
				var simdPrefix = instruction.Opcode.GetSimdPrefix();
				if (xex.SimdPrefix.HasValue && xex.SimdPrefix.Value != simdPrefix)
					continue;
				if (simdPrefix != SimdPrefix.None && legacyPrefixes.PotentialSimdPrefix != simdPrefix)
					continue;

				// Ensure we match the RexW requirements
				if ((encoding & InstructionEncoding.RexW_Mask) == InstructionEncoding.RexW_Fixed
					&& xex.OperandSize64 != ((instruction.Opcode & Opcode.RexW) != 0))
				{
					continue;
				}

				// Ensure we match the opcode
				if ((opcode & encoding.GetOpcodeMainByteFixedMask()) != instruction.Opcode.GetMainByte())
					continue;

				// Record the candidate, favoring more specific operand size matches
				var operandSizeMatchLevel = GetOperandSizeMatchLevel(encoding, operandSize);
				if (operandSizeMatchLevel > bestOperandSizeMatchLevel)
				{
					bestMatch = instruction;
					bestOperandSizeMatchLevel = operandSizeMatchLevel;
				}
			}

			if (bestMatch == null)
			{
				hasModRM = false;
				immediateSizeInBytes = 0;
				return false;
			}

			hasModRM = bestMatch.Encoding.HasModRM();
			immediateSizeInBytes = bestMatch.Encoding.GetImmediatesSizeInBytes(operandSize, addressSize);
			return true;
		}
	}
}
