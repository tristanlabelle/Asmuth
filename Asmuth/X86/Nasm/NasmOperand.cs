using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Nasm
{
	public struct NasmOperand : IEquatable<NasmOperand>
	{
		public readonly OperandFields Field;
		public readonly NasmOperandType Type;

		public NasmOperand(OperandFields field, NasmOperandType type)
		{
			this.Field = field;
			this.Type = type;
		}

		public bool Equals(NasmOperand other) => Type == other.Type && Field == other.Field;
		public override bool Equals(object obj) => obj is NasmOperand && Equals((NasmOperand)obj);
		public override int GetHashCode() => ((int)Field << 16) | (int)Type;

		public static bool Equals(NasmOperand first, NasmOperand second) => first.Equals(second);
		public static bool operator ==(NasmOperand lhs, NasmOperand rhs) => Equals(lhs, rhs);
		public static bool operator !=(NasmOperand lhs, NasmOperand rhs) => !Equals(lhs, rhs);
	}

	public enum NasmOperandType : byte
	{
		Void,

		Unity,

		Imm,
		Imm8,
		Imm16,
		Imm32,
		Imm64,

		SByteWord,
		SByteDword,
		SByteWord16,
		SByteDword32,
		SByteDword64,
		UDword,
		SDword,

		Reg8,
		Reg16,
		Reg32,
		Reg32NA,
		Reg64,
		Reg_AL,
		Reg_AX,
		Reg_Eax,
		Reg_Rax,
		Reg_DX,
		Reg_CX,
		Reg_CL,
		Reg_Ecx,
		Reg_Rcx,
		Reg_Edx,

		RM8,
		RM16,
		RM32,
		RM64,

		Reg_SReg,
		Reg_ES,
		Reg_CS,
		Reg_SS,
		Reg_DS,
		Reg_FS,
		Reg_GS,

		Reg_CReg,
		Reg_DReg,
		Reg_TReg,

		FpuReg,
		Fpu0,
		MmxReg,
		MmxRM,
		MmxRM64,

		Mem,
		Mem8,
		Mem16,
		Mem32,
		Mem64,
		Mem80,
		Mem128,
		Mem256,
		Mem512,
		Mem_Offs,

		Xmm0,
		XmmReg,
		XmmRM,
		XmmRM16,
		XmmRM32,
		XmmRM64,
		XmmRM128,

		YmmReg,
		YmmRM,
		YmmRM256,

		ZmmReg,
		ZmmRM512,

		YMem64,
		ZMem32,
		ZMem64,

		KReg,
		KRM16,

		BndReg,

		// ???
		XMem32,
		XMem64,
		YMem32 
	}
}
