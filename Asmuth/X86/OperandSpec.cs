using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Text;

namespace Asmuth.X86
{
	public class OperandSpec {}

	public sealed class FixedRegOperandSpec : OperandSpec
	{
		public NamedRegister Register { get; }

		public FixedRegOperandSpec(NamedRegister register) => this.Register = register;
	}

	public sealed class RegOperandSpec : OperandSpec
	{
		// Gpr8_High: AL, CL, DL, BL, AH, CH, DH, BH
		public RegisterNamespace Namespace { get; }

		public RegOperandSpec(RegisterNamespace @namespace)
		{
			this.Namespace = @namespace;
		}

		public static readonly RegOperandSpec Gpr8_LowOnly = new RegOperandSpec(RegisterNamespace.Gpr8_Low);
		public static readonly RegOperandSpec Gpr8_LowHigh = new RegOperandSpec(RegisterNamespace.Gpr8_High);
		public static readonly RegOperandSpec Gpr16 = new RegOperandSpec(RegisterNamespace.Gpr16);
		public static readonly RegOperandSpec Gpr32 = new RegOperandSpec(RegisterNamespace.Gpr32);
		public static readonly RegOperandSpec Gpr64 = new RegOperandSpec(RegisterNamespace.Gpr64);
	}

	public sealed class MemOperandSpec : OperandSpec
	{
		private readonly byte sizeInBytes;
		public int SizeInBytes => sizeInBytes;
		public int SizeInBits => (int)sizeInBytes * 8;

		public MemOperandSpec(int sizeInBytes) => this.sizeInBytes = checked((byte)sizeInBytes);

		public static readonly MemOperandSpec M8 = new MemOperandSpec(1);
		public static readonly MemOperandSpec M16 = new MemOperandSpec(2);
		public static readonly MemOperandSpec M32 = new MemOperandSpec(4);
		public static readonly MemOperandSpec M64 = new MemOperandSpec(8);
		public static readonly MemOperandSpec M128 = new MemOperandSpec(16);
	}

	public sealed class RegOrMemOperandSpec : OperandSpec
	{
		public RegOperandSpec RegSpec { get; }
		public MemOperandSpec MemSpec { get; }

		public RegOrMemOperandSpec(RegOperandSpec regSpec, MemOperandSpec memSpec)
		{
			Contract.Requires(regSpec != null);
			Contract.Requires(memSpec != null);
			this.RegSpec = regSpec;
			this.MemSpec = memSpec;
		}

		public static readonly RegOrMemOperandSpec RM8_LowOnly = new RegOrMemOperandSpec(RegOperandSpec.Gpr8_LowOnly, MemOperandSpec.M8);
		public static readonly RegOrMemOperandSpec RM8_LowHigh = new RegOrMemOperandSpec(RegOperandSpec.Gpr8_LowHigh, MemOperandSpec.M8);
		public static readonly RegOrMemOperandSpec RM16 = new RegOrMemOperandSpec(RegOperandSpec.Gpr16, MemOperandSpec.M16);
		public static readonly RegOrMemOperandSpec RM32 = new RegOrMemOperandSpec(RegOperandSpec.Gpr32, MemOperandSpec.M32);
		public static readonly RegOrMemOperandSpec RM64 = new RegOrMemOperandSpec(RegOperandSpec.Gpr64, MemOperandSpec.M64);
	}

	public sealed class ImmOperandSpec : OperandSpec
	{
		// TODO
	}
	
	public sealed class ConstOperandSpec : OperandSpec
	{
		public sbyte Value { get; }

		public ConstOperandSpec(sbyte value) => this.Value = value;
	}
}
