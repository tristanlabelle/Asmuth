using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	public enum GprCode : byte
	{
		A = 0, C, D, B,

		AL = 0, CL, DL, BL, SplOrAH, BplOrCH, SilOrDH, DilOrBH,
		R8b, R9b, R10b, R11b, R12b, R13b, R14b, R15b,

		AX = 0, CX, DX, BX, SP, BP, SI, DI,
		R8w, R9w, R10w, R11w, R12w, R13w, R14w, R15w,

		Eax = 0, Ecx, Edx, Ebx, Esp, Ebp, Esi, Edi,
		R8d, R9d, R10d, R11d, R12d, R13d, R14d, R15d,

		Rax = 0, Rcx, Rdx, Rbx, Rsp, Rbp, Rsi, Rdi,
		R8, R9, R10, R11, R12, R13, R14, R15,
	}

	public static class GprCodeEnum
	{
		public static bool RequiresRexBit(this GprCode code) => code >= GprCode.R8;
		public static byte GetLow3Bits(this GprCode code) => (byte)((int)code & 0x7);
	}

	public readonly struct Gpr : IEquatable<Gpr>
	{
		private enum HighByteTag : byte { }

		// 0b0000_1111: index
		// 0b0001_0000: high byte bit
		// 0b1110_0000: size
		private readonly byte value;

		private Gpr(int index, HighByteTag tag)
		{
			if (index >= 4) throw new ArgumentOutOfRangeException(nameof(index));
			this.value = (byte)(index | 0b1_0000);
		}
		
		public Gpr(int index, IntegerSize size)
		{
			if (unchecked((uint)index) >= 0x10) throw new ArgumentOutOfRangeException(nameof(index));
			value = unchecked((byte)(index | ((int)size << 5)));
		}

		public Gpr(GprCode code, IntegerSize size, bool hasRex)
		{
			if (unchecked((uint)code) >= (hasRex ? 0x10 : 0x8))
				throw new ArgumentOutOfRangeException(nameof(code));
			if (size == IntegerSize.Byte && !hasRex && (int)code >= 4)
				value = unchecked((byte)(((int)code + 12) | ((int)size << 5)));
			else
				value = unchecked((byte)((int)code | ((int)size << 5)));
		}

		public int Index => value & 0xF;
		public bool IsHighByte => (value & 0b0001_0000) != 0;
		public bool IsExtended => Index >= 8;
		public GprCode Code => IsHighByte ? (GprCode)(Index + 4) : (GprCode)Index;
		public IntegerSize Size => (IntegerSize)(value >> 5);
		public int SizeInBytes => Size.InBytes();
		public int SizeInBits => Size.InBits();

		public bool? RexPresence
		{
			get
			{
				if (IsHighByte) return false;
				if (Index >= (Size == IntegerSize.Byte ? 4 : 8)) return true;
				return null; // Optional
			}
		}
		
		public string Name
		{
			get
			{
				string name = GetBaseName(Index);

				if (Index < 8)
				{
					if (IsHighByte) name += "h";
					else if (Size == IntegerSize.Byte) name += "l";
					else
					{
						if (Index < 4) name += "x";
						if (Size == IntegerSize.Dword) name = "e" + name;
						else if (Size == IntegerSize.Qword) name = "r" + name;
					}
				}
				else
				{
					if (Size == IntegerSize.Byte) name += "b";
					else if (Size == IntegerSize.Word) name += "w";
					else if (Size == IntegerSize.Dword) name += "d";
				}

				return name;
			}
		}

		// Operator overload abuse for nice code emitting syntax
		public EffectiveAddress this[int displacement] => EffectiveAddress.Indirect(this, displacement);

		public override bool Equals(object obj) => obj is Gpr && Equals((Gpr)obj);
		public bool Equals(Gpr other) => value == other.value;
		public override int GetHashCode() => value;
		public override string ToString() => Name;

		public static bool Equals(Gpr first, Gpr second) => first.Equals(second);
		public static bool operator ==(Gpr lhs, Gpr rhs) => Equals(lhs, rhs);
		public static bool operator !=(Gpr lhs, Gpr rhs) => Equals(lhs, rhs);

		public static Gpr Byte(int index) => new Gpr(index, IntegerSize.Byte);
		public static Gpr Byte(int index, bool highByte = false)
			=> highByte ? HighByte(index) : Byte(index);
		public static Gpr HighByte(int index) => new Gpr(index, default(HighByteTag));
		public static Gpr Word(int index) => new Gpr(index, IntegerSize.Word);
		public static Gpr Dword(int index) => new Gpr(index, IntegerSize.Dword);
		public static Gpr Qword(int index) => new Gpr(index, IntegerSize.Qword);
		public static Gpr Byte(GprCode code, bool hasRex) => new Gpr(code, IntegerSize.Byte, hasRex);
		public static Gpr Word(GprCode code) => Word((int)code);
		public static Gpr Dword(GprCode code) => Dword((int)code);
		public static Gpr Qword(GprCode code) => Qword((int)code);

		#region Static parsing
		public static string GetBaseName(int index)
		{
			if (unchecked((uint)index) >= 0x10)
				throw new ArgumentOutOfRangeException(nameof(index));
			if (index < 4) return "acdb"[index].ToString();
			if (index >= 10) return "r1" + "012345"[index - 10];
			if (index == 4) return "sp";
			if (index == 5) return "bp";
			if (index == 6) return "si";
			if (index == 7) return "di";
			if (index == 8) return "r8";
			if (index == 9) return "r9";
			throw new UnreachableException();
		}

		public static IntegerSize? TryParseRSuffix(char c)
		{
			switch (c)
			{
				case 'D': case 'd': return IntegerSize.Dword;
				case 'W': case 'w': return IntegerSize.Word;
				case 'B': case 'b': return IntegerSize.Byte;
				default: return null;
			}
		}

		public static Gpr? TryParse(string str)
		{
			if (str == null) throw new ArgumentNullException(nameof(str));

			if (str.Length < 2 || str.Length > 4) return null;

			str = str.ToLowerInvariant();

			// R8 to R15
			var rMatch = Regex.Match(str, @"\Ar([89]|1[0-5])([bwd])?\Z", RegexOptions.CultureInvariant);
			if (rMatch.Success)
			{
				int index = int.Parse(rMatch.Groups[1].Value, CultureInfo.InvariantCulture);
				IntegerSize size = IntegerSize.Qword;
				if (rMatch.Groups[2].Success)
					size = TryParseRSuffix(rMatch.Groups[2].Value[0]).Value;
				return new Gpr(index, size);
			}

			if (str.Length == 2)
			{
				if (str[1] == 'i')
				{
					// [SD]I
					if (str[0] == 's') return SI;
					if (str[0] == 'd') return DI;
					return null;
				}

				if (str[1] == 'p')
				{
					// [SD]I
					if (str[0] == 's') return SP;
					if (str[0] == 'b') return BP;
					return null;
				}

				// [ABCD][XLH]
				int index = "acdb".IndexOf(str[0]);
				if (index < 0) return null;

				if (str[1] == 'x') return Word(index);
				else if (str[1] == 'l') return Byte(index);
				else if (str[1] == 'h') return HighByte(index);
				return null;
			}
			else if (str.Length == 3)
			{
				// [ER] + 16-bit register
				if (str[0] == 'e' || str[0] == 'r')
				{
					var baseRegister = TryParse(str.Substring(1));
					if (!baseRegister.HasValue || baseRegister.Value.Size != IntegerSize.Word)
						return null;
					return new Gpr(baseRegister.Value.Index,
						str[0] == 'e' ? IntegerSize.Dword : IntegerSize.Qword);
				}

				// (SP|BP|SI|DI)L
				if (str[2] == 'l')
				{
					var baseRegister = TryParse(str.Substring(0, 2));
					if (!baseRegister.HasValue || baseRegister.Value.Index < 4 || baseRegister.Value.Index >= 8) return null;
					return Byte(baseRegister.Value.Index);
				}
			}

			return null;
		}
		#endregion
		#region Static instances
		public static readonly Gpr AL = Byte(0);
		public static readonly Gpr CL = Byte(1);
		public static readonly Gpr DL = Byte(2);
		public static readonly Gpr BL = Byte(3);
		public static readonly Gpr Spl = Byte(4);
		public static readonly Gpr Bpl = Byte(5);
		public static readonly Gpr Sil = Byte(6);
		public static readonly Gpr Dil = Byte(7);
		public static readonly Gpr R8b = Byte(8);
		public static readonly Gpr R9b = Byte(9);
		public static readonly Gpr R10b = Byte(10);
		public static readonly Gpr R11b = Byte(11);
		public static readonly Gpr R12b = Byte(12);
		public static readonly Gpr R13b = Byte(13);
		public static readonly Gpr R14b = Byte(14);
		public static readonly Gpr R15b = Byte(15);

		public static readonly Gpr AH = HighByte(0);
		public static readonly Gpr CH = HighByte(1);
		public static readonly Gpr DH = HighByte(2);
		public static readonly Gpr BH = HighByte(3);

		public static readonly Gpr AX = Word(0);
		public static readonly Gpr CX = Word(1);
		public static readonly Gpr DX = Word(2);
		public static readonly Gpr BX = Word(3);
		public static readonly Gpr SP = Word(4);
		public static readonly Gpr BP = Word(5);
		public static readonly Gpr SI = Word(6);
		public static readonly Gpr DI = Word(7);
		public static readonly Gpr R8w = Word(8);
		public static readonly Gpr R9w = Word(9);
		public static readonly Gpr R10w = Word(10);
		public static readonly Gpr R11w = Word(11);
		public static readonly Gpr R12w = Word(12);
		public static readonly Gpr R13w = Word(13);
		public static readonly Gpr R14w = Word(14);
		public static readonly Gpr R15w = Word(15);

		public static readonly Gpr Eax = Dword(0);
		public static readonly Gpr Ecx = Dword(1);
		public static readonly Gpr Edx = Dword(2);
		public static readonly Gpr Ebx = Dword(3);
		public static readonly Gpr Esp = Dword(4);
		public static readonly Gpr Ebp = Dword(5);
		public static readonly Gpr Esi = Dword(6);
		public static readonly Gpr Edi = Dword(7);
		public static readonly Gpr R8d = Dword(8);
		public static readonly Gpr R9d = Dword(9);
		public static readonly Gpr R10d = Dword(10);
		public static readonly Gpr R11d = Dword(11);
		public static readonly Gpr R12d = Dword(12);
		public static readonly Gpr R13d = Dword(13);
		public static readonly Gpr R14d = Dword(14);
		public static readonly Gpr R15d = Dword(15);

		public static readonly Gpr Rax = Qword(0);
		public static readonly Gpr Rcx = Qword(1);
		public static readonly Gpr Rdx = Qword(2);
		public static readonly Gpr Rbx = Qword(3);
		public static readonly Gpr Rsp = Qword(4);
		public static readonly Gpr Rbp = Qword(5);
		public static readonly Gpr Rsi = Qword(6);
		public static readonly Gpr Rdi = Qword(7);
		public static readonly Gpr R8 = Qword(8);
		public static readonly Gpr R9 = Qword(9);
		public static readonly Gpr R10 = Qword(10);
		public static readonly Gpr R11 = Qword(11);
		public static readonly Gpr R12 = Qword(12);
		public static readonly Gpr R13 = Qword(13);
		public static readonly Gpr R14 = Qword(14);
		public static readonly Gpr R15 = Qword(15);
		#endregion
	}
}
