using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	public sealed class InstructionDictionary
	{
		private readonly MultiValueDictionary<string, InstructionDefinition> byMnemonic
			= new MultiValueDictionary<string, InstructionDefinition>();
		private readonly MultiValueDictionary<Opcode, InstructionDefinition> byOpcodeKey
			= new MultiValueDictionary<Opcode, InstructionDefinition>();

		public void Add(InstructionDefinition instruction)
		{
			Contract.Requires(instruction != null);
			byMnemonic.Add(instruction.Mnemonic, instruction);
            byOpcodeKey.Add(GetOpcodeKey(instruction.Opcode), instruction);
		}

		public InstructionDefinition Find(Opcode opcode)
		{
			IReadOnlyCollection<InstructionDefinition> candidates;
			if (!byOpcodeKey.TryGetValue(GetOpcodeKey(opcode), out candidates))
				return null;

			foreach (var candidate in candidates)
				if (candidate.IsMatch(opcode))
					return candidate;

			return null;
		}

		private static Opcode GetOpcodeKey(Opcode opcode)
		{
			// Don't include the SIMD prefix since callers can't distinguish them from legacy prefixes.
			return opcode & (Opcode.XexType_Mask | Opcode.Map_Mask | Opcode.MainByte_High4Mask);
		}
	}
}
