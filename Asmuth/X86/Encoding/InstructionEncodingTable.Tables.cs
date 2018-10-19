using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Encoding
{
	partial class InstructionEncodingTable
	{
		// Each bit indicates if the corresponding non-escaped opcode has a ModRM byte
		private static readonly ushort[] opcode_NoEscape_HasModRM =
		{
			0b0000_1111_0000_1111, // 0x00 to 0x0F: add, or
			0b0000_1111_0000_1111, // 0x10 to 0x1F: adc, sbb
			0b0000_1111_0000_1111, // 0x20 to 0x2F: and, sub
			0b0000_1111_0000_1111, // 0x30 to 0x3F: xor, cmp
			0b0000_0000_0000_0000, // 0x40 to 0x4F: -
			0b0000_0000_0000_0000, // 0x50 to 0x5F: -
			0b0000_1010_0000_1100, // 0x60 to 0x6F: bound, arpl, imul
			0b0000_0000_0000_0000, // 0x70 to 0x7F: -
			0b1111_1111_1111_1111, // 0x80 to 0x8F: alu, test, xchg, mov, lea, pop
			0b0000_0000_0000_0000, // 0x90 to 0x9F: -
			0b0000_0000_0000_0000, // 0xA0 to 0xAF: -
			0b0000_0000_0000_0000, // 0xB0 to 0xBF: -
			0b0000_0000_1100_0011, // 0xC0 to 0xCF: rotate, mov
			0b1111_1111_0000_1111, // 0xD0 to 0xDF: rotate, fpu 
			0b0000_0000_0000_0000, // 0xE0 to 0xEF: -
			0b1100_0000_1100_0000, // 0xF0 to 0xFF: alu, inc, dec, call(f), jmp(f), push
		};

		// Each bit indicates if the corresponding 0F-escaped opcode modRM lookup is valid
		private static readonly ushort[] opcode_Escape0F_HasModRMValid =
		{
			0b0111_1111_1100_1111, // 0x00 to 0x0F: All but 0x04, 0x05, 0x0F
			0b1111_1111_1111_1111, // 0x10 to 0x1F: All
			0b1111_1111_0000_1111, // 0x20 to 0x2F: All but 0x24-0x27
			0b0000_0000_1011_1111, // 0x30 to 0x3F: All but 0x36, 0x38-0x3F
			0b1111_1111_1111_1111, // 0x40 to 0x4F: All
			0b1111_1111_1111_1111, // 0x50 to 0x5F: All
			0b1111_1111_1111_1111, // 0x60 to 0x6F: All
			0b1111_0011_1111_1111, // 0x70 to 0x7F: All but 0x7A, 0x7B
			0b1111_1111_1111_1111, // 0x80 to 0x8F: All
			0b1111_1111_1111_1111, // 0x90 to 0x9F: All
			0b1111_1111_0011_1111, // 0xA0 to 0xAF: All but 0xA6, 0xA7
			0b1111_1111_1111_1111, // 0xB0 to 0xBF: All
			0b1111_1111_1111_1111, // 0xC0 to 0xCF: All
			0b1111_1111_1111_1111, // 0xD0 to 0xDF: All
			0b1111_1111_1111_1111, // 0xE0 to 0xEF: All
			0b0111_1111_1111_1111, // 0xF0 to 0xFF: All but 0xFF
		};


		// Each bit indicates if the corresponding 0F-escaped opcode has a modRM byte
		private static readonly ushort[] opcode_Escape0F_HasModRM =
		{
			0b0100_0000_0000_1111, // 0x00 to 0x0F
			0b1000_0001_1111_1111, // 0x10 to 0x1F
			0b1111_1111_0000_1111, // 0x20 to 0x2F
			0b0000_0000_0000_0000, // 0x30 to 0x3F
			0b1111_1111_1111_1111, // 0x40 to 0x4F
			0b1111_1111_1111_1111, // 0x50 to 0x5F
			0b1111_1111_1111_1111, // 0x60 to 0x6F
			0b1111_0011_1111_1111, // 0x70 to 0x7F
			0b0000_0000_0000_0000, // 0x80 to 0x8F
			0b1111_1111_1111_1111, // 0x90 to 0x9F
			0b1111_1000_0011_1000, // 0xA0 to 0xAF
			0b1111_1111_1111_1111, // 0xB0 to 0xBF
			0b0000_0000_1111_1111, // 0xC0 to 0xCF
			0b1111_1111_1111_1111, // 0xD0 to 0xDF
			0b1111_1111_1111_1111, // 0xE0 to 0xEF
			0b0111_1111_1111_1111, // 0xF0 to 0xFF			   
		};
	}
}
