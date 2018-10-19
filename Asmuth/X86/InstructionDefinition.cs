using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	public readonly struct OperandDefinition
	{
		public OperandSpec Spec { get; }
		public OperandField? Field { get; }
		public AccessType Access { get; }

		public OperandDefinition(OperandSpec spec, OperandField? field, AccessType access)
		{
			if (!spec.DataLocation.IsWritable() && (access & AccessType.Write) == AccessType.Write)
				throw new ArgumentException();
			this.Spec = spec ?? throw new ArgumentNullException(nameof(spec));
			this.Field = field;
			this.Access = access;
		}

		public override string ToString() => Spec.ToString();
	}

	public sealed class InstructionDefinition
	{
		public struct Data
		{
			public string Mnemonic;
			public IReadOnlyList<OperandDefinition> Operands;
			public OpcodeEncoding Encoding;
			public CpuidFeatureFlags RequiredFeatureFlags;
			public EFlags? AffectedFlags;
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
		public IReadOnlyList<OperandDefinition> Operands => data.Operands;
		public OpcodeEncoding Encoding => data.Encoding;
		public CpuidFeatureFlags RequiredFeatureFlags => data.RequiredFeatureFlags;
		public EFlags? AffectedFlags => data.AffectedFlags;
		#endregion

		#region Methods
		public void FormatOperandList(TextWriter textWriter, in Instruction instruction, ulong? ip = null)
		{
			bool firstOperand = true;
			foreach (var operand in Operands)
			{
				textWriter.Write(firstOperand ? ' ' : ',');
				operand.Spec.Format(textWriter, in instruction, operand.Field, ip);
				firstOperand = false;
			}
		}

		public override string ToString()
		{
			var stringBuilder = new StringBuilder(Mnemonic.Length + Operands.Count * 6);

			stringBuilder.Append(Mnemonic);

			bool firstOperand = true;
			foreach (var operand in Operands)
			{
				stringBuilder.Append(firstOperand ? " " : ", ");
				stringBuilder.Append(operand.Spec);
				firstOperand = false;
			}

			stringBuilder.Append(": ");
			stringBuilder.Append(Encoding.ToString());

			return stringBuilder.ToString();
		}
		#endregion
	}
}
