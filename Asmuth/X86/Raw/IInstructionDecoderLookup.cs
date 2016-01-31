using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	/// <summary>
	/// Provides an <see cref="InstructionDecoder"/> with the means to query encoding information from an instruction database.
	/// </summary>
	public interface IInstructionDecoderLookup
	{
		bool TryLookup(InstructionDecodingMode mode, Opcode opcode, out bool hasModRM, out OperandSize? immediateSize);
	}
}
