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
		public InstructionEncoding Encoding;
		public CpuidFeatureFlags RequiredFeatureFlags;
		public EFlags AffectedFlags;
		public OperandDefinition[] Operands;
	}

	public sealed class InstructionDefinition
	{
		#region Fields
		private readonly InstructionDefinitionData data;
		#endregion

		#region Constructor
		public InstructionDefinition(InstructionDefinitionData data)
		{
			Contract.Requires(data.Mnemonic != null);
			Contract.Requires(data.Operands != null);
			this.data = data;
		}
		#endregion

		#region Properties
		public string Mnemonic => data.Mnemonic;
		public InstructionEncoding Encoding => data.Encoding;
		public IReadOnlyList<OperandDefinition> Operands => data.Operands;
		#endregion

		#region Methods
		public override string ToString()
		{
			return Mnemonic;
		}
		#endregion
	}

	public struct OperandDefinition
	{
		public readonly OperandEncoding Encoding;
		public readonly OperandFields Field;
		public readonly AccessType AccessType;

		public OperandDefinition(OperandFields field, OperandEncoding encoding, AccessType accessType)
		{
			this.Field = field;
			this.Encoding = encoding;
			this.AccessType = accessType;
		}
	}
}
