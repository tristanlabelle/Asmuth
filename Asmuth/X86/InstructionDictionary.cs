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

		public InstructionDefinition Find(Opcode opcode)
		{
			var key = opcode & Opcode.LookupKey_Mask;
			foreach (var candidate in byOpcodeKey[opcode & Opcode.LookupKey_Mask])
				if (candidate.IsMatch(opcode))
					return candidate;
			return null;
		}

		bool IInstructionDecoderLookup.TryLookup(
			InstructionDecodingMode mode, ImmutableLegacyPrefixList legacyPrefixes,
			Xex xex, byte opcode, out bool hasModRM, out int immediateSizeInBytes)
		{
			var lookupKey = OpcodeEnum.MakeLookupKey(xex.OpcodeMap, opcode);
			foreach (var instruction in byOpcodeKey[lookupKey])
			{
				var encoding = instruction.Encoding;

				// Ensure we match the opcode
				if ((opcode & encoding.GetOpcodeMainByteFixedMask()) != instruction.Opcode.GetMainByte())
					continue;

				// Ensure we match the RexW requirements
				if ((encoding & InstructionEncoding.RexW_Mask) == InstructionEncoding.RexW_Fixed
					&& xex.OperandSize64 != ((instruction.Opcode & Opcode.RexW) != 0))
				{
					continue;
				}

				var operandSize = mode.GetEffectiveOperandSize(legacyPrefixes, xex);
				var addressSize = mode.GetEffectiveAddressSize(legacyPrefixes);
				hasModRM = encoding.HasModRM();
				immediateSizeInBytes = encoding.GetImmediatesSizeInBytes(operandSize, addressSize);
				return true;
			}

			hasModRM = false;
			immediateSizeInBytes = 0;
			return false;
		}
	}
}
