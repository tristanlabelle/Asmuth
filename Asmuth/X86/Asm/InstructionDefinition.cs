using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Asm
{
	public sealed class InstructionDefinition
	{
		public struct Data
		{
			public string Mnemonic;
			public IReadOnlyList<OperandFormat> Operands;
			public OpcodeEncoding Encoding;
			public CpuidFeatureFlags RequiredFeatureFlags;
			public Flags? AffectedFlags;
		}

		#region Fields
		private readonly Data data;
		#endregion

		#region Constructor
		public InstructionDefinition(in Data data)
		{
			if (data.Mnemonic == null || data.Operands == null)
				throw new ArgumentException("Some data fields are null.", nameof(data));
			this.data = data;
			this.data.Operands = data.Operands.ToArray();
		}
		#endregion

		#region Properties
		public string Mnemonic => data.Mnemonic;
		public IReadOnlyList<OperandFormat> Operands => data.Operands;
		public OpcodeEncoding Encoding => data.Encoding;
		public CpuidFeatureFlags RequiredFeatureFlags => data.RequiredFeatureFlags;
		public Flags? AffectedFlags => data.AffectedFlags;
		#endregion

		#region Methods
		public override string ToString()
		{
			var stringBuilder = new StringBuilder(Mnemonic.Length + Operands.Count * 6);

			stringBuilder.Append(Mnemonic);

			bool firstOperand = true;
			foreach (var operand in Operands)
			{
				stringBuilder.Append(firstOperand ? " " : ", ");
				stringBuilder.Append(operand);
				firstOperand = false;
			}

			return stringBuilder.ToString();
		}
		#endregion
	}
}
