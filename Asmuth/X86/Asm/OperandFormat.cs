using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Asm
{
	public abstract class OperandFormat
	{
		private OperandFormat() { } // Disallow external inheritance

		// Used for NASM's "size match"
		public abstract OperandSize? ImpliedIntegerSize { get; }

		public abstract override string ToString();

		public interface IWithReg
		{
			RegisterClass RegisterClass { get; }
		}

		// PUSH CS
		public sealed class FixedReg : OperandFormat, IWithReg
		{
			public Register Register { get; }
			public RegisterClass RegisterClass => Register.Class;

			public FixedReg(Register register) => this.Register = register;
			public FixedReg(RegisterClass @class, byte index)
				: this(new Register(@class, index)) {}

			public override OperandSize? ImpliedIntegerSize => throw new NotImplementedException();

			public override string ToString() => Register.Name;

			public static readonly FixedReg AL = new FixedReg(Register.AL);
			public static readonly FixedReg CL = new FixedReg(Register.CL);
			public static readonly FixedReg AX = new FixedReg(Register.AX);
			public static readonly FixedReg CX = new FixedReg(Register.CX);
			public static readonly FixedReg DX = new FixedReg(Register.DX);
			public static readonly FixedReg Eax = new FixedReg(Register.Eax);
			public static readonly FixedReg Ecx = new FixedReg(Register.Ecx);
			public static readonly FixedReg Rax = new FixedReg(Register.Rax);
			public static readonly FixedReg ST0 = new FixedReg(Register.ST0);
		}

		// PUSH r32
		public sealed class Reg : OperandFormat, IWithReg
		{
			public RegisterClass RegisterClass { get; }
			public RegisterFamily RegisterFamily => RegisterClass.Family;

			public Reg(RegisterClass @class)
			{
				this.RegisterClass = @class;
			}

			public override OperandSize? ImpliedIntegerSize => throw new NotImplementedException();

			public override string ToString() => throw new NotImplementedException();

			public static readonly Reg GprUnsized = new Reg(RegisterClass.GprUnsized);
			public static readonly Reg Gpr8 = new Reg(RegisterClass.GprByte);
			public static readonly Reg Gpr16 = new Reg(RegisterClass.GprWord);
			public static readonly Reg Gpr32 = new Reg(RegisterClass.GprDword);
			public static readonly Reg Gpr64 = new Reg(RegisterClass.GprQword);
			public static readonly Reg X87 = new Reg(RegisterClass.X87);
		}

		// FDIV m32fp
		public sealed class Mem : OperandFormat
		{
			public OperandDataType DataType { get; }
			public int SizeInBytes => DataType.GetElementSizeInBytes();
			public int SizeInBits => DataType.GetElementSizeInBits();

			public Mem(OperandDataType dataType) => this.DataType = dataType;

			public override OperandSize? ImpliedIntegerSize => DataType.TryGetIntegerSize();

			public override string ToString() 
				=> SizeInBytes == 0 ? "m" : ("m" + SizeInBits.ToString());

			public static readonly Mem M = new Mem(OperandDataType.Unknown);
			public static readonly Mem I8 = new Mem(OperandDataType.I8);
			public static readonly Mem I16 = new Mem(OperandDataType.I16);
			public static readonly Mem I32 = new Mem(OperandDataType.I32);
			public static readonly Mem I64 = new Mem(OperandDataType.I64);
			public static readonly Mem M80 = new Mem(OperandDataType.ElementSize_80Bits);
			public static readonly Mem M128 = new Mem(OperandDataType.ElementSize_128Bits);
			public static readonly Mem M256 = new Mem(OperandDataType.ElementSize_256Bits);
			public static readonly Mem M512 = new Mem(OperandDataType.ElementSize_512Bits);
		}

		// NEG r/m8
		public sealed class RegOrMem : OperandFormat, IWithReg
		{
			public Reg RegSpec { get; }
			public Mem MemSpec { get; }

			public RegOrMem(Reg regSpec, Mem memSpec)
			{
				// TODO: Check matching sizes
				this.RegSpec = regSpec ?? throw new ArgumentNullException(nameof(regSpec));
				this.MemSpec = memSpec ?? throw new ArgumentNullException(nameof(memSpec));
			}

			public override OperandSize? ImpliedIntegerSize => MemSpec.ImpliedIntegerSize;

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
		}

		// PUSH imm32 
		public sealed class Imm : OperandFormat
		{
			public OperandDataType DataType { get; }

			public Imm(OperandDataType dataType)
			{
				if (dataType.GetElementSizeInBytes() == 0)
					throw new ArgumentException("Immediates cannot be zero-sized", nameof(dataType));
				this.DataType = dataType;
			}

			public override OperandSize? ImpliedIntegerSize => DataType.TryGetIntegerSize();

			public override string ToString() => "imm" + DataType.GetElementSizeInBits();

			public static readonly Imm I8 = new Imm(OperandDataType.I8);
			public static readonly Imm I16 = new Imm(OperandDataType.I16);
			public static readonly Imm I32 = new Imm(OperandDataType.I32);
			public static readonly Imm I64 = new Imm(OperandDataType.I64);
		}

		// SAL r/m8, 1 
		public sealed class Const : OperandFormat
		{
			public sbyte Value { get; }

			public Const(sbyte value) => this.Value = value;

			public override OperandSize? ImpliedIntegerSize => OperandSize.Byte;

			public override string ToString() => Value.ToString();

			public static readonly Const Zero = new Const(0);
			public static readonly Const One = new Const(1);
		}

		// JMP rel8
		public sealed class Rel : OperandFormat
		{
			public OperandSize OffsetSize { get; }

			public Rel(OperandSize offsetSize) => this.OffsetSize = offsetSize;

			public override OperandSize? ImpliedIntegerSize => OffsetSize;

			public override string ToString() => "rel" + OffsetSize.InBits();

			public static readonly Rel Rel8 = new Rel(OperandSize.Byte);
			public static readonly Rel Rel16 = new Rel(OperandSize.Word);
			public static readonly Rel Rel32 = new Rel(OperandSize.Qword);
		}
	}
}
