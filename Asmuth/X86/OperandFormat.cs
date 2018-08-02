using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Text;

namespace Asmuth.X86
{
	public abstract class OperandFormat
	{
		internal OperandFormat() { } // Disallow external inheritance

		public abstract override string ToString();
	}

	public sealed class FixedRegOperand : OperandFormat
	{
		public NamedRegister Register { get; }

		public FixedRegOperand(NamedRegister register) => this.Register = register;

		public override string ToString() => Register.ToString();
	}

	public sealed class RegOperand : OperandFormat
	{
		// Gpr8_High: AL, CL, DL, BL, AH, CH, DH, BH (Legacy with no REX prefix)
		public RegisterNamespace Namespace { get; }

		public RegOperand(RegisterNamespace @namespace)
		{
			this.Namespace = @namespace;
		}

		public override string ToString() => throw new NotImplementedException();

		public static readonly RegOperand Gpr8_LowOnly = new RegOperand(RegisterNamespace.Gpr8_Low);
		public static readonly RegOperand Gpr8_LowHigh = new RegOperand(RegisterNamespace.Gpr8_High);
		public static readonly RegOperand Gpr16 = new RegOperand(RegisterNamespace.Gpr16);
		public static readonly RegOperand Gpr32 = new RegOperand(RegisterNamespace.Gpr32);
		public static readonly RegOperand Gpr64 = new RegOperand(RegisterNamespace.Gpr64);
	}

	public sealed class MemOperand : OperandFormat
	{
		// TODO: Data type
		private readonly byte sizeInBytes;
		public int SizeInBytes => sizeInBytes;
		public int SizeInBits => (int)sizeInBytes * 8;

		public MemOperand(int sizeInBytes) => this.sizeInBytes = checked((byte)sizeInBytes);

		public override string ToString() => "m" + SizeInBytes.ToString();

		public static readonly MemOperand M8 = new MemOperand(1);
		public static readonly MemOperand M16 = new MemOperand(2);
		public static readonly MemOperand M32 = new MemOperand(4);
		public static readonly MemOperand M64 = new MemOperand(8);
		public static readonly MemOperand M128 = new MemOperand(16);
	}

	public sealed class RegOrMemOperand : OperandFormat
	{
		public RegOperand RegSpec { get; }
		public MemOperand MemSpec { get; }

		public RegOrMemOperand(RegOperand regSpec, MemOperand memSpec)
		{
			Contract.Requires(regSpec != null);
			Contract.Requires(memSpec != null);

			// TODO: Check matching sizes
			this.RegSpec = regSpec;
			this.MemSpec = memSpec;
		}

		public override string ToString() => throw new NotImplementedException();

		public static readonly RegOrMemOperand RM8_LowOnly = new RegOrMemOperand(RegOperand.Gpr8_LowOnly, MemOperand.M8);
		public static readonly RegOrMemOperand RM8_LowHigh = new RegOrMemOperand(RegOperand.Gpr8_LowHigh, MemOperand.M8);
		public static readonly RegOrMemOperand RM16 = new RegOrMemOperand(RegOperand.Gpr16, MemOperand.M16);
		public static readonly RegOrMemOperand RM32 = new RegOrMemOperand(RegOperand.Gpr32, MemOperand.M32);
		public static readonly RegOrMemOperand RM64 = new RegOrMemOperand(RegOperand.Gpr64, MemOperand.M64);
	}

	public sealed class ImmOperand : OperandFormat
	{
		// TODO: Data type
		public override string ToString() => "imm";
	}
	
	public sealed class ConstOperand : OperandFormat
	{
		public sbyte Value { get; }

		public ConstOperand(sbyte value) => this.Value = value;

		public override string ToString() => Value.ToString();

		public static readonly ConstOperand Zero = new ConstOperand(0);
		public static readonly ConstOperand One = new ConstOperand(1);
	}
}
