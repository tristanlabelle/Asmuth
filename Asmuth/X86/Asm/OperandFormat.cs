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
			RegisterNamespace RegisterNamespace { get; }
		}

		// PUSH CS
		public sealed class FixedReg : OperandFormat, IWithReg
		{
			public NamedRegister Register { get; }
			public RegisterNamespace RegisterNamespace => Register.Namespace;

			public FixedReg(NamedRegister register) => this.Register = register;
			public FixedReg(RegisterNamespace @namespace, byte index)
				: this(new NamedRegister(@namespace, index)) {}

			public override OperandSize? ImpliedIntegerSize => Register.Namespace.TryGetIntegerSize();

			public override string ToString() => Register.ToString();

			public static readonly FixedReg AL = new FixedReg(NamedRegister.AL);
			public static readonly FixedReg CL = new FixedReg(NamedRegister.CL);
			public static readonly FixedReg AX = new FixedReg(NamedRegister.AX);
			public static readonly FixedReg CX = new FixedReg(NamedRegister.CX);
			public static readonly FixedReg DX = new FixedReg(NamedRegister.DX);
			public static readonly FixedReg Eax = new FixedReg(NamedRegister.Eax);
			public static readonly FixedReg Ecx = new FixedReg(NamedRegister.Ecx);
			public static readonly FixedReg Rax = new FixedReg(NamedRegister.Rax);
			public static readonly FixedReg ST0 = new FixedReg(NamedRegister.ST0);
		}

		// PUSH r32
		public sealed class Reg : OperandFormat, IWithReg
		{
			public RegisterNamespace Namespace { get; }

			public Reg(RegisterNamespace @namespace)
			{
				this.Namespace = @namespace;
			}

			public override OperandSize? ImpliedIntegerSize => Namespace.TryGetIntegerSize();

			public override string ToString()
			{
				switch (Namespace)
				{
					case RegisterNamespace.Gpr8: return "r8";
					case RegisterNamespace.Gpr16: return "r16";
					case RegisterNamespace.Gpr32: return "r32";
					case RegisterNamespace.Gpr64: return "r64";
					case RegisterNamespace.X87: return "st";
					case RegisterNamespace.Mmx: return "mm";
					case RegisterNamespace.Xmm: return "xmm";
					case RegisterNamespace.Ymm: return "ymm";
					case RegisterNamespace.Zmm: return "zmm";
					default: throw new NotImplementedException();
				}
			}
			
			RegisterNamespace IWithReg.RegisterNamespace => Namespace;

			public static readonly Reg Gpr8 = new Reg(RegisterNamespace.Gpr8);
			public static readonly Reg Gpr16 = new Reg(RegisterNamespace.Gpr16);
			public static readonly Reg Gpr32 = new Reg(RegisterNamespace.Gpr32);
			public static readonly Reg Gpr64 = new Reg(RegisterNamespace.Gpr64);
			public static readonly Reg X87 = new Reg(RegisterNamespace.X87);
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
				return (RegSpec.Namespace.IsGpr() ? "r" : RegSpec.ToString())
					+ "/" + MemSpec.ToString();
			}

			RegisterNamespace IWithReg.RegisterNamespace => RegSpec.Namespace;

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
