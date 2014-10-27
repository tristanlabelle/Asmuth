using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	public enum OperandType : byte
	{
		None = 0x00,
		
		// Immediates: rel, imm, ptr, moffs
		
		Relative8,
		Relative16,
		Relative32,
		Ptr16_16,
		Ptr16_32,
		Imm8,
		Imm16,
		Imm32,
		Imm64,

		// Registers
		Reg8,
		Reg16,
		Reg32,
		Reg64,
		SegReg,
		ST,
		mm,
		xmm,
		ymm,

		RM8,
		RM16,
		RM32,
		RM64,
		mm_m32,
		mm_m64,
		xmm_m32,
		xmm_m64,
		xmm_m128,
		ymm_m256,
		zmm_m512,

		M8,
		M16,
		M32,
		M64,
		M128,
		M256,

		M_Seg16_Off16,
		M_Seg16_Off32,
		M_Seg16_Off64,

		M_16And16,
		M_16And32,
		M_32And32,
		M_16And64,
	}
}
