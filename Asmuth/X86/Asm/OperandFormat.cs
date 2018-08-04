using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Text;

namespace Asmuth.X86.Asm
{
	public abstract class OperandFormat
	{
		private OperandFormat() { } // Disallow external inheritance

		public abstract override string ToString();

		// PUSH CS
		public sealed class FixedReg : OperandFormat
		{
			public NamedRegister Register { get; }

			public FixedReg(NamedRegister register) => this.Register = register;
			public FixedReg(RegisterNamespace @namespace, byte index)
				: this(new NamedRegister(@namespace, index)) {}

			public override string ToString() => Register.ToString();

			public static readonly FixedReg AL = new FixedReg(NamedRegister.AL);
			public static readonly FixedReg AX = new FixedReg(NamedRegister.AX);
			public static readonly FixedReg Eax = new FixedReg(NamedRegister.Eax);
			public static readonly FixedReg Rax = new FixedReg(NamedRegister.Rax);
			public static readonly FixedReg ST0 = new FixedReg(NamedRegister.ST0);
		}

		// PUSH r32
		public sealed class Reg : OperandFormat
		{
			public RegisterNamespace Namespace { get; }

			public Reg(RegisterNamespace @namespace)
			{
				this.Namespace = @namespace;
			}

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

			public static readonly Reg Gpr8 = new Reg(RegisterNamespace.Gpr8);
			public static readonly Reg Gpr16 = new Reg(RegisterNamespace.Gpr16);
			public static readonly Reg Gpr32 = new Reg(RegisterNamespace.Gpr32);
			public static readonly Reg Gpr64 = new Reg(RegisterNamespace.Gpr64);
			public static readonly Reg X87 = new Reg(RegisterNamespace.X87);
		}

		// FDIV m32fp
		public sealed class Mem : OperandFormat
		{
			// TODO: Data type
			private readonly byte sizeInBytes;
			public int SizeInBytes => sizeInBytes;
			public int SizeInBits => (int)sizeInBytes * 8;

			public Mem(int sizeInBytes) => this.sizeInBytes = checked((byte)sizeInBytes);

			public override string ToString() => "m" + SizeInBits.ToString();

			public static readonly Mem M = new Mem(0);
			public static readonly Mem M8 = new Mem(1);
			public static readonly Mem M16 = new Mem(2);
			public static readonly Mem M32 = new Mem(4);
			public static readonly Mem M64 = new Mem(8);
			public static readonly Mem M128 = new Mem(16);
		}

		// NEG r/m8
		public sealed class RegOrMem : OperandFormat
		{
			public Reg RegSpec { get; }
			public Mem MemSpec { get; }

			public RegOrMem(Reg regSpec, Mem memSpec)
			{
				Contract.Requires(regSpec != null);
				Contract.Requires(memSpec != null);

				// TODO: Check matching sizes
				this.RegSpec = regSpec;
				this.MemSpec = memSpec;
			}

			public override string ToString()
			{
				return (RegSpec.Namespace.IsGpr() ? "r" : RegSpec.ToString())
					+ "/" + MemSpec.ToString();
			}

			public static readonly RegOrMem RM8 = new RegOrMem(Reg.Gpr8, Mem.M8);
			public static readonly RegOrMem RM16 = new RegOrMem(Reg.Gpr16, Mem.M16);
			public static readonly RegOrMem RM32 = new RegOrMem(Reg.Gpr32, Mem.M32);
			public static readonly RegOrMem RM64 = new RegOrMem(Reg.Gpr64, Mem.M64);
		}

		// PUSH imm32 
		public sealed class Imm : OperandFormat
		{
			// TODO: Data type
			public override string ToString() => "imm";
		}

		// SAL r/m8, 1 
		public sealed class Const : OperandFormat
		{
			public sbyte Value { get; }

			public Const(sbyte value) => this.Value = value;

			public override string ToString() => Value.ToString();

			public static readonly Const Zero = new Const(0);
			public static readonly Const One = new Const(1);
		}

		// JMP rel8
		public sealed class Rel : OperandFormat
		{
			public OperandSize OffsetSize { get; }

			public Rel(OperandSize offsetSize) => this.OffsetSize = offsetSize;

			public override string ToString() => "rel" + OffsetSize.InBits();

			public static readonly Rel Rel8 = new Rel(OperandSize.Byte);
			public static readonly Rel Rel16 = new Rel(OperandSize.Word);
			public static readonly Rel Rel32 = new Rel(OperandSize.Qword);
		}
	}
}
