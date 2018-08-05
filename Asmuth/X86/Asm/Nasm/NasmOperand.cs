using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Asm.Nasm
{
	public readonly struct NasmOperand : IEquatable<NasmOperand>
	{
		public OperandField? Field { get; }
		public NasmOperandType Type { get; }

		public NasmOperand(OperandField? field, NasmOperandType type)
		{
			this.Field = field;
			this.Type = type;
		}

		public OperandFormat TryToOperandFormat(OperandSize? unsizedSize)
			=> TryToOperandFormat(Type, unsizedSize);

		public static OperandFormat[] ToOperandFormat(
			IReadOnlyList<NasmOperand> operands, IEnumerable<string> flags)
		{
			foreach (string flag in flags)
			{
				if (flag == NasmInstructionFlags.SizeMatch)
					return ToOperandFormat(operands, sizeMatch: true);
				else
				{
					var unsizedSize = NasmInstructionFlags.TryAsUnsizedOperandSize(flag);
					if (unsizedSize.HasValue) return ToOperandFormat(operands, unsizedSize.Value);
				}
			}

			return ToOperandFormat(operands, sizeMatch: false);
		}

		public static OperandFormat[] ToOperandFormat(IReadOnlyList<NasmOperand> operands, bool sizeMatch)
		{
			var operandFormats = new OperandFormat[operands.Count];
			
			OperandSize? impliedSize = null;
			for (int i = 0; i < operands.Count; ++i)
			{
				var operandFormat = operands[i].TryToOperandFormat(unsizedSize: null);
				if (operandFormat == null)
				{
					if (!sizeMatch) throw new FormatException();
					// If we're size matching, allow null as we'll do a second pass
				}
				else
				{
					operandFormats[i] = operandFormat;

					// Determine the size we'll match on the second pass
					if (sizeMatch)
					{
						var operandSize = operandFormat.ImpliedIntegerSize;
						if (operandSize.HasValue)
						{
							if (impliedSize.HasValue && operandSize != impliedSize)
								throw new FormatException(); // Can't size match
							impliedSize = operandSize;
						}
					}
				}
			}

			if (sizeMatch)
			{
				// Do a second pass with the implied size
				if (!impliedSize.HasValue) throw new FormatException();
				for (int i = 0; i < operands.Count; ++i)
				{
					var operandFormat = operands[i].TryToOperandFormat(impliedSize);
					operandFormats[i] = operandFormat ?? throw new FormatException();
				}
			}
			
			return operandFormats;
		}

		public static OperandFormat[] ToOperandFormat(
			IReadOnlyList<NasmOperand> operands, OperandSize unsizedSize)
		{
			var operandFormats = new OperandFormat[operands.Count];
			for (int i = 0; i < operands.Count; ++i)
			{
				var operandFormat = operands[i].TryToOperandFormat(unsizedSize);
				operandFormats[i] = operandFormat ?? throw new FormatException();
			}
			return operandFormats;
		}

		public static OperandFormat TryToOperandFormat(NasmOperandType type, OperandSize? unsizedSize)
		{
			switch (type)
			{
				case NasmOperandType.Reg_AL: return OperandFormat.FixedReg.AL;
				case NasmOperandType.Reg_AX: return OperandFormat.FixedReg.AX;
				case NasmOperandType.Reg_Eax: return OperandFormat.FixedReg.Eax;
				case NasmOperandType.Reg_Rax: return OperandFormat.FixedReg.Rax;
				case NasmOperandType.Reg8: return OperandFormat.Reg.Gpr8;
				case NasmOperandType.Reg16: return OperandFormat.Reg.Gpr16;
				case NasmOperandType.Reg32: return OperandFormat.Reg.Gpr32;
				case NasmOperandType.Reg64: return OperandFormat.Reg.Gpr64;

				case NasmOperandType.Fpu0: return OperandFormat.FixedReg.ST0;
				case NasmOperandType.FpuReg: return OperandFormat.Reg.X87;

				case NasmOperandType.RM8: return OperandFormat.RegOrMem.RM8;
				case NasmOperandType.RM16: return OperandFormat.RegOrMem.RM16;
				case NasmOperandType.RM32: return OperandFormat.RegOrMem.RM32;
				case NasmOperandType.RM64: return OperandFormat.RegOrMem.RM64;

				case NasmOperandType.Mem:
				{
					if (!unsizedSize.HasValue) return OperandFormat.Mem.M;
					else if (unsizedSize == OperandSize.Byte) return OperandFormat.Mem.I8;
					else if (unsizedSize == OperandSize.Word) return OperandFormat.Mem.I16;
					else if (unsizedSize == OperandSize.Dword) return OperandFormat.Mem.I32;
					else if (unsizedSize == OperandSize.Qword) return OperandFormat.Mem.I64;
					else throw new ArgumentOutOfRangeException(nameof(unsizedSize));
				}
				case NasmOperandType.Mem8: return OperandFormat.Mem.I8;
				case NasmOperandType.Mem16: return OperandFormat.Mem.I16;
				case NasmOperandType.Mem32: return OperandFormat.Mem.I32;
				case NasmOperandType.Mem64: return OperandFormat.Mem.I64;

				case NasmOperandType.Imm:
				{
					if (!unsizedSize.HasValue) return null;
					else if (unsizedSize == OperandSize.Byte) return OperandFormat.Imm.I8;
					else if (unsizedSize == OperandSize.Word) return OperandFormat.Imm.I16;
					else if (unsizedSize == OperandSize.Dword) return OperandFormat.Imm.I32;
					else if (unsizedSize == OperandSize.Qword) return OperandFormat.Imm.I64;
					else throw new ArgumentOutOfRangeException(nameof(unsizedSize));
				}
				case NasmOperandType.Imm8: return OperandFormat.Imm.I8;
				case NasmOperandType.Imm16: return OperandFormat.Imm.I16;
				case NasmOperandType.Imm32: return OperandFormat.Imm.I32;
				case NasmOperandType.Imm64: return OperandFormat.Imm.I64;

				case NasmOperandType.SByteWord:
				case NasmOperandType.SByteWord16:
				case NasmOperandType.SByteDword:
				case NasmOperandType.SByteDword32:
				case NasmOperandType.SByteDword64:
					return null; // These should be assembler only
			}
			throw new NotImplementedException();
		}

		public bool Equals(NasmOperand other) => Type == other.Type && Field == other.Field;
		public override bool Equals(object obj) => obj is NasmOperand && Equals((NasmOperand)obj);
		public override int GetHashCode() => (((byte?)Field).GetValueOrDefault(66) << 16) | (int)Type;

		public static bool Equals(NasmOperand first, NasmOperand second) => first.Equals(second);
		public static bool operator ==(NasmOperand lhs, NasmOperand rhs) => Equals(lhs, rhs);
		public static bool operator !=(NasmOperand lhs, NasmOperand rhs) => !Equals(lhs, rhs);
	}

	[Flags]
	public enum NasmOperandType : uint
	{
		OpType_Shift = 0,
		OpType_Bits = 2,
		OpType_Immediate = 0 << (int)OpType_Shift,
		OpType_Register = 1 << (int)OpType_Shift,
		OpType_RegisterOrMemory = 2 << (int)OpType_Shift,
		OpType_Memory = 3 << (int)OpType_Shift,
		OpType_Mask = ((1 << (int)OpType_Bits) - 1) << (int)OpType_Shift,

		RegClass_Shift = OpType_Shift + OpType_Bits,
		RegClass_Bits = 4,
		RegClass_None = 0 << (int)RegClass_Shift,
		RegClass_Control = 1 << (int)RegClass_Shift,
		RegClass_Debug = 2 << (int)RegClass_Shift,
		RegClass_T = 3 << (int)RegClass_Shift,
		RegClass_GeneralPurpose = 4 << (int)RegClass_Shift,
		RegClass_Segment = 5 << (int)RegClass_Shift,
		RegClass_Fpu = 6 << (int)RegClass_Shift,
		RegClass_Mmx = 7 << (int)RegClass_Shift,
		RegClass_Xmm = 8 << (int)RegClass_Shift,
		RegClass_Ymm = 9 << (int)RegClass_Shift,
		RegClass_Zmm = 10 << (int)RegClass_Shift,
		RegClass_Opmask = 11 << (int)RegClass_Shift,
		RegClass_Bound = 12 << (int)RegClass_Shift,
		RegClass_Mask = ((1 << (int)RegClass_Bits) - 1) << (int)RegClass_Shift,

		Size_Shift = RegClass_Shift + RegClass_Bits,
		Size_Bits = 4,
		Size_Undefined = 0 << (int)Size_Shift,
		Size_8 = 1 << (int)Size_Shift,
		Size_16 = 2 << (int)Size_Shift,
		Size_32 = 3 << (int)Size_Shift,
		Size_64 = 4 << (int)Size_Shift,
		Size_80 = 5 << (int)Size_Shift,
		Size_128 = 6 << (int)Size_Shift,
		Size_256 = 7 << (int)Size_Shift,
		Size_512 = 8 << (int)Size_Shift,
		Size_Mask = ((1 << (int)Size_Shift) - 1) << (int)Size_Shift,

		Value_Shift = Size_Shift + Size_Bits,
		Value_Bits = 3,
		Value_Variable = 0 << (int)Value_Shift,
		Value_0 = 1 << (int)Value_Shift,
		Value_1 = 2 << (int)Value_Shift,
		Value_2 = 3 << (int)Value_Shift,
		Value_3 = 4 << (int)Value_Shift,
		Value_4 = 5 << (int)Value_Shift,
		Value_5 = 6 << (int)Value_Shift,
		Value_Mask = ((1 << (int)Value_Bits) - 1) << (int)Value_Shift,

		FarFlag = Value_Mask + 1,
		NearFlag = FarFlag << 1,
		ShortFlag = NearFlag << 1,
		ToFlag = ShortFlag << 1,
		ColonFlag = ToFlag << 1,
		StrictFlag = ColonFlag << 1,

		Variant_Shift = Value_Shift + Value_Bits + 6,

		// Enumerants
		Unity = OpType_Immediate | Value_0,

		Imm = OpType_Immediate,
		Imm8 = OpType_Immediate | Size_8,
		Imm16 = OpType_Immediate | Size_16,
		Imm32 = OpType_Immediate | Size_32,
		Imm64 = OpType_Immediate | Size_64,

		SByteWord = OpType_Immediate | Size_16 | (1 << (int)Variant_Shift),
		SByteDword = OpType_Immediate | Size_32 | (1 << (int)Variant_Shift),
		SByteWord16 = OpType_Immediate | Size_16 | (2 << (int)Variant_Shift),
		SByteDword32 = OpType_Immediate | Size_32 | (2 << (int)Variant_Shift),
		SByteDword64 = OpType_Immediate | Size_32 | (3 << (int)Variant_Shift),
		UDword = OpType_Immediate | Size_32 | (4 << (int)Variant_Shift),
		SDword = OpType_Immediate | Size_32 | (5 << (int)Variant_Shift),

		Reg = OpType_Register | RegClass_GeneralPurpose | Size_Undefined,
		Reg8 = OpType_Register | RegClass_GeneralPurpose | Size_8,
		Reg16 = OpType_Register | RegClass_GeneralPurpose | Size_16,
		Reg32 = OpType_Register | RegClass_GeneralPurpose | Size_32,
		Reg32NA = OpType_Register | RegClass_GeneralPurpose | Size_32 | (1 << (int)Variant_Shift),
		Reg64 = OpType_Register | RegClass_GeneralPurpose | Size_64,
		Reg_AL = OpType_Register | RegClass_GeneralPurpose | Size_8 | Value_0,
		Reg_AX = OpType_Register | RegClass_GeneralPurpose | Size_16 | Value_0,
		Reg_Eax = OpType_Register | RegClass_GeneralPurpose | Size_32 | Value_0,
		Reg_Rax = OpType_Register | RegClass_GeneralPurpose | Size_64 | Value_0,
		Reg_CL = OpType_Register | RegClass_GeneralPurpose | Size_8 | Value_1,
		Reg_CX = OpType_Register | RegClass_GeneralPurpose | Size_16 | Value_1,
		Reg_Ecx = OpType_Register | RegClass_GeneralPurpose | Size_32 | Value_1,
		Reg_Rcx = OpType_Register | RegClass_GeneralPurpose | Size_64 | Value_1,
		Reg_DX = OpType_Register | RegClass_GeneralPurpose | Size_16 | Value_2,
		Reg_Edx = OpType_Register | RegClass_GeneralPurpose | Size_32 | Value_2,

		RM8 = OpType_RegisterOrMemory | RegClass_GeneralPurpose | Size_8,
		RM16 = OpType_RegisterOrMemory | RegClass_GeneralPurpose | Size_16,
		RM32 = OpType_RegisterOrMemory | RegClass_GeneralPurpose | Size_32,
		RM64 = OpType_RegisterOrMemory | RegClass_GeneralPurpose | Size_64,

		Reg_SReg = OpType_Register | RegClass_Segment | Size_16,
		Reg_ES = OpType_Register | RegClass_Segment | Size_16 | Value_0,
		Reg_CS = OpType_Register | RegClass_Segment | Size_16 | Value_1,
		Reg_SS = OpType_Register | RegClass_Segment | Size_16 | Value_2,
		Reg_DS = OpType_Register | RegClass_Segment | Size_16 | Value_3,
		Reg_FS = OpType_Register | RegClass_Segment | Size_16 | Value_4,
		Reg_GS = OpType_Register | RegClass_Segment | Size_16 | Value_5,

		Reg_CReg = OpType_Register | RegClass_Control | Size_32,
		Reg_DReg = OpType_Register | RegClass_Debug | Size_32,
		Reg_TReg = OpType_Register | RegClass_T | Size_32,

		FpuReg = OpType_Register | RegClass_Fpu,
		Fpu0 = OpType_Register | RegClass_Fpu | Value_0,
		MmxReg = OpType_Register | RegClass_Mmx,
		MmxRM = OpType_RegisterOrMemory | RegClass_Mmx,
		MmxRM64 = OpType_RegisterOrMemory | RegClass_Mmx | Size_64,

		Mem = OpType_Memory,
		Mem8 = OpType_Memory | Size_8,
		Mem16 = OpType_Memory | Size_16,
		Mem32 = OpType_Memory | Size_32,
		Mem64 = OpType_Memory | Size_64,
		Mem80 = OpType_Memory | Size_80,
		Mem128 = OpType_Memory | Size_128,
		Mem256 = OpType_Memory | Size_256,
		Mem512 = OpType_Memory | Size_512,
		Mem_Offs = OpType_Memory | (1 << (int)Variant_Shift),

		XMem32 = OpType_Memory | Size_32 | (1 << (int)Variant_Shift),
		XMem64 = OpType_Memory | Size_64 | (1 << (int)Variant_Shift),
		YMem32 = OpType_Memory | Size_32 | (2 << (int)Variant_Shift),
		YMem64 = OpType_Memory | Size_64 | (2 << (int)Variant_Shift),
		ZMem32 = OpType_Memory | Size_32 | (3 << (int)Variant_Shift),
		ZMem64 = OpType_Memory | Size_64 | (3 << (int)Variant_Shift),

		Xmm0 = OpType_Register | RegClass_Xmm | Value_0,
		XmmReg = OpType_Register | RegClass_Xmm,
		XmmRM = OpType_RegisterOrMemory | RegClass_Xmm,
		XmmRM8 = OpType_RegisterOrMemory | RegClass_Xmm | Size_8,
		XmmRM16 = OpType_RegisterOrMemory | RegClass_Xmm | Size_16,
		XmmRM32 = OpType_RegisterOrMemory | RegClass_Xmm | Size_32,
		XmmRM64 = OpType_RegisterOrMemory | RegClass_Xmm | Size_64,
		XmmRM128 = OpType_RegisterOrMemory | RegClass_Xmm | Size_128,

		YmmReg = OpType_Register | RegClass_Ymm,
		YmmRM = OpType_RegisterOrMemory | RegClass_Ymm,
		YmmRM256 = OpType_RegisterOrMemory | RegClass_Ymm | Size_256,

		ZmmReg = OpType_Register | RegClass_Zmm,
		ZmmRM512 = OpType_RegisterOrMemory | RegClass_Zmm | Size_512,
		
		KReg = OpType_Register | RegClass_Opmask,
		KRM8 = OpType_RegisterOrMemory | RegClass_Opmask | Size_8,
		KRM16 = OpType_RegisterOrMemory | RegClass_Opmask | Size_16,
		KRM32 = OpType_RegisterOrMemory | RegClass_Opmask | Size_32,
		KRM64 = OpType_RegisterOrMemory | RegClass_Opmask | Size_64,

		BndReg = OpType_Register | RegClass_Bound,
	}
}
