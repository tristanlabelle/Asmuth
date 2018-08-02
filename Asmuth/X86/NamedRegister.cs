using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;
using System.Text;

namespace Asmuth.X86
{
	public enum RegisterNamespace : byte
	{
		Gpr8_Low, // AL, CL, DL, BL, SPL ... R8b ... R15b
		Gpr8_High, // 4-7: AH, CH, DH, BH
		Gpr16, // AX ... R15W
		Gpr32, // EAX ... R15D
		Gpr64, // RAX ... R15

		X87, // ST(0)-ST(7)
		Mmx, // MM0-MM7
		Xmm, // XMM0-XMM31
		Ymm, // YMM0-YMM31
		Zmm, // ZMM0-ZMM31

		AvxOpmask, // k0-k7

		Segment, // ES, CS, SS, DS, FS, GS
		Debug, // DR0-DR7
		Control, // CR0-CR8

		Flags, // EFLAGS/RFLAGS
		IP, // EIP/RIP
	}

	public static class RegisterNamespaceEnum
	{
		[Pure]
		public static bool IsGpr(this RegisterNamespace @namespace)
			=> @namespace <= RegisterNamespace.Gpr64;

		[Pure]
		public static bool IsSse(this RegisterNamespace @namespace)
			=> @namespace >= RegisterNamespace.Xmm && @namespace <= RegisterNamespace.Zmm;
	}

	/// <summary>
	/// Identifies a register that has a name, even though two registers with
	/// different names may alias to the same underlying physical register.
	/// </summary>
	[StructLayout(LayoutKind.Sequential, Size = 2)]
	public readonly struct NamedRegister : IEquatable<NamedRegister>
	{
		public RegisterNamespace Namespace { get; }
		public byte Index { get; }

		public NamedRegister(RegisterNamespace @namespace, byte index)
		{
			this.Namespace = @namespace;
			this.Index = index;
		}

		public bool Equals(NamedRegister other)
			=> Namespace == other.Namespace && Index == other.Index;

		public override bool Equals(object obj)
			=> obj is NamedRegister && Equals((NamedRegister)obj);

		public override int GetHashCode() => ((int)Namespace << 8) | Index;

		public static bool Equals(NamedRegister lhs, NamedRegister rhs) => lhs.Equals(rhs);

		public static bool operator ==(NamedRegister lhs, NamedRegister rhs) => Equals(lhs, rhs);
		public static bool operator !=(NamedRegister lhs, NamedRegister rhs) => !Equals(lhs, rhs);

		public static readonly NamedRegister AL = new NamedRegister(RegisterNamespace.Gpr8_Low, 0);
		public static readonly NamedRegister CL = new NamedRegister(RegisterNamespace.Gpr8_Low, 1);
		public static readonly NamedRegister DL = new NamedRegister(RegisterNamespace.Gpr8_Low, 2);
		public static readonly NamedRegister BL = new NamedRegister(RegisterNamespace.Gpr8_Low, 3);
		public static readonly NamedRegister Spl = new NamedRegister(RegisterNamespace.Gpr8_Low, 4);
		public static readonly NamedRegister Bpl = new NamedRegister(RegisterNamespace.Gpr8_Low, 5);
		public static readonly NamedRegister Sil = new NamedRegister(RegisterNamespace.Gpr8_Low, 6);
		public static readonly NamedRegister Dil = new NamedRegister(RegisterNamespace.Gpr8_Low, 7);
		public static readonly NamedRegister AH = new NamedRegister(RegisterNamespace.Gpr8_High, 4);
		public static readonly NamedRegister CH = new NamedRegister(RegisterNamespace.Gpr8_High, 5);
		public static readonly NamedRegister DH = new NamedRegister(RegisterNamespace.Gpr8_High, 6);
		public static readonly NamedRegister BH = new NamedRegister(RegisterNamespace.Gpr8_High, 7);

		public static readonly NamedRegister AX = new NamedRegister(RegisterNamespace.Gpr16, 0);
		public static readonly NamedRegister CX = new NamedRegister(RegisterNamespace.Gpr16, 1);
		public static readonly NamedRegister DX = new NamedRegister(RegisterNamespace.Gpr16, 2);
		public static readonly NamedRegister BX = new NamedRegister(RegisterNamespace.Gpr16, 3);
		public static readonly NamedRegister SP = new NamedRegister(RegisterNamespace.Gpr16, 4);
		public static readonly NamedRegister BP = new NamedRegister(RegisterNamespace.Gpr16, 5);
		public static readonly NamedRegister SI = new NamedRegister(RegisterNamespace.Gpr16, 6);
		public static readonly NamedRegister DI = new NamedRegister(RegisterNamespace.Gpr16, 7);

		public static readonly NamedRegister EAX = new NamedRegister(RegisterNamespace.Gpr32, 0);
		public static readonly NamedRegister ECX = new NamedRegister(RegisterNamespace.Gpr32, 1);
		public static readonly NamedRegister EDX = new NamedRegister(RegisterNamespace.Gpr32, 2);
		public static readonly NamedRegister EBX = new NamedRegister(RegisterNamespace.Gpr32, 3);
		public static readonly NamedRegister ESP = new NamedRegister(RegisterNamespace.Gpr32, 4);
		public static readonly NamedRegister EBP = new NamedRegister(RegisterNamespace.Gpr32, 5);
		public static readonly NamedRegister ESI = new NamedRegister(RegisterNamespace.Gpr32, 6);
		public static readonly NamedRegister EDI = new NamedRegister(RegisterNamespace.Gpr32, 7);

		public static readonly NamedRegister RAX = new NamedRegister(RegisterNamespace.Gpr64, 0);
		public static readonly NamedRegister RCX = new NamedRegister(RegisterNamespace.Gpr64, 1);
		public static readonly NamedRegister RDX = new NamedRegister(RegisterNamespace.Gpr64, 2);
		public static readonly NamedRegister RBX = new NamedRegister(RegisterNamespace.Gpr64, 3);
		public static readonly NamedRegister RSP = new NamedRegister(RegisterNamespace.Gpr64, 4);
		public static readonly NamedRegister RBP = new NamedRegister(RegisterNamespace.Gpr64, 5);
		public static readonly NamedRegister RSI = new NamedRegister(RegisterNamespace.Gpr64, 6);
		public static readonly NamedRegister RDI = new NamedRegister(RegisterNamespace.Gpr64, 7);
	}
}
