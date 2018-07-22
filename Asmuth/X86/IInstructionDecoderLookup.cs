using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	/// <summary>
	/// Provides an <see cref="InstructionDecoder"/> with the means to query encoding information from an instruction database.
	/// </summary>
	public interface IInstructionDecoderLookup
	{
		object TryLookup(CodeContext codeContext,
			ImmutableLegacyPrefixList legacyPrefixes, Xex xex,
			byte opcode, byte? modReg,
			out bool hasModRM, // If true and null return, must lookup again after reading modRM
			out int immediateSizeInBytes); // Only valid on non-null return
	}
}
