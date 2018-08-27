﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Asmuth.X86.Asm
{
	public abstract class OperandSpec
	{
		private OperandSpec() { } // Disallow external inheritance

		// Used for NASM's "size match"
		public abstract IntegerSize? ImpliedIntegerOperandSize { get; }

		public abstract bool IsValidField(OperandField field);

		public abstract string Format(in Instruction instruction, OperandField? field);

		public abstract override string ToString();

		public interface IWithReg
		{
			RegisterClass RegisterClass { get; }
		}

		// PUSH CS
		public sealed class FixedReg : OperandSpec, IWithReg
		{
			public Register Register { get; }
			public RegisterClass RegisterClass => Register.Class;

			public FixedReg(Register register) => this.Register = register;
			public FixedReg(RegisterClass @class, byte index)
				: this(new Register(@class, index)) {}

			public override IntegerSize? ImpliedIntegerOperandSize => Register.IsSizedGpr
				? IntegerSizeEnum.TryFromBytes(Register.SizeInBytes.Value) : null;

			public override bool IsValidField(OperandField field) => false;

			public override string Format(in Instruction instruction, OperandField? field)
				=> Register.Name;

			public override string ToString() => Register.Name;

			public static readonly FixedReg AL = new FixedReg(Register.AL);
			public static readonly FixedReg CL = new FixedReg(Register.CL);
			public static readonly FixedReg AX = new FixedReg(Register.AX);
			public static readonly FixedReg CX = new FixedReg(Register.CX);
			public static readonly FixedReg DX = new FixedReg(Register.DX);
			public static readonly FixedReg Eax = new FixedReg(Register.Eax);
			public static readonly FixedReg Ecx = new FixedReg(Register.Ecx);
			public static readonly FixedReg Edx = new FixedReg(Register.Edx);
			public static readonly FixedReg Rax = new FixedReg(Register.Rax);
			public static readonly FixedReg Rcx = new FixedReg(Register.Rcx);

			public static readonly FixedReg ST0 = new FixedReg(Register.ST0);
			public static readonly FixedReg Xmm0 = new FixedReg(Register.Xmm0);

			public static readonly FixedReg ES = new FixedReg(Register.ES);
			public static readonly FixedReg CS = new FixedReg(Register.CS);
			public static readonly FixedReg SS = new FixedReg(Register.SS);
			public static readonly FixedReg DS = new FixedReg(Register.DS);
			public static readonly FixedReg FS = new FixedReg(Register.FS);
			public static readonly FixedReg GS = new FixedReg(Register.GS);
		}

		// PUSH r32
		public sealed class Reg : OperandSpec, IWithReg
		{
			public RegisterClass RegisterClass { get; }
			public RegisterFamily RegisterFamily => RegisterClass.Family;

			public Reg(RegisterClass @class)
			{
				this.RegisterClass = @class;
			}

			public RegOrMem OrMem(OperandDataType dataType) => new RegOrMem(this, new Mem(dataType));

			public override IntegerSize? ImpliedIntegerOperandSize => RegisterClass.IsSized
				? IntegerSizeEnum.TryFromBytes(RegisterClass.SizeInBytes.Value) : null;

			public override bool IsValidField(OperandField field)
				=> field == OperandField.ModReg
				|| field == OperandField.BaseReg
				|| field == OperandField.NonDestructiveReg;

			public override string Format(in Instruction instruction, OperandField? field)
			{
				byte regCode;
				if (field == OperandField.ModReg)
				{
					regCode = instruction.ModRM.HasValue
						? instruction.ModRM.Value.Reg
						: MainOpcodeByte.GetEmbeddedReg(instruction.MainOpcodeByte);
					if (instruction.NonLegacyPrefixes.ModRegExtension)
						regCode |= 0b1000;
				}
				else if (field == OperandField.BaseReg)
				{
					if (!instruction.ModRM.HasValue
						|| instruction.ModRM.Value.IsMemoryRM)
					{
						throw new InvalidOperationException("Instruction does not encode a mod base register.");
					}

					regCode = instruction.ModRM.Value.RM;
					if (instruction.NonLegacyPrefixes.BaseRegExtension)
						regCode |= 0b1000;
				}
				else if (field == OperandField.NonDestructiveReg)
				{
					if (!instruction.NonLegacyPrefixes.NonDestructiveReg.HasValue)
						throw new InvalidOperationException("Instruction does not encode a non-destructive register.");

					regCode = instruction.NonLegacyPrefixes.NonDestructiveReg.Value;
				}
				else
				{
					throw new ArgumentOutOfRangeException(nameof(field));
				}
				
				if (RegisterClass == RegisterClass.GprByte && regCode >= 4 && regCode < 8)
					throw new NotImplementedException("GPR high bytes.");
				var register = new Register(RegisterClass, regCode);
				return register.Name;
			}

			public override string ToString() => RegisterClass.Name;

			public static readonly Reg GprUnsized = new Reg(RegisterClass.GprUnsized);
			public static readonly Reg Gpr8 = new Reg(RegisterClass.GprByte);
			public static readonly Reg Gpr16 = new Reg(RegisterClass.GprWord);
			public static readonly Reg Gpr32 = new Reg(RegisterClass.GprDword);
			public static readonly Reg Gpr64 = new Reg(RegisterClass.GprQword);
			public static readonly Reg X87 = new Reg(RegisterClass.X87);
			public static readonly Reg Mmx = new Reg(RegisterClass.Mmx);
			public static readonly Reg Xmm = new Reg(RegisterClass.Xmm);
			public static readonly Reg Ymm = new Reg(RegisterClass.Ymm);
			public static readonly Reg Zmm = new Reg(RegisterClass.Zmm);
			public static readonly Reg AvxOpmask = new Reg(RegisterClass.AvxOpmask);
			public static readonly Reg Bound = new Reg(RegisterClass.Bound);
			public static readonly Reg Segment = new Reg(RegisterClass.Segment);
			public static readonly Reg Debug = new Reg(RegisterClass.DebugUnsized);
			public static readonly Reg Control = new Reg(RegisterClass.ControlUnsized);
		}

		// FDIV m32fp
		public sealed class Mem : OperandSpec
		{
			// Can be null for LEA, which doesn't actually access the address
			public OperandDataType? DataType { get; }
			public int SizeInBytes => DataType.HasValue ? DataType.Value.TotalSizeInBytes : 0;
			public int SizeInBits => SizeInBytes * 8;

			public Mem(OperandDataType? dataType) => this.DataType = dataType;

			public override IntegerSize? ImpliedIntegerOperandSize
				=> DataType.HasValue ? DataType.Value.GetImpliedGprSize() : null;

			public override bool IsValidField(OperandField field)
				=> field == OperandField.BaseReg || field == OperandField.Immediate;

			public override string Format(in Instruction instruction, OperandField? field)
			{
				if (field == OperandField.BaseReg)
				{
					return instruction.GetRMEffectiveAddress().ToString();
				}
				else
				{
					throw new NotImplementedException();
				}
			}

			public override string ToString() 
				=> SizeInBytes == 0 ? "m" : ("m" + SizeInBits.ToString());

			public static Mem Untyped(int sizeInBytes)
			{
				switch (sizeInBytes)
				{
					case 1: return M8;
					case 2: return M16;
					case 4: return M32;
					case 8: return M64;
					case 10: return M80;
					case 16: return M128;
					case 32: return M256;
					case 64: return M512;
					default: return new Mem(new OperandDataType(ScalarType.Untyped, sizeInBytes));
				}
			}

			public static readonly Mem M = new Mem(null);
			public static readonly Mem M8 = new Mem(OperandDataType.Byte);
			public static readonly Mem M16 = new Mem(OperandDataType.Word);
			public static readonly Mem M32 = new Mem(OperandDataType.Dword);
			public static readonly Mem M64 = new Mem(OperandDataType.Qword);
			public static readonly Mem M80 = new Mem(OperandDataType.Untyped80);
			public static readonly Mem M128 = new Mem(OperandDataType.Untyped128);
			public static readonly Mem M256 = new Mem(OperandDataType.Untyped256);
			public static readonly Mem M512 = new Mem(OperandDataType.Untyped512);

			public static readonly Mem I8 = new Mem(OperandDataType.I8);
			public static readonly Mem I16 = new Mem(OperandDataType.I16);
			public static readonly Mem I32 = new Mem(OperandDataType.I32);
			public static readonly Mem I64 = new Mem(OperandDataType.I64);

			public static readonly Mem F16 = new Mem(OperandDataType.F16);
			public static readonly Mem F32 = new Mem(OperandDataType.F32);
			public static readonly Mem F64 = new Mem(OperandDataType.F64);
			public static readonly Mem F80 = new Mem(OperandDataType.F80);
		}

		// NEG r/m8
		public sealed class RegOrMem : OperandSpec, IWithReg
		{
			public Reg RegSpec { get; }
			public Mem MemSpec { get; }

			public RegOrMem(Reg regSpec, Mem memSpec)
			{
				if (regSpec == null) throw new ArgumentNullException(nameof(regSpec));
				if (memSpec == null) throw new ArgumentNullException(nameof(memSpec));

				var regSize = regSpec.RegisterClass.SizeInBytes.Value;
				var memSize = memSpec.SizeInBytes;
				bool allowSubregister = regSpec.RegisterFamily == RegisterFamily.Sse
					|| regSpec.RegisterFamily == RegisterFamily.AvxOpmask;
				if (allowSubregister ? (regSize < memSize) : (regSize != memSize))
					throw new ArgumentException("RM mem size inconsistent with register class.");
				
				this.RegSpec = regSpec;
				this.MemSpec = memSpec;
			}

			public override IntegerSize? ImpliedIntegerOperandSize => RegSpec.ImpliedIntegerOperandSize;

			public override bool IsValidField(OperandField field)
				=> field == OperandField.BaseReg;

			public override string Format(in Instruction instruction, OperandField? field)
			{
				if (!instruction.ModRM.HasValue) throw new ArgumentOutOfRangeException(nameof(field));

				return instruction.ModRM.Value.IsDirect
					? RegSpec.Format(instruction, field)
					: MemSpec.Format(instruction, field);
			}

			public override string ToString()
			{
				return (RegSpec.RegisterClass.Family == RegisterFamily.Gpr ? "r" : RegSpec.ToString())
					+ "/" + MemSpec.ToString();
			}

			RegisterClass IWithReg.RegisterClass => RegSpec.RegisterClass;

			public static readonly RegOrMem RM8 = new RegOrMem(Reg.Gpr8, Mem.I8);
			public static readonly RegOrMem RM16 = new RegOrMem(Reg.Gpr16, Mem.I16);
			public static readonly RegOrMem RM32 = new RegOrMem(Reg.Gpr32, Mem.I32);
			public static readonly RegOrMem RM64 = new RegOrMem(Reg.Gpr64, Mem.I64);
			public static readonly RegOrMem Mmx = new RegOrMem(Reg.Mmx, Mem.F64);
			public static readonly RegOrMem Xmm = new RegOrMem(Reg.Xmm, Mem.M128);
			public static readonly RegOrMem Ymm = new RegOrMem(Reg.Ymm, Mem.M256);
			public static readonly RegOrMem Zmm = new RegOrMem(Reg.Zmm, Mem.M512);
		}

		// Memory operand with vector SIB
		// VGATHERDPD xmm1, vm32x, xmm2
		public sealed class VMem : OperandSpec
		{
			public SseVectorSize IndexRegSize { get; }
			public IntegerSize IndicesSize { get; }

			public VMem(SseVectorSize indexRegSize, IntegerSize indicesSize)
			{
				if (indicesSize != IntegerSize.Dword && indicesSize != IntegerSize.Qword)
					throw new ArgumentOutOfRangeException(nameof(indicesSize));

				this.IndexRegSize = indexRegSize;
				this.IndicesSize = indicesSize;
			}

			public RegisterClass IndexRegClass => IndexRegSize.GetRegisterClass();
			public int MaxIndexCount => IndexRegSize.InBytes() / IndicesSize.InBytes();

			public override IntegerSize? ImpliedIntegerOperandSize => null;

			public override bool IsValidField(OperandField field) => field == OperandField.BaseReg;

			public override string Format(in Instruction instruction, OperandField? field)
			{
				throw new NotImplementedException();
			}

			public override string ToString()
			{
				return "vm" + IndicesSize.InBits().ToString(CultureInfo.InvariantCulture)
					+ IndexRegClass.Name[0];
			}

			public static readonly VMem VM32X = new VMem(SseVectorSize._128, IntegerSize.Dword);
			public static readonly VMem VM32Y = new VMem(SseVectorSize._256, IntegerSize.Dword);
			public static readonly VMem VM32Z = new VMem(SseVectorSize._512, IntegerSize.Dword);
			public static readonly VMem VM64X = new VMem(SseVectorSize._128, IntegerSize.Qword);
			public static readonly VMem VM64Y = new VMem(SseVectorSize._256, IntegerSize.Qword);
			public static readonly VMem VM64Z = new VMem(SseVectorSize._512, IntegerSize.Qword);
		}

		// PUSH imm32 
		public sealed class Imm : OperandSpec
		{
			public OperandDataType DataType { get; }

			public Imm(OperandDataType dataType)
			{
				if (dataType.IsVector)
					throw new ArgumentException("Immediates cannot be vectored.", nameof(dataType));
				this.DataType = dataType;
			}

			public override IntegerSize? ImpliedIntegerOperandSize => DataType.GetImpliedGprSize();

			public override bool IsValidField(OperandField field)
				=> field == OperandField.Immediate || field == OperandField.SecondImmediate;

			public override string Format(in Instruction instruction, OperandField? field)
			{
				if (DataType.TotalSizeInBytes > instruction.ImmediateSizeInBytes)
					throw new InvalidOperationException("Instruction doesn't have an immediate big enough to encode operand.");
				if (field != OperandField.Immediate && field != OperandField.SecondImmediate)
					throw new ArgumentOutOfRangeException(nameof(field));

				if (DataType.TotalSizeInBytes != instruction.ImmediateSizeInBytes)
					throw new NotImplementedException("Multiple immediates.");

				if ((DataType.ScalarType != ScalarType.Untyped
					&& DataType.ScalarType != ScalarType.SignedInt
					&& DataType.ScalarType != ScalarType.UnsignedInt
					&& DataType.ScalarType != ScalarType.NearPointer)
					|| DataType.IsVector)
					throw new NotImplementedException("Formatting non-integral immediates.");

				var value = instruction.ImmediateData.RawStorage;
				return value < 0x10
					? value.ToString(CultureInfo.InvariantCulture)
					: "0x" + value.ToString("x", CultureInfo.InvariantCulture);
			}

			public override string ToString() => "imm" + DataType.ScalarSizeInBytes;

			public static readonly Imm I8 = new Imm(OperandDataType.I8);
			public static readonly Imm I16 = new Imm(OperandDataType.I16);
			public static readonly Imm I32 = new Imm(OperandDataType.I32);
			public static readonly Imm I64 = new Imm(OperandDataType.I64);

			public static readonly Imm MOffs8 = new Imm(OperandDataType.NearPtr8);
			public static readonly Imm MOffs16 = new Imm(OperandDataType.NearPtr16);
			public static readonly Imm MOffs32 = new Imm(OperandDataType.NearPtr32);
			public static readonly Imm MOffs64 = new Imm(OperandDataType.NearPtr64);
		}

		// SAL r/m8, 1 
		public sealed class Const : OperandSpec
		{
			public byte Value { get; }

			public Const(byte value) => this.Value = value;

			public override IntegerSize? ImpliedIntegerOperandSize => IntegerSize.Byte;

			public override bool IsValidField(OperandField field) => false;

			public override string Format(in Instruction instruction, OperandField? field)
			{
				return Value < 0x10
					? Value.ToString(CultureInfo.InvariantCulture)
					: "0x" + Value.ToString("x2", CultureInfo.InvariantCulture);
			}

			public override string ToString() => Value.ToString();

			public static readonly Const Zero = new Const(0);
			public static readonly Const One = new Const(1);
		}

		// JMP rel8
		public sealed class Rel : OperandSpec
		{
			public IntegerSize OffsetSize { get; }

			public Rel(IntegerSize offsetSize) => this.OffsetSize = offsetSize;

			public override IntegerSize? ImpliedIntegerOperandSize => null;

			public override bool IsValidField(OperandField field) => field == OperandField.Immediate;

			public override string Format(in Instruction instruction, OperandField? field)
			{
				if (OffsetSize.InBytes() != instruction.ImmediateSizeInBytes)
					throw new InvalidOperationException("Instruction immediate size doesn't match operand.");

				long value;
				switch (OffsetSize)
				{
					case IntegerSize.Byte: value = instruction.ImmediateData.AsSInt8(); break;
					case IntegerSize.Word: value = instruction.ImmediateData.AsInt16(); break;
					case IntegerSize.Dword: value = instruction.ImmediateData.AsInt32(); break;
					case IntegerSize.Qword: value = instruction.ImmediateData.AsInt64(); break;
					default: throw new UnreachableException();
				}

				var str = value.ToString(CultureInfo.InvariantCulture);
				if (value >= 0) str = "+" + str;
				return str;
			}

			public override string ToString() => "rel" + OffsetSize.InBits();

			public static readonly Rel Rel8 = new Rel(IntegerSize.Byte);
			public static readonly Rel Rel16 = new Rel(IntegerSize.Word);
			public static readonly Rel Rel32 = new Rel(IntegerSize.Dword);
		}
	}
}
