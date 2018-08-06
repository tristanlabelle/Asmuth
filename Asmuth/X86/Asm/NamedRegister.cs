using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;
using System.Text;

namespace Asmuth.X86.Asm
{
	public enum RegisterNamespace : byte
	{
		Gpr8, // AL, CL, DL, BL, SPL ... R8b ... R15b, 20-23: AH, CH, DH, BH
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
		public static bool IsGpr(this RegisterNamespace @namespace)
			=> @namespace <= RegisterNamespace.Gpr64;
		public static bool IsSse(this RegisterNamespace @namespace)
			=> @namespace >= RegisterNamespace.Xmm && @namespace <= RegisterNamespace.Zmm;
		public static OperandSize? TryGetIntegerSize(this RegisterNamespace @namespace)
		{
			switch (@namespace)
			{
				case RegisterNamespace.Gpr8: return OperandSize.Byte;
				case RegisterNamespace.Gpr16: return OperandSize.Word;
				case RegisterNamespace.Gpr32: return OperandSize.Dword;
				case RegisterNamespace.Gpr64: return OperandSize.Qword;
				default: return null;
			}
		}
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

		public static readonly NamedRegister AL = new NamedRegister(RegisterNamespace.Gpr8, 0);
		public static readonly NamedRegister CL = new NamedRegister(RegisterNamespace.Gpr8, 1);
		public static readonly NamedRegister DL = new NamedRegister(RegisterNamespace.Gpr8, 2);
		public static readonly NamedRegister BL = new NamedRegister(RegisterNamespace.Gpr8, 3);
		public static readonly NamedRegister Spl = new NamedRegister(RegisterNamespace.Gpr8, 4);
		public static readonly NamedRegister Bpl = new NamedRegister(RegisterNamespace.Gpr8, 5);
		public static readonly NamedRegister Sil = new NamedRegister(RegisterNamespace.Gpr8, 6);
		public static readonly NamedRegister Dil = new NamedRegister(RegisterNamespace.Gpr8, 7);
		public static readonly NamedRegister AH = new NamedRegister(RegisterNamespace.Gpr8, 0x14);
		public static readonly NamedRegister CH = new NamedRegister(RegisterNamespace.Gpr8, 0x15);
		public static readonly NamedRegister DH = new NamedRegister(RegisterNamespace.Gpr8, 0x16);
		public static readonly NamedRegister BH = new NamedRegister(RegisterNamespace.Gpr8, 0x17);

		public static readonly NamedRegister AX = new NamedRegister(RegisterNamespace.Gpr16, 0);
		public static readonly NamedRegister CX = new NamedRegister(RegisterNamespace.Gpr16, 1);
		public static readonly NamedRegister DX = new NamedRegister(RegisterNamespace.Gpr16, 2);
		public static readonly NamedRegister BX = new NamedRegister(RegisterNamespace.Gpr16, 3);
		public static readonly NamedRegister SP = new NamedRegister(RegisterNamespace.Gpr16, 4);
		public static readonly NamedRegister BP = new NamedRegister(RegisterNamespace.Gpr16, 5);
		public static readonly NamedRegister SI = new NamedRegister(RegisterNamespace.Gpr16, 6);
		public static readonly NamedRegister DI = new NamedRegister(RegisterNamespace.Gpr16, 7);

		public static readonly NamedRegister Eax = new NamedRegister(RegisterNamespace.Gpr32, 0);
		public static readonly NamedRegister Ecx = new NamedRegister(RegisterNamespace.Gpr32, 1);
		public static readonly NamedRegister Edx = new NamedRegister(RegisterNamespace.Gpr32, 2);
		public static readonly NamedRegister Ebx = new NamedRegister(RegisterNamespace.Gpr32, 3);
		public static readonly NamedRegister Esp = new NamedRegister(RegisterNamespace.Gpr32, 4);
		public static readonly NamedRegister Ebp = new NamedRegister(RegisterNamespace.Gpr32, 5);
		public static readonly NamedRegister Esi = new NamedRegister(RegisterNamespace.Gpr32, 6);
		public static readonly NamedRegister Edi = new NamedRegister(RegisterNamespace.Gpr32, 7);

		public static readonly NamedRegister Rax = new NamedRegister(RegisterNamespace.Gpr64, 0);
		public static readonly NamedRegister Rcx = new NamedRegister(RegisterNamespace.Gpr64, 1);
		public static readonly NamedRegister Rdx = new NamedRegister(RegisterNamespace.Gpr64, 2);
		public static readonly NamedRegister Rbx = new NamedRegister(RegisterNamespace.Gpr64, 3);
		public static readonly NamedRegister Rsp = new NamedRegister(RegisterNamespace.Gpr64, 4);
		public static readonly NamedRegister Rbp = new NamedRegister(RegisterNamespace.Gpr64, 5);
		public static readonly NamedRegister Rsi = new NamedRegister(RegisterNamespace.Gpr64, 6);
		public static readonly NamedRegister Rdi = new NamedRegister(RegisterNamespace.Gpr64, 7);

		public static readonly NamedRegister ST0 = new NamedRegister(RegisterNamespace.X87, 0);
		public static readonly NamedRegister ST1 = new NamedRegister(RegisterNamespace.X87, 1);
		public static readonly NamedRegister ST2 = new NamedRegister(RegisterNamespace.X87, 2);
		public static readonly NamedRegister ST3 = new NamedRegister(RegisterNamespace.X87, 3);
		public static readonly NamedRegister ST4 = new NamedRegister(RegisterNamespace.X87, 4);
		public static readonly NamedRegister ST5 = new NamedRegister(RegisterNamespace.X87, 5);
		public static readonly NamedRegister ST6 = new NamedRegister(RegisterNamespace.X87, 6);
		public static readonly NamedRegister ST7 = new NamedRegister(RegisterNamespace.X87, 7);
	}
}
