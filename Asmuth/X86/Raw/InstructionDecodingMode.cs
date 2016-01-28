using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	public enum InstructionDecodingMode : byte
	{
		// 8086, virtual 8086 or system management modes
		_8086,

		// Protected or IA-32e compatibility modes
		IA32_Default16, // Code segment default operand/address size attribute of 16 bits
		IA32_Default32, // Code segment default operand/address size attribute of 32 bits

		// 64-bit mode
		SixtyFourBit
	}

	public static class InstructionDecodingModeEnum
	{
		public static int GetDefaultOperandSizeInBytes(this InstructionDecodingMode mode)
			=> (mode <= InstructionDecodingMode.IA32_Default16) ? 2 : 4;

		public static int GetDefaultAddressSizeInBytes(this InstructionDecodingMode mode)
		{
			switch (mode)
			{
				case InstructionDecodingMode._8086: return 2;
				case InstructionDecodingMode.IA32_Default16: return 2;
				case InstructionDecodingMode.IA32_Default32: return 4;
				case InstructionDecodingMode.SixtyFourBit: return 8;
				default: throw new ArgumentOutOfRangeException(nameof(mode));
			}
		}
	}
}
