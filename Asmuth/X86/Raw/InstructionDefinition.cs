using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	public struct InstructionDefinitionData
	{
		public string Mnemonic;
		public string Name;	// Human-readable
		public Opcode OpcodeMask;
		public Opcode OpcodeValue;
		public byte SupportedProcessorModes;
		public byte ImmediateSize;
		public Mode64OperandSizePolicy mode64OperandSizePolicy;
		public EFlags FlagsAffectedMask;
	}

	public sealed class InstructionDefinition
	{
		public const Opcode RequiredMask = Opcode.SimdPrefix_Mask | Opcode.Map_Mask | Opcode.MainByte_High5Mask;
		private readonly InstructionDefinitionData data;

		public InstructionDefinition(InstructionDefinitionData data)
		{
			Contract.Requires((data.OpcodeMask & RequiredMask) == RequiredMask);
			this.data = data;
		}

		#region Properties
		public string Mnemonic => data.Mnemonic;
		public string Name => data.Name;
		public Opcode OpcodeMask => data.OpcodeMask;
		public Opcode OpcodeValue => data.OpcodeValue;

		// Even if ModRM is not in the required mask, it can be set in the value to indicate that some value is expected.
		public bool HasModRM => ((OpcodeMask | OpcodeValue) & Opcode.ModRM_Mask) != 0;
		#endregion

		public bool IsMatch(Opcode opcode) => (opcode & OpcodeMask) == OpcodeValue;
	}

	public sealed class OperandDefinition
	{
		public InstructionOperandFields Field;
		public AccessType AccessType;
	}
}
