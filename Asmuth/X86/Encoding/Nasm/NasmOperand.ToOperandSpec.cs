using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Encoding.Nasm
{
	partial struct NasmOperand
	{
		public OperandSpec TryToOperandSpec(AddressSize? addressSize, int? defaultSizeInBytes)
			=> TryToOperandSpec(Type, Flags, addressSize, defaultSizeInBytes);

		public static OperandSpec TryGetImmediateOperandSpec(
			NasmEncodingTokenType type, int? defaultSizeInBytes)
		{
			switch (type)
			{
				case NasmEncodingTokenType.Immediate_Byte: return OperandSpec.Imm.Byte;
				case NasmEncodingTokenType.Immediate_Byte_Signed: return OperandSpec.Imm.I8;
				case NasmEncodingTokenType.Immediate_Byte_Unsigned: return OperandSpec.Imm.U8;
				case NasmEncodingTokenType.Immediate_Word: return OperandSpec.Imm.Word;
				case NasmEncodingTokenType.Immediate_Dword: return OperandSpec.Imm.Dword;
				case NasmEncodingTokenType.Immediate_Dword_Signed: return OperandSpec.Imm.I32;
				case NasmEncodingTokenType.Immediate_Qword: return OperandSpec.Imm.Qword;

				case NasmEncodingTokenType.Immediate_RelativeOffset8: return OperandSpec.Rel.Short;
				case NasmEncodingTokenType.Immediate_RelativeOffset:
					if (defaultSizeInBytes == 2) return OperandSpec.Rel.Long16;
					if (defaultSizeInBytes == 4) return OperandSpec.Rel.Long32;
					return null;

				case NasmEncodingTokenType.Immediate_Is4: return null;

				default: return null;
			}
		}

		public static OperandSpec TryToOperandSpec(
			NasmOperandType type, NasmOperandFlags flags,
			AddressSize? addressSize, int? defaultSizeInBytes)
		{
			switch (type)
			{
				case NasmOperandType.Unity: return OperandSpec.Const.One;

				// Used with UD /r, where the ModRM value is irrelevant
				case NasmOperandType.Reg: return OperandSpec.Reg.GprUnsized;

				case NasmOperandType.Reg_AL: return OperandSpec.FixedReg.AL_Untyped;
				case NasmOperandType.Reg_CL: return OperandSpec.FixedReg.CL_Untyped;
				case NasmOperandType.Reg_AX: return OperandSpec.FixedReg.AX_Untyped;
				case NasmOperandType.Reg_CX: return OperandSpec.FixedReg.CX_Untyped;
				case NasmOperandType.Reg_DX: return OperandSpec.FixedReg.DX_Untyped;
				case NasmOperandType.Reg_Eax: return OperandSpec.FixedReg.Eax_Untyped;
				case NasmOperandType.Reg_Ecx: return OperandSpec.FixedReg.Ecx_Untyped;
				case NasmOperandType.Reg_Edx: return OperandSpec.FixedReg.Edx_Untyped;
				case NasmOperandType.Reg_Rax: return OperandSpec.FixedReg.Rax_Untyped;
				case NasmOperandType.Reg_Rcx: return OperandSpec.FixedReg.Rcx_Untyped;
				case NasmOperandType.Reg8: return OperandSpec.Reg.Gpr8;
				case NasmOperandType.Reg16: return OperandSpec.Reg.Gpr16;
				case NasmOperandType.Reg32: return OperandSpec.Reg.Gpr32;
				case NasmOperandType.Reg64: return OperandSpec.Reg.Gpr64;

				// "XCHG eax,eax" is not actually encodable as it aliases to NOP,
				// which has different semantics in 64-bit mode (does not zero the top dword).
				// Let's ignore this for simplicity.
				case NasmOperandType.Reg32NA: return OperandSpec.Reg.Gpr32;

				case NasmOperandType.RM8: return GetGprOrMem(IntegerSize.Byte, flags);
				case NasmOperandType.RM16: return GetGprOrMem(IntegerSize.Word, flags);
				case NasmOperandType.RM32: return GetGprOrMem(IntegerSize.Dword, flags);
				case NasmOperandType.RM64: return GetGprOrMem(IntegerSize.Qword, flags);

				case NasmOperandType.Fpu0: return OperandSpec.FixedReg.ST0_F80;
				case NasmOperandType.FpuReg: return OperandSpec.Reg.X87;

				case NasmOperandType.Reg_SReg: return OperandSpec.Reg.Segment;
				case NasmOperandType.Reg_ES: return OperandSpec.FixedReg.ES;
				case NasmOperandType.Reg_CS: return OperandSpec.FixedReg.CS;
				case NasmOperandType.Reg_SS: return OperandSpec.FixedReg.SS;
				case NasmOperandType.Reg_DS: return OperandSpec.FixedReg.DS;
				case NasmOperandType.Reg_FS: return OperandSpec.FixedReg.FS;
				case NasmOperandType.Reg_GS: return OperandSpec.FixedReg.GS;

				case NasmOperandType.Reg_CReg: return OperandSpec.Reg.Control;
				case NasmOperandType.Reg_DReg: return OperandSpec.Reg.Debug;

				case NasmOperandType.MmxReg: return OperandSpec.Reg.Mmx;
				case NasmOperandType.MmxRM: return MakeRM(OperandSpec.Reg.Mmx, defaultSizeInBytes);
				case NasmOperandType.MmxRM64: return OperandSpec.RegOrMem.Mmx_Untyped;

				case NasmOperandType.Xmm0: return OperandSpec.FixedReg.Xmm0_Untyped;
				case NasmOperandType.XmmReg: return OperandSpec.Reg.Xmm;
				case NasmOperandType.XmmRM: return MakeRM(OperandSpec.Reg.Xmm, defaultSizeInBytes);
				case NasmOperandType.XmmRM8: return OperandSpec.Reg.Xmm.OrMem(DataType.Byte);
				case NasmOperandType.XmmRM16: return OperandSpec.Reg.Xmm.OrMem(DataType.Word);
				case NasmOperandType.XmmRM32: return OperandSpec.Reg.Xmm.OrMem(DataType.Dword);
				case NasmOperandType.XmmRM64: return OperandSpec.Reg.Xmm.OrMem(DataType.Qword);
				case NasmOperandType.XmmRM128: return OperandSpec.RegOrMem.Mmx_Untyped;

				case NasmOperandType.YmmReg: return OperandSpec.Reg.Ymm;
				case NasmOperandType.YmmRM: return MakeRM(OperandSpec.Reg.Ymm, defaultSizeInBytes);
				case NasmOperandType.YmmRM256: return OperandSpec.RegOrMem.Mmx_Untyped;

				case NasmOperandType.ZmmReg: return OperandSpec.Reg.Zmm;
				case NasmOperandType.ZmmRM512: return OperandSpec.RegOrMem.Zmm_Untyped;

				case NasmOperandType.KReg: return OperandSpec.Reg.AvxOpmask;
				case NasmOperandType.KRM8: return OperandSpec.Reg.AvxOpmask.OrMem(DataType.Byte);
				case NasmOperandType.KRM16: return OperandSpec.Reg.AvxOpmask.OrMem(DataType.Word);
				case NasmOperandType.KRM32: return OperandSpec.Reg.AvxOpmask.OrMem(DataType.Dword);
				case NasmOperandType.KRM64: return OperandSpec.Reg.AvxOpmask.OrMem(DataType.Qword);

				case NasmOperandType.BndReg: return OperandSpec.Reg.Bound;

				case NasmOperandType.Mem:
					return defaultSizeInBytes.HasValue
						? OperandSpec.Mem.WithDataType(GetDataType(defaultSizeInBytes.Value, flags))
						: OperandSpec.Mem.M;
				case NasmOperandType.Mem8: return OperandSpec.Mem.WithDataType(GetDataType(1, flags));
				case NasmOperandType.Mem16: return OperandSpec.Mem.WithDataType(GetDataType(2, flags));
				case NasmOperandType.Mem32: return OperandSpec.Mem.WithDataType(GetDataType(4, flags));
				case NasmOperandType.Mem64: return OperandSpec.Mem.WithDataType(GetDataType(8, flags));
				case NasmOperandType.Mem80: return OperandSpec.Mem.WithDataType(GetDataType(10, flags));
				case NasmOperandType.Mem128: return OperandSpec.Mem.WithDataType(GetDataType(16, flags));
				case NasmOperandType.Mem256: return OperandSpec.Mem.WithDataType(GetDataType(32, flags));
				case NasmOperandType.Mem512: return OperandSpec.Mem.WithDataType(GetDataType(64, flags));

				case NasmOperandType.XMem32: return OperandSpec.VMem.VM32X;
				case NasmOperandType.XMem64: return OperandSpec.VMem.VM64X;
				case NasmOperandType.YMem32: return OperandSpec.VMem.VM32Y;
				case NasmOperandType.YMem64: return OperandSpec.VMem.VM64Y;
				case NasmOperandType.ZMem32: return OperandSpec.VMem.VM32Z;
				case NasmOperandType.ZMem64: return OperandSpec.VMem.VM64Z;

				case NasmOperandType.Imm:
					if (!defaultSizeInBytes.HasValue) return null;
					return OperandSpec.Imm.WithDataType(GetDataType(defaultSizeInBytes.Value, flags));
				case NasmOperandType.Imm8: return OperandSpec.Imm.WithDataType(GetDataType(1, flags));
				case NasmOperandType.Imm16: return OperandSpec.Imm.WithDataType(GetDataType(2, flags));
				case NasmOperandType.Imm32: return OperandSpec.Imm.WithDataType(GetDataType(4, flags));
				case NasmOperandType.Imm64: return OperandSpec.Imm.WithDataType(GetDataType(8, flags));

				// These are assembler only, probably to allow specifying the immediate
				// in a different format, but they all map to an "ib,s" immediate.
				case NasmOperandType.SByteWord:
				case NasmOperandType.SByteWord16:
				case NasmOperandType.SByteDword:
				case NasmOperandType.SByteDword32:
				case NasmOperandType.SByteDword64:
					return OperandSpec.Imm.I8;

				case NasmOperandType.UDword: return OperandSpec.Imm.U32;
				case NasmOperandType.SDword: return OperandSpec.Imm.I32;

				case NasmOperandType.Mem_Offs:
					if (!addressSize.HasValue || !defaultSizeInBytes.HasValue) return null;
					return new OperandSpec.MOffs(addressSize.Value,
						new DataType(ScalarType.Untyped, defaultSizeInBytes.Value));
			}

			throw new NotImplementedException($"Unimplemented NasmOperandType > OperandSpec conversion case: {type}.");
		}

		private static DataType GetDataType(int sizeInBytes, NasmOperandFlags flags)
		{
			if ((flags & NasmOperandFlags.NearPointer) != 0)
				return new DataType(ScalarType.NearPointer, sizeInBytes);
			if ((flags & NasmOperandFlags.FarPointer) != 0)
				return new DataType(ScalarType.FarPointer, sizeInBytes + 2);
			return new DataType(ScalarType.Untyped, sizeInBytes);
		}

		private static OperandSpec.RegOrMem GetGprOrMem(IntegerSize size, NasmOperandFlags flags)
		{
			if ((flags & NasmOperandFlags.FarPointer) != 0)
				throw new FormatException("GPRs cannot store far pointers.");

			if ((flags & NasmOperandFlags.NearPointer) != 0)
			{
				if (size == IntegerSize.Word) return OperandSpec.RegOrMem.RM16_NearPtr;
				if (size == IntegerSize.Dword) return OperandSpec.RegOrMem.RM32_NearPtr;
				if (size == IntegerSize.Qword) return OperandSpec.RegOrMem.RM64_NearPtr;
				throw new FormatException($"Unexpected near pointer size: {size.InBytes()} bytes.");
			}

			if (size == IntegerSize.Byte) return OperandSpec.RegOrMem.RM8_Untyped;
			if (size == IntegerSize.Word) return OperandSpec.RegOrMem.RM16_Untyped;
			if (size == IntegerSize.Dword) return OperandSpec.RegOrMem.RM32_Untyped;
			if (size == IntegerSize.Qword) return OperandSpec.RegOrMem.RM64_Untyped;
			throw new UnreachableException();
		}

		private static OperandSpec MakeRM(OperandSpec.Reg reg, int? defaultSizeInBytes)
		{
			if (!defaultSizeInBytes.HasValue)
			{
				if (reg.RegisterFamily != RegisterFamily.SseAvx)
					return null;

				// Assume xmmrm = xmmrm128, that's our best guess
				defaultSizeInBytes = reg.RegisterClass.SizeInBytes;
			}

			return reg.OrMem(new DataType(ScalarType.Untyped, defaultSizeInBytes.Value));
		}
	}
}
