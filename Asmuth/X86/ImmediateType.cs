using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	// InstructionEncoding depends on this being 5 bits
	public enum ImmediateType : byte
	{
		Size_Shift = 0,
		Size_0 = 0 << Size_Shift,
		Size_8 = 1 << Size_Shift,
		Size_16 = 2 << Size_Shift,
		Size_32 = 3 << Size_Shift,
		Size_64 = 4 << Size_Shift,
		Size_16Or32 = 5 << Size_Shift,
		Size_32Or64 = 6 << Size_Shift,
		Size_16Or32Or64 = 7 << Size_Shift,
		Size_Mask = 7,

		Type_Shift = Size_Shift + 3,
		Type_None = 0 << Type_Shift,
		Type_Operand = 1 << Type_Shift,
		Type_RelativeCodeOffset = 2 << Type_Shift,
		Type_OpcodeExtension = 3 << Type_Shift,
		Type_Mask = 3 << Type_Shift,

		None = 0,
		Imm8 = Type_Operand | Size_8,
		Imm16 = Type_Operand | Size_16,
		Imm32 = Type_Operand | Size_32,
		Imm64 = Type_Operand | Size_64,
		Imm16Or32 = Type_Operand | Size_16Or32, // Size depends on operand size
		Imm32Or64 = Type_Operand | Size_32Or64, // Size depends on operand size
		Imm16Or32Or64 = Type_Operand | Size_16Or32Or64,	// Size depends on operand size
		RelativeCodeOffset8 = Type_RelativeCodeOffset | Size_8,
		RelativeCodeOffset16 = Type_RelativeCodeOffset | Size_16,
		RelativeCodeOffset32 = Type_RelativeCodeOffset | Size_32,
		RelativeCodeOffset64 = Type_RelativeCodeOffset | Size_64,
		RelativeCodeOffset16Or32 = Type_RelativeCodeOffset | Size_16Or32, // Size depends on address size
		OpcodeExtension = Type_OpcodeExtension | Size_8,
	}
}
