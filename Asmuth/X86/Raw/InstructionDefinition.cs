using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	public sealed class InstructionDefinition
	{
		#region Fields
		private string mnemonic;
		private InstructionEncoding encoding;
		private CpuidFeatureFlags requiredFeatureFlags;
		private EFlags? affectedFlags;
		private IList<OperandDefinition> operands;
		#endregion

		#region Constructor
		private InstructionDefinition() { }
		#endregion

		#region Properties
		public string Mnemonic => mnemonic;
		public InstructionEncoding Encoding => encoding;
		public CpuidFeatureFlags RequiredFeatureFlags => requiredFeatureFlags;
		public EFlags? AffectedFlags => affectedFlags;
		public IReadOnlyList<OperandDefinition> Operands => (IReadOnlyList<OperandDefinition>)operands;
		#endregion

		#region Methods
		public override string ToString() => Mnemonic;
		#endregion

		#region Builder class
		public sealed class Builder
		{
			private InstructionDefinition instruction = CreateEmpty();

			#region Properties
			public string Mnemonic
			{
				get { return instruction.mnemonic; }
				set { instruction.mnemonic = value; }
			}

			public InstructionEncoding Encoding
			{
				get { return instruction.encoding; }
				set { instruction.encoding = value; }
			}

			public CpuidFeatureFlags RequiredFeatureFlags
			{
				get { return instruction.requiredFeatureFlags; }
				set { instruction.requiredFeatureFlags = value; }
			}

			public EFlags? AffectedFlags
			{
				get { return instruction.affectedFlags; }
				set { instruction.affectedFlags = value; }
			}

			public IList<OperandDefinition> Operands => instruction.operands;
			#endregion

			#region Methods
			public InstructionDefinition Build(bool reuse = true)
			{
				var result = instruction;
				instruction = reuse ? CreateEmpty() : null;
				return result;
			}

			private static InstructionDefinition CreateEmpty()
			{
				var instruction = new InstructionDefinition();
				instruction.operands = new List<OperandDefinition>();
				return instruction;
			}
			#endregion
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
