using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
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

	public enum GprPart : byte
	{
		Byte,
		Word,
		Dword,
		Qword,

		HighByte
	}

	public static class GprEnums
	{
		[Pure]
		public static bool RequiresRexBit(this GprCode code)
			=> code >= GprCode.R8;

		[Pure]
		public static byte GetLow3Bits(this GprCode code)
			=> (byte)((int)code & 0x7);

		[Pure]
		public static OperandSize GetSize(this GprPart part)
			=> part == GprPart.HighByte ? OperandSize.Byte : (OperandSize)part;

		[Pure]
		public static int GetSizeInBytes(this GprPart part)
			=> part == GprPart.HighByte ? 1 : (1 << (int)part);

		[Pure]
		public static int GetOffsetInBytes(this GprPart part)
			=> part == GprPart.HighByte ? 1 : 0;
	}
	
	public struct Gpr : IEquatable<Gpr>
	{
		#region Static instances
		public static readonly Gpr AL = new Gpr(0, GprPart.Byte);
		public static readonly Gpr CL = new Gpr(1, GprPart.Byte);
		public static readonly Gpr DL = new Gpr(2, GprPart.Byte);
		public static readonly Gpr BL = new Gpr(3, GprPart.Byte);
		public static readonly Gpr Spl = new Gpr(4, GprPart.Byte);
		public static readonly Gpr Bpl = new Gpr(5, GprPart.Byte);
		public static readonly Gpr Sil = new Gpr(6, GprPart.Byte);
		public static readonly Gpr Dil = new Gpr(7, GprPart.Byte);

		public static readonly Gpr AH = new Gpr(0, GprPart.HighByte);
		public static readonly Gpr CH = new Gpr(1, GprPart.HighByte);
		public static readonly Gpr DH = new Gpr(2, GprPart.HighByte);
		public static readonly Gpr BH = new Gpr(3, GprPart.HighByte);

		public static readonly Gpr AX = new Gpr(0, GprPart.Word);
		public static readonly Gpr CX = new Gpr(1, GprPart.Word);
		public static readonly Gpr DX = new Gpr(2, GprPart.Word);
		public static readonly Gpr BX = new Gpr(3, GprPart.Word);
		public static readonly Gpr SP = new Gpr(4, GprPart.Word);
		public static readonly Gpr BP = new Gpr(5, GprPart.Word);
		public static readonly Gpr SI = new Gpr(6, GprPart.Word);
		public static readonly Gpr DI = new Gpr(7, GprPart.Word);

		public static readonly Gpr Eax = new Gpr(0, GprPart.Dword);
		public static readonly Gpr Ecx = new Gpr(1, GprPart.Dword);
		public static readonly Gpr Edx = new Gpr(2, GprPart.Dword);
		public static readonly Gpr Ebx = new Gpr(3, GprPart.Dword);
		public static readonly Gpr Esp = new Gpr(4, GprPart.Dword);
		public static readonly Gpr Ebp = new Gpr(5, GprPart.Dword);
		public static readonly Gpr Esi = new Gpr(6, GprPart.Dword);
		public static readonly Gpr Edi = new Gpr(7, GprPart.Dword);

		public static readonly Gpr Rax = new Gpr(0, GprPart.Qword);
		public static readonly Gpr Rcx = new Gpr(1, GprPart.Qword);
		public static readonly Gpr Rdx = new Gpr(2, GprPart.Qword);
		public static readonly Gpr Rbx = new Gpr(3, GprPart.Qword);
		public static readonly Gpr Rsp = new Gpr(4, GprPart.Qword);
		public static readonly Gpr Rbp = new Gpr(5, GprPart.Qword);
		public static readonly Gpr Rsi = new Gpr(6, GprPart.Qword);
		public static readonly Gpr Rdi = new Gpr(7, GprPart.Qword);
		#endregion

		// Low nibble: register index
		// High nibble: part
		private readonly byte value;

		public Gpr(int index, GprPart part)
		{
			Contract.Requires(index >= 0 && index < 0x10);
			Contract.Requires(part != GprPart.HighByte || index < 4);
			value = unchecked((byte)(index | ((int)part << 4)));
		}

		public Gpr(GprCode code, GprPart part) : this((int)code, part) { }

		public int Index => value & 0xF;
		public GprPart Part => (GprPart)(value >> 4);
		public OperandSize Size => Part.GetSize();
		public int SizeInBytes => Part.GetSizeInBytes();
		public bool RequiresRex => Index >= 8 || (Part == GprPart.Byte && Index >= 4);
		public bool RequiresRexBit => Index >= 8;
		public bool PreventsRex => Part == GprPart.HighByte;
		public GprCode Code => Part == GprPart.HighByte ? (GprCode)(4 + Index) : (GprCode)(Index & 7);

		public string Name
		{
			get
			{
				var index = Index;
				var part = Part;

				string name = GetBaseName(index);
				if (index < 8)
				{
					if (part == GprPart.Byte) name += "L";
					else if (part == GprPart.HighByte) name += "H";
					else
					{
						if (index < 4) name += "X";
						if (part == GprPart.Dword) name = "E" + name;
						else if (part == GprPart.Qword) name = "R" + name;
					}
				}
				else
				{
					if (part == GprPart.Byte) name += "B";
					else if (part == GprPart.Word) name += "W";
					else if (part == GprPart.Dword) name += "D";
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

		public static string GetName(GprCode code, GprPart part)
			=> new Gpr(code, part).Name;

		public static Gpr FromCode(GprCode code, OperandSize size, bool hasRex)
		{
			if (size == OperandSize.Byte)
			{
				if (!hasRex && code >= (GprCode)4 && code < (GprCode)8)
					return HighByte((int)code - 4);
				return Byte((int)code);
			}
			else if (size == OperandSize.Word) return Word(code);
			else if (size == OperandSize.Dword) return Dword(code);
			else if (size == OperandSize.Qword) return Qword(code);
			else throw new ArgumentOutOfRangeException(nameof(size));
		}

		public static Gpr Byte(int index) => new Gpr(index, GprPart.Byte);
		public static Gpr HighByte(int index) => new Gpr(index, GprPart.HighByte);
		public static Gpr Word(int index) => new Gpr(index, GprPart.Word);
		public static Gpr Dword(int index) => new Gpr(index, GprPart.Dword);
		public static Gpr Qword(int index) => new Gpr(index, GprPart.Qword);
		public static Gpr Word(GprCode code) => Word((int)code);
		public static Gpr Dword(GprCode code) => Dword((int)code);
		public static Gpr Qword(GprCode code) => Qword((int)code);

		#region Static parsing
		public static string GetBaseName(int index)
		{
			Contract.Requires(index >= 0 && index < 0x10);
			if (index < 4) return "ACDB"[index].ToString();
			if (index >= 10) return "R1" + "012345"[index - 10];
			if (index == 4) return "SP";
			if (index == 5) return "BP";
			if (index == 6) return "SI";
			if (index == 7) return "DI";
			if (index == 8) return "R8";
			if (index == 9) return "R9";
			throw new UnreachableException();
		}

		public static string GetName(int index, GprPart part)
		{
			return new Gpr(index, part).Name;
		}

		public static GprPart? TryParseRSuffix(char c)
		{
			switch (c)
			{
				case 'D': case 'd': return GprPart.Dword;
				case 'W': case 'w': return GprPart.Word;
				case 'B': case 'b': return GprPart.Byte;
				default: return null;
			}
		}

		public static Gpr? TryParse(string str)
		{
			Contract.Requires(str != null);

			if (str.Length < 2 || str.Length > 4) return null;

			str = str.ToUpperInvariant();

			// R8 to R15
			var rMatch = Regex.Match(str, @"\AR([89]|1[0-5])([BWD])?\Z", RegexOptions.CultureInvariant);
			if (rMatch.Success)
			{
				int index = int.Parse(rMatch.Groups[1].Value, CultureInfo.InvariantCulture);
				GprPart part = GprPart.Qword;
				if (rMatch.Groups[2].Success) part = TryParseRSuffix(rMatch.Groups[2].Value[0]).Value;
				return new Gpr(index, part);
			}

			if (str.Length == 2)
			{
				if (str[1] == 'I' || str[1] == 'i')
				{
					// [SD]I
					if (str[0] == 'S' || str[0] == 's') return SI;
					if (str[0] == 'D' || str[0] == 'd') return DI;
					return null;
				}

				if (str[1] == 'P' || str[1] == 'p')
				{
					// [SD]I
					if (str[0] == 'S' || str[0] == 's') return SP;
					if (str[0] == 'B' || str[0] == 'b') return BP;
					return null;
				}

				// [ABCD][XLH]
				var index = "ACDB".IndexOf(str[0]);
				if (index < 0)
				{
					index = "acdb".IndexOf(str[0]);
					if (index < 0) return null;
				}

				GprPart part;
				if (str[1] == 'X' || str[1] == 'x') part = GprPart.Word;
				else if (str[1] == 'L' || str[1] == 'l') part = GprPart.Byte;
				else if (str[1] == 'H' || str[1] == 'h') part = GprPart.HighByte;
				else return null;

				return new Gpr(index, part);
			}
			else if (str.Length == 3)
			{
				// [ER] + 16-bit register
				if (str[0] == 'E' || str[0] == 'R')
				{
					var baseRegister = TryParse(str.Substring(1));
					if (!baseRegister.HasValue || baseRegister.Value.Part != GprPart.Word)
						return null;
					return new Gpr(baseRegister.Value.Index, str[0] == 'E' ? GprPart.Dword : GprPart.Qword);
				}

				// (SP|BP|SI|DI)L
				if (str[2] == 'L')
				{
					var baseRegister = TryParse(str.Substring(0, 2));
					if (!baseRegister.HasValue || baseRegister.Value.Index < 4 || baseRegister.Value.Index >= 8) return null;
					return new Gpr(baseRegister.Value.Index, GprPart.Byte);
				}
			}

			return null;
		} 
		#endregion
	}
}
