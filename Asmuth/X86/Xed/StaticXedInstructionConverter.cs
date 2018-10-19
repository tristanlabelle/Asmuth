using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Asmuth.X86.Xed
{
	public sealed partial class StaticXedInstructionConverter
	{
		public static InstructionDefinition GetInstructionDefinition(
			string mnemonic, XedInstructionForm form)
		{
			var encoding = GetOpcodeEncoding(form.Pattern);
			var operands = GetOperandDefinitions(form.Operands).ToList();

			return new InstructionDefinition(new InstructionDefinition.Data
			{
				Mnemonic = mnemonic,
				Encoding = encoding,
				Operands = operands
			});
		}
	}
}
