using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Asmuth.X86
{
	public enum RegisterFamily : byte
	{
		Gpr,
		X87, // ST(0)-ST(7)
		Mmx, // MM0-MM7 (aliased to X87 registers but not in order)
		Sse, // [XYZ]MM0-31
		AvxOpmask, // k0-k7

		Segment, // ES, CS, SS, DS, FS, GS
		Bound, // BND0-BND3
		Debug, // DR0-DR7
		Control, // CR0-CR8

		Flags, // EFLAGS/RFLAGS
		IP, // EIP/RIP
	}

	public static class RegisterFamilyEnum
	{
		public static bool IsValidSizeInBytes(this RegisterFamily family, int sizeInBytes)
		{
			for (uint packedValidSizes = GetPackedValidSizes(family);
				(packedValidSizes & 0xFF) != 0; packedValidSizes >>= 8)
			{
				if ((packedValidSizes & 0xFF) == sizeInBytes)
					return true;
			}

			return false;
		}

		public static int? TryGetFixedSizeInBytes(this RegisterFamily family)
		{
			uint packedValidSizes = GetPackedValidSizes(family);
			return packedValidSizes < 0x100 ? (int?)packedValidSizes : null;
		}

		private static uint GetPackedValidSizes(RegisterFamily family)
		{
			switch (family)
			{
				case RegisterFamily.Gpr: return 0x08_04_02_01;
				case RegisterFamily.X87: return 0x10;
				case RegisterFamily.Mmx: return 0x08;
				case RegisterFamily.Sse: return 0x40_20_10;
				case RegisterFamily.AvxOpmask: return 0x08;
				case RegisterFamily.Segment: return 0x02;
				case RegisterFamily.Bound: return 0x10;
				case RegisterFamily.Debug: return 0x08_04;
				case RegisterFamily.Control: return 0x08_04;
				case RegisterFamily.Flags: return 0x08_04_02;
				case RegisterFamily.IP: return 0x08_04_02;
				default: throw new ArgumentOutOfRangeException(nameof(family));
			}
		}
	}

	[StructLayout(LayoutKind.Sequential, Size = 2)]
	public readonly struct RegisterClass : IEquatable<RegisterClass>
	{
		private readonly RegisterFamily family;
		private readonly byte sizeInBytes;

		public RegisterClass(RegisterFamily family)
		{
			this.family = family;
			sizeInBytes = (byte)family.TryGetFixedSizeInBytes().GetValueOrDefault();
		}

		public RegisterClass(RegisterFamily family, int sizeInBytes)
		{
			if (!family.IsValidSizeInBytes(sizeInBytes))
				throw new ArgumentOutOfRangeException(nameof(sizeInBytes));

			this.family = family;
			this.sizeInBytes = (byte)sizeInBytes;
		}

		public RegisterFamily Family => family;
		public bool IsGpr => Family == RegisterFamily.Gpr;
		public bool IsSized => sizeInBytes > 0;
		public bool IsSizedGpr => IsGpr && IsSized;
		public int? SizeInBytes => IsSized ? sizeInBytes : (int?)null;
		public int? SizeInBits => IsSized ? sizeInBytes * 8 : (int?)null;

		public string Name
		{
			get
			{
				switch (family)
				{
					case RegisterFamily.Gpr: return IsSized ? "r" + SizeInBits : "r";
					case RegisterFamily.X87: return "st";
					case RegisterFamily.Mmx: return "mm";
					case RegisterFamily.Sse:
						if (SizeInBytes == 16) return "xmm";
						if (SizeInBytes == 32) return "ymm";
						if (SizeInBytes == 64) return "zmm";
						throw new NotImplementedException();
					case RegisterFamily.AvxOpmask: return "kreg";
					case RegisterFamily.Segment: return "sreg";
					case RegisterFamily.Debug: return "dr";
					case RegisterFamily.Control: return "cr";
					case RegisterFamily.IP: return "ip";
					case RegisterFamily.Flags: return "flags";
					default: throw new NotImplementedException();
				}
			}
		}

		public override string ToString() => Name;

		public bool Equals(RegisterClass other)
			=> family == other.family && sizeInBytes == other.sizeInBytes;

		public override bool Equals(object obj)
			=> obj is RegisterClass && Equals((RegisterClass)obj);

		public override int GetHashCode() => ((int)Family << 8) | sizeInBytes;

		public static bool Equals(RegisterClass lhs, RegisterClass rhs) => lhs.Equals(rhs);
		public static bool operator ==(RegisterClass lhs, RegisterClass rhs) => Equals(lhs, rhs);
		public static bool operator !=(RegisterClass lhs, RegisterClass rhs) => !Equals(lhs, rhs);

		public static readonly RegisterClass GprUnsized = new RegisterClass(RegisterFamily.Gpr);
		// AL, CL, DL, BL, SPL ... R8b ... R15b, 20-23: AH, CH, DH, BH
		public static readonly RegisterClass GprByte = new RegisterClass(RegisterFamily.Gpr, sizeInBytes: 1);
		public static readonly RegisterClass GprWord = new RegisterClass(RegisterFamily.Gpr, sizeInBytes: 2);
		public static readonly RegisterClass GprDword = new RegisterClass(RegisterFamily.Gpr, sizeInBytes: 4);
		public static readonly RegisterClass GprQword = new RegisterClass(RegisterFamily.Gpr, sizeInBytes: 8);
		public static readonly RegisterClass X87 = new RegisterClass(RegisterFamily.X87);
		public static readonly RegisterClass Mmx = new RegisterClass(RegisterFamily.Mmx);
		public static readonly RegisterClass Xmm = new RegisterClass(RegisterFamily.Sse, sizeInBytes: 16);
		public static readonly RegisterClass Ymm = new RegisterClass(RegisterFamily.Sse, sizeInBytes: 32);
		public static readonly RegisterClass Zmm = new RegisterClass(RegisterFamily.Sse, sizeInBytes: 64);
		public static readonly RegisterClass AvxOpmask = new RegisterClass(RegisterFamily.AvxOpmask);

		public static readonly RegisterClass Segment = new RegisterClass(RegisterFamily.Segment);

		public static readonly RegisterClass DebugUnsized = new RegisterClass(RegisterFamily.Debug);
		public static readonly RegisterClass ControlUnsized = new RegisterClass(RegisterFamily.Control);

		public static readonly RegisterClass FlagsUnsized = new RegisterClass(RegisterFamily.Flags);
		public static readonly RegisterClass Flags16 = new RegisterClass(RegisterFamily.Flags, sizeInBytes: 2);
		public static readonly RegisterClass EFlags = new RegisterClass(RegisterFamily.Flags, sizeInBytes: 4);
		public static readonly RegisterClass RFlags = new RegisterClass(RegisterFamily.Flags, sizeInBytes: 8);

		public static readonly RegisterClass IPUnsized = new RegisterClass(RegisterFamily.IP);
		public static readonly RegisterClass IP16 = new RegisterClass(RegisterFamily.IP, sizeInBytes: 2);
		public static readonly RegisterClass Eip = new RegisterClass(RegisterFamily.IP, sizeInBytes: 4);
		public static readonly RegisterClass Rip = new RegisterClass(RegisterFamily.IP, sizeInBytes: 8);
	}

	[StructLayout(LayoutKind.Sequential, Size = 4)]
	public readonly partial struct Register : IEquatable<Register>
	{
		public RegisterClass Class { get; }
		public byte Index { get; }

		public Register(RegisterClass @class, int index)
		{
			// TODO: Assert valid index
			this.Class = @class;
			this.Index = (byte)index;
		}

		public Register(Gpr gpr)
		{
			if (gpr.IsHighByte)
			{
				Class = RegisterClass.GprByte;
				Index = (byte)(0x10 + gpr.Index);
			}
			else
			{
				switch (gpr.Size)
				{
					case IntegerSize.Byte: Class = RegisterClass.GprByte; break;
					case IntegerSize.Word: Class = RegisterClass.GprWord; break;
					case IntegerSize.Dword: Class = RegisterClass.GprDword; break;
					case IntegerSize.Qword: Class = RegisterClass.GprQword; break;
					default: throw new UnreachableException();
				}
				this.Index = (byte)gpr.Index;
			}
		}

		public RegisterFamily Family => Class.Family;
		public bool IsSized => Class.IsSized;
		public int? SizeInBytes => Class.SizeInBytes;
		public int? SizeInBits => Class.SizeInBits;
		public bool IsGpr => Class.IsGpr;
		public bool IsSizedGpr => Class.IsSizedGpr;

		public Gpr AsSizedGpr()
		{
			if (!IsSizedGpr) throw new InvalidOperationException();
			switch (SizeInBytes.Value)
			{
				case 1: return Index < 0x10 ? Gpr.Byte(Index) : Gpr.HighByte(Index & 0xF);
				case 2: return Gpr.Word(Index);
				case 4: return Gpr.Dword(Index);
				case 8: return Gpr.Qword(Index);
				default: throw new UnreachableException();
			}
		}

		public override string ToString() => Name;

		public bool Equals(Register other)
			=> Class == other.Class && Index == other.Index;

		public override bool Equals(object obj)
			=> obj is Register && Equals((Register)obj);

		public override int GetHashCode() => (Class.GetHashCode() << 8) | Index;

		public static Register First(RegisterClass @class) => new Register(@class, 0);

		public static bool Equals(Register lhs, Register rhs) => lhs.Equals(rhs);

		public static bool operator ==(Register lhs, Register rhs) => Equals(lhs, rhs);
		public static bool operator !=(Register lhs, Register rhs) => !Equals(lhs, rhs);

		public static readonly Register AL = new Register(RegisterClass.GprByte, 0);
		public static readonly Register CL = new Register(RegisterClass.GprByte, 1);
		public static readonly Register DL = new Register(RegisterClass.GprByte, 2);
		public static readonly Register BL = new Register(RegisterClass.GprByte, 3);
		public static readonly Register Spl = new Register(RegisterClass.GprByte, 4);
		public static readonly Register Bpl = new Register(RegisterClass.GprByte, 5);
		public static readonly Register Sil = new Register(RegisterClass.GprByte, 6);
		public static readonly Register Dil = new Register(RegisterClass.GprByte, 7);
		public static readonly Register R8b = new Register(RegisterClass.GprByte, 8);
		public static readonly Register R9b = new Register(RegisterClass.GprByte, 9);
		public static readonly Register R10b = new Register(RegisterClass.GprByte, 10);
		public static readonly Register R11b = new Register(RegisterClass.GprByte, 11);
		public static readonly Register R12b = new Register(RegisterClass.GprByte, 12);
		public static readonly Register R13b = new Register(RegisterClass.GprByte, 13);
		public static readonly Register R14b = new Register(RegisterClass.GprByte, 14);
		public static readonly Register R15b = new Register(RegisterClass.GprByte, 15);
		public static readonly Register AH = new Register(RegisterClass.GprByte, 0x10);
		public static readonly Register CH = new Register(RegisterClass.GprByte, 0x11);
		public static readonly Register DH = new Register(RegisterClass.GprByte, 0x12);
		public static readonly Register BH = new Register(RegisterClass.GprByte, 0x13);

		public static readonly Register AX = new Register(RegisterClass.GprWord, 0);
		public static readonly Register CX = new Register(RegisterClass.GprWord, 1);
		public static readonly Register DX = new Register(RegisterClass.GprWord, 2);
		public static readonly Register BX = new Register(RegisterClass.GprWord, 3);
		public static readonly Register SP = new Register(RegisterClass.GprWord, 4);
		public static readonly Register BP = new Register(RegisterClass.GprWord, 5);
		public static readonly Register SI = new Register(RegisterClass.GprWord, 6);
		public static readonly Register DI = new Register(RegisterClass.GprWord, 7);
		public static readonly Register R8w = new Register(RegisterClass.GprWord, 8);
		public static readonly Register R9w = new Register(RegisterClass.GprWord, 9);
		public static readonly Register R10w = new Register(RegisterClass.GprWord, 10);
		public static readonly Register R11w = new Register(RegisterClass.GprWord, 11);
		public static readonly Register R12w = new Register(RegisterClass.GprWord, 12);
		public static readonly Register R13w = new Register(RegisterClass.GprWord, 13);
		public static readonly Register R14w = new Register(RegisterClass.GprWord, 14);
		public static readonly Register R15w = new Register(RegisterClass.GprWord, 15);

		public static readonly Register Eax = new Register(RegisterClass.GprDword, 0);
		public static readonly Register Ecx = new Register(RegisterClass.GprDword, 1);
		public static readonly Register Edx = new Register(RegisterClass.GprDword, 2);
		public static readonly Register Ebx = new Register(RegisterClass.GprDword, 3);
		public static readonly Register Esp = new Register(RegisterClass.GprDword, 4);
		public static readonly Register Ebp = new Register(RegisterClass.GprDword, 5);
		public static readonly Register Esi = new Register(RegisterClass.GprDword, 6);
		public static readonly Register Edi = new Register(RegisterClass.GprDword, 7);
		public static readonly Register R8d = new Register(RegisterClass.GprDword, 8);
		public static readonly Register R9d = new Register(RegisterClass.GprDword, 9);
		public static readonly Register R10d = new Register(RegisterClass.GprDword, 10);
		public static readonly Register R11d = new Register(RegisterClass.GprDword, 11);
		public static readonly Register R12d = new Register(RegisterClass.GprDword, 12);
		public static readonly Register R13d = new Register(RegisterClass.GprDword, 13);
		public static readonly Register R14d = new Register(RegisterClass.GprDword, 14);
		public static readonly Register R15d = new Register(RegisterClass.GprDword, 15);

		public static readonly Register Rax = new Register(RegisterClass.GprQword, 0);
		public static readonly Register Rcx = new Register(RegisterClass.GprQword, 1);
		public static readonly Register Rdx = new Register(RegisterClass.GprQword, 2);
		public static readonly Register Rbx = new Register(RegisterClass.GprQword, 3);
		public static readonly Register Rsp = new Register(RegisterClass.GprQword, 4);
		public static readonly Register Rbp = new Register(RegisterClass.GprQword, 5);
		public static readonly Register Rsi = new Register(RegisterClass.GprQword, 6);
		public static readonly Register Rdi = new Register(RegisterClass.GprQword, 7);
		public static readonly Register R8 = new Register(RegisterClass.GprQword, 8);
		public static readonly Register R9 = new Register(RegisterClass.GprQword, 9);
		public static readonly Register R10 = new Register(RegisterClass.GprQword, 10);
		public static readonly Register R11 = new Register(RegisterClass.GprQword, 11);
		public static readonly Register R12 = new Register(RegisterClass.GprQword, 12);
		public static readonly Register R13 = new Register(RegisterClass.GprQword, 13);
		public static readonly Register R14 = new Register(RegisterClass.GprQword, 14);
		public static readonly Register R15 = new Register(RegisterClass.GprQword, 15);

		public static readonly Register ST0 = new Register(RegisterClass.X87, 0);
		public static readonly Register ST1 = new Register(RegisterClass.X87, 1);
		public static readonly Register ST2 = new Register(RegisterClass.X87, 2);
		public static readonly Register ST3 = new Register(RegisterClass.X87, 3);
		public static readonly Register ST4 = new Register(RegisterClass.X87, 4);
		public static readonly Register ST5 = new Register(RegisterClass.X87, 5);
		public static readonly Register ST6 = new Register(RegisterClass.X87, 6);
		public static readonly Register ST7 = new Register(RegisterClass.X87, 7);

		public static readonly Register MM0 = new Register(RegisterClass.Mmx, 0);
		public static readonly Register MM1 = new Register(RegisterClass.Mmx, 1);
		public static readonly Register MM2 = new Register(RegisterClass.Mmx, 2);
		public static readonly Register MM3 = new Register(RegisterClass.Mmx, 3);
		public static readonly Register MM4 = new Register(RegisterClass.Mmx, 4);
		public static readonly Register MM5 = new Register(RegisterClass.Mmx, 5);
		public static readonly Register MM6 = new Register(RegisterClass.Mmx, 6);
		public static readonly Register MM7 = new Register(RegisterClass.Mmx, 7);

		public static readonly Register Xmm0 = new Register(RegisterClass.Xmm, 0);
		public static readonly Register Xmm1 = new Register(RegisterClass.Xmm, 1);
		public static readonly Register Xmm2 = new Register(RegisterClass.Xmm, 2);
		public static readonly Register Xmm3 = new Register(RegisterClass.Xmm, 3);
		public static readonly Register Xmm4 = new Register(RegisterClass.Xmm, 4);
		public static readonly Register Xmm5 = new Register(RegisterClass.Xmm, 5);
		public static readonly Register Xmm6 = new Register(RegisterClass.Xmm, 6);
		public static readonly Register Xmm7 = new Register(RegisterClass.Xmm, 7);
		public static readonly Register Xmm8 = new Register(RegisterClass.Xmm, 8);
		public static readonly Register Xmm9 = new Register(RegisterClass.Xmm, 9);
		public static readonly Register Xmm10 = new Register(RegisterClass.Xmm, 10);
		public static readonly Register Xmm11 = new Register(RegisterClass.Xmm, 11);
		public static readonly Register Xmm12 = new Register(RegisterClass.Xmm, 12);
		public static readonly Register Xmm13 = new Register(RegisterClass.Xmm, 13);
		public static readonly Register Xmm14 = new Register(RegisterClass.Xmm, 14);
		public static readonly Register Xmm15 = new Register(RegisterClass.Xmm, 15);

		public static readonly Register Ymm0 = new Register(RegisterClass.Ymm, 0);
		public static readonly Register Ymm1 = new Register(RegisterClass.Ymm, 1);
		public static readonly Register Ymm2 = new Register(RegisterClass.Ymm, 2);
		public static readonly Register Ymm3 = new Register(RegisterClass.Ymm, 3);
		public static readonly Register Ymm4 = new Register(RegisterClass.Ymm, 4);
		public static readonly Register Ymm5 = new Register(RegisterClass.Ymm, 5);
		public static readonly Register Ymm6 = new Register(RegisterClass.Ymm, 6);
		public static readonly Register Ymm7 = new Register(RegisterClass.Ymm, 7);
		public static readonly Register Ymm8 = new Register(RegisterClass.Ymm, 8);
		public static readonly Register Ymm9 = new Register(RegisterClass.Ymm, 9);
		public static readonly Register Ymm10 = new Register(RegisterClass.Ymm, 10);
		public static readonly Register Ymm11 = new Register(RegisterClass.Ymm, 11);
		public static readonly Register Ymm12 = new Register(RegisterClass.Ymm, 12);
		public static readonly Register Ymm13 = new Register(RegisterClass.Ymm, 13);
		public static readonly Register Ymm14 = new Register(RegisterClass.Ymm, 14);
		public static readonly Register Ymm15 = new Register(RegisterClass.Ymm, 15);

		public static readonly Register Zmm0 = new Register(RegisterClass.Zmm, 0);
		public static readonly Register Zmm1 = new Register(RegisterClass.Zmm, 1);
		public static readonly Register Zmm2 = new Register(RegisterClass.Zmm, 2);
		public static readonly Register Zmm3 = new Register(RegisterClass.Zmm, 3);
		public static readonly Register Zmm4 = new Register(RegisterClass.Zmm, 4);
		public static readonly Register Zmm5 = new Register(RegisterClass.Zmm, 5);
		public static readonly Register Zmm6 = new Register(RegisterClass.Zmm, 6);
		public static readonly Register Zmm7 = new Register(RegisterClass.Zmm, 7);
		public static readonly Register Zmm8 = new Register(RegisterClass.Zmm, 8);
		public static readonly Register Zmm9 = new Register(RegisterClass.Zmm, 9);
		public static readonly Register Zmm10 = new Register(RegisterClass.Zmm, 10);
		public static readonly Register Zmm11 = new Register(RegisterClass.Zmm, 11);
		public static readonly Register Zmm12 = new Register(RegisterClass.Zmm, 12);
		public static readonly Register Zmm13 = new Register(RegisterClass.Zmm, 13);
		public static readonly Register Zmm14 = new Register(RegisterClass.Zmm, 14);
		public static readonly Register Zmm15 = new Register(RegisterClass.Zmm, 15);

		public static readonly Register K0 = new Register(RegisterClass.AvxOpmask, 0);
		public static readonly Register K1 = new Register(RegisterClass.AvxOpmask, 1);
		public static readonly Register K2 = new Register(RegisterClass.AvxOpmask, 2);
		public static readonly Register K3 = new Register(RegisterClass.AvxOpmask, 3);
		public static readonly Register K4 = new Register(RegisterClass.AvxOpmask, 4);
		public static readonly Register K5 = new Register(RegisterClass.AvxOpmask, 5);
		public static readonly Register K6 = new Register(RegisterClass.AvxOpmask, 6);
		public static readonly Register K7 = new Register(RegisterClass.AvxOpmask, 7);

		public static readonly Register DR0 = new Register(RegisterClass.DebugUnsized, 0);
		public static readonly Register DR1 = new Register(RegisterClass.DebugUnsized, 1);
		public static readonly Register DR2 = new Register(RegisterClass.DebugUnsized, 2);
		public static readonly Register DR3 = new Register(RegisterClass.DebugUnsized, 3);
		public static readonly Register DR4 = new Register(RegisterClass.DebugUnsized, 4);
		public static readonly Register DR5 = new Register(RegisterClass.DebugUnsized, 5);
		public static readonly Register DR6 = new Register(RegisterClass.DebugUnsized, 6);
		public static readonly Register DR7 = new Register(RegisterClass.DebugUnsized, 7);

		public static readonly Register CR0 = new Register(RegisterClass.ControlUnsized, 0);
		public static readonly Register CR1 = new Register(RegisterClass.ControlUnsized, 1);
		public static readonly Register CR2 = new Register(RegisterClass.ControlUnsized, 2);
		public static readonly Register CR3 = new Register(RegisterClass.ControlUnsized, 3);
		public static readonly Register CR4 = new Register(RegisterClass.ControlUnsized, 4);
		public static readonly Register CR5 = new Register(RegisterClass.ControlUnsized, 5);
		public static readonly Register CR6 = new Register(RegisterClass.ControlUnsized, 6);
		public static readonly Register CR7 = new Register(RegisterClass.ControlUnsized, 7);
		public static readonly Register CR8 = new Register(RegisterClass.ControlUnsized, 8);
		
		public static readonly Register ES = new Register(RegisterClass.Segment, 0);
		public static readonly Register CS = new Register(RegisterClass.Segment, 1);
		public static readonly Register SS = new Register(RegisterClass.Segment, 2);
		public static readonly Register DS = new Register(RegisterClass.Segment, 3);
		public static readonly Register FS = new Register(RegisterClass.Segment, 4);
		public static readonly Register GS = new Register(RegisterClass.Segment, 5);
		
		public static readonly Register FlagsUnsized = First(RegisterClass.FlagsUnsized);
		public static readonly Register Flags16 = First(RegisterClass.Flags16);
		public static readonly Register EFlags = First(RegisterClass.EFlags);
		public static readonly Register RFlags = First(RegisterClass.RFlags);

		public static readonly Register IPUnsized = First(RegisterClass.IPUnsized);
		public static readonly Register IP16 = First(RegisterClass.IP16);
		public static readonly Register Eip = First(RegisterClass.Eip);
		public static readonly Register Rip = First(RegisterClass.Rip);
	}
}
