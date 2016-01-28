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
		bool TryLookup(InstructionDecodingMode mode, Opcode opcode, out bool hasModRM, out int immediateSize);
	}

	[Flags]
	public enum InstructionSuffixFlags : byte
	{
		None = 0,
		HasModRM = 1 << 0,
		AllowSib = 1 << 1,
		AllowDisplacement = 1 << 2,
		HasImmediate = 1 << 3,
		DefaultsTo16BitsAddressing = 1 << 4,
	}
}
