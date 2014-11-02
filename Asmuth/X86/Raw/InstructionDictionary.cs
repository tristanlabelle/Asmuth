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
		private readonly MultiValueDictionary<Opcode, InstructionDefinition> byOpcode
			= new MultiValueDictionary<Opcode, InstructionDefinition>();

		public void Add(InstructionDefinition instruction)
		{
			Contract.Requires(instruction != null);
		}

		public InstructionDefinition Find(Opcode opcode)
		{
			throw new NotImplementedException();
		}

		private static Opcode GetOpcodeLookupKey(Opcode opcode)
		{
			return opcode & (Opcode.SimdPrefix_Mask | Opcode.Map_Mask | Opcode.MainByte_High5Mask);
		}
	}
}
