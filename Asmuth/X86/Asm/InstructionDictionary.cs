using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Asm
{
	public sealed partial class InstructionDictionary : IInstructionDecoderLookup
	{
		private readonly MultiDictionary<string, InstructionDefinition> byMnemonic
			= new MultiDictionary<string, InstructionDefinition>();
		private readonly MultiDictionary<EncodingLookupKey, InstructionDefinition> byEncoding
			= new MultiDictionary<EncodingLookupKey, InstructionDefinition>();

		public void Add(InstructionDefinition instruction)
		{
			Contract.Requires(instruction != null);
			byMnemonic.Add(instruction.Mnemonic, instruction);
			byEncoding.Add(GetLookupKey(instruction.Encoding), instruction);
		}

		public InstructionDefinition Find(in Instruction instruction)
		{
			var key = GetEncodingLookupKey(instruction);
			foreach (var candidate in byEncoding[key])
				if (candidate.Encoding.IsMatch(instruction))
					return candidate;
			return null;
		}
		
		object IInstructionDecoderLookup.TryLookup(
			CodeSegmentType codeSegmentType, ImmutableLegacyPrefixList legacyPrefixes,
			Xex xex, byte opcode, ModRM? modRM,
			out bool hasModRM, out int immediateSizeInBytes)
		{
			var operandSize = codeSegmentType.GetIntegerOperandSize(legacyPrefixes, xex);
			var addressSize = codeSegmentType.GetEffectiveAddressSize(legacyPrefixes);

			InstructionDefinition match = null;
			var lookupKey = GetEncodingLookupKey(legacyPrefixes, xex, opcode);
			foreach (var instruction in byEncoding[lookupKey])
			{
				if (!instruction.Encoding.IsMatchUpToMainByte(codeSegmentType, legacyPrefixes, xex, opcode))
					continue;

				if (match != null) throw new NotImplementedException();

				match = instruction;
			}
			
			if (match == null)
			{
				hasModRM = false;
				immediateSizeInBytes = -1;
				return null;
			}

			hasModRM = match.Encoding.HasModRM;
			immediateSizeInBytes = match.Encoding.ImmediateSizeInBytes;
			return match;
		}
	}
}
