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
	}
}
