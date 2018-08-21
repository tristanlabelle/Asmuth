using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Asmuth.X86
{
	[StructLayout(LayoutKind.Sequential, Size = sizeof(byte))]
	public readonly struct Rex : IEquatable<Rex>
	{
		public const byte ReservedValue = 0x40;
		public const byte ReservedMask = 0xF0;
		public const byte FlagsMask = 0x0F;
		public const byte BBit = 0b0001;
		public const byte XBit = 0b0010;
		public const byte RBit = 0b0100;
		public const byte WBit = 0b1000;

		public byte LowNibble { get; }

		public Rex(byte value)
		{
			if (!Test(value)) throw new ArgumentException();
			LowNibble = (byte)(value & FlagsMask);
		}

		public Rex(bool modRegExt, bool baseRegExt, bool indexRegExt, bool op64)
		{
			LowNibble = 0;
			if (modRegExt) LowNibble |= RBit;
			if (baseRegExt) LowNibble |= BBit;
			if (indexRegExt) LowNibble |= XBit;
			if (op64) LowNibble |= WBit;
		}
		
		public byte Value => (byte)(ReservedValue | LowNibble);
		public bool B => (LowNibble & BBit) != 0;
		public bool X => (LowNibble & XBit) != 0;
		public bool R => (LowNibble & RBit) != 0;
		public bool W => (LowNibble & WBit) != 0;

		public bool BaseRegExtension => B;
		public bool IndexRegExtension => X;
		public bool ModRegExtension => R;
		public bool OperandSize64 => W;

		public bool Equals(Rex other) => LowNibble == other.LowNibble;
		public override bool Equals(object obj) => obj is Rex && Equals((Rex)obj);
		public override int GetHashCode() => LowNibble;
		public static bool Equals(Rex lhs, Rex rhs) => lhs.Equals(rhs);
		public static bool operator ==(Rex lhs, Rex rhs) => Equals(lhs, rhs);
		public static bool operator !=(Rex lhs, Rex rhs) => !Equals(lhs, rhs);

		public string ToIntelStyleString()
		{
			var stringBuilder = new StringBuilder(8);
			stringBuilder.Append("rex");
			if (LowNibble != 0)
			{
				stringBuilder.Append('.');
				for (int i = 3; i >= 0; i--)
					if ((LowNibble & (1 << i)) != 0)
						stringBuilder.Append("bxrw"[i]);
			}
			return stringBuilder.ToString();
		}

		public override string ToString() => ToIntelStyleString();

		public static bool Test(byte value) => (value & ReservedMask) == ReservedValue;

		public static implicit operator byte(Rex rex) => rex.Value;
		public static implicit operator Rex(byte value) => new Rex(value);
	}
}
