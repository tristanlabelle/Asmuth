using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	public enum GprPart : byte
	{
		Byte,
		Word,
		Dword,
		Qword,

		HighByte
	}

	public static class GprPartEnum
	{
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
		public static readonly Gpr SPL = new Gpr(4, GprPart.Byte);
		public static readonly Gpr BPL = new Gpr(5, GprPart.Byte);
		public static readonly Gpr SIL = new Gpr(6, GprPart.Byte);
		public static readonly Gpr DIL = new Gpr(7, GprPart.Byte);

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

		public static readonly Gpr EAX = new Gpr(0, GprPart.Dword);
		public static readonly Gpr ECX = new Gpr(1, GprPart.Dword);
		public static readonly Gpr EDX = new Gpr(2, GprPart.Dword);
		public static readonly Gpr EBX = new Gpr(3, GprPart.Dword);
		public static readonly Gpr ESP = new Gpr(4, GprPart.Dword);
		public static readonly Gpr EBP = new Gpr(5, GprPart.Dword);
		public static readonly Gpr ESI = new Gpr(6, GprPart.Dword);
		public static readonly Gpr EDI = new Gpr(7, GprPart.Dword);

		public static readonly Gpr RAX = new Gpr(0, GprPart.Qword);
		public static readonly Gpr RCX = new Gpr(1, GprPart.Qword);
		public static readonly Gpr RDX = new Gpr(2, GprPart.Qword);
		public static readonly Gpr RBX = new Gpr(3, GprPart.Qword);
		public static readonly Gpr RSP = new Gpr(4, GprPart.Qword);
		public static readonly Gpr RBP = new Gpr(5, GprPart.Qword);
		public static readonly Gpr RSI = new Gpr(6, GprPart.Qword);
		public static readonly Gpr RDI = new Gpr(7, GprPart.Qword);
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

		public int Index => value & 0xF;
		public GprPart Part => (GprPart)(value >> 4);
		public int SizeInBytes => Part.GetSizeInBytes();
		public bool RequiresRex => Index >= 8 || (Part == GprPart.Byte && Index >= 4);
		public bool RequiresRexBit => Index >= 8;
		public bool PreventsRex => Part == GprPart.HighByte;
		public int EncodedID => Part == GprPart.HighByte ? (4 + Index) : (Index & 7);
		public int Encoded3BitID => Part == GprPart.HighByte ? (4 + Index) : (Index & 7);

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

		public override bool Equals(object obj) => obj is Gpr && Equals((Gpr)obj);
		public bool Equals(Gpr other) => value == other.value;
		public override int GetHashCode() => value;
		public override string ToString() => Name;

		public static bool Equals(Gpr first, Gpr second) => first.Equals(second);
		public static bool operator ==(Gpr lhs, Gpr rhs) => Equals(lhs, rhs);
		public static bool operator !=(Gpr lhs, Gpr rhs) => Equals(lhs, rhs);

		public static Gpr FromEncodedID(int id, int operandSize, bool hasRex)
		{
			Contract.Requires(id >= 0 && id < (hasRex ? 16 : 8));

			if (operandSize == 1)
			{
				if (!hasRex && id >= 4 && id < 8) return HighByte(id - 4);
				return Byte(id);
			}
			else if (operandSize == 2) return Word(id);
			else if (operandSize == 4) return Dword(id);
			else if (operandSize == 8) return Qword(id);
			else throw new ArgumentOutOfRangeException(nameof(operandSize));
		}

		public static Gpr Byte(int index) => new Gpr(index, GprPart.Byte);
		public static Gpr HighByte(int index) => new Gpr(index, GprPart.HighByte);
		public static Gpr Word(int index) => new Gpr(index, GprPart.Word);
		public static Gpr Dword(int index) => new Gpr(index, GprPart.Dword);
		public static Gpr Qword(int index) => new Gpr(index, GprPart.Qword);

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
