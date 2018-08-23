using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Asmuth.X86
{
	[StructLayout(LayoutKind.Sequential, Size = sizeof(byte))]
	public readonly struct Rex : IEquatable<Rex>
	{
		public struct Builder
		{
			public bool ModRegExtension;
			public bool BaseRegExtension;
			public bool IndexRegExtension;
			public bool OperandSize64;

			public Rex Build() => new Rex(this);

			public static implicit operator Rex(Builder builder) => builder.Build();
		}

		public const byte ReservedValue = 0x40;
		public const byte ReservedMask = 0xF0;
		public const byte FlagsMask = 0x0F;
		private const byte BaseRegExtensionBit = 0b0001;
		private const byte IndexRegExtensionBit = 0b0010;
		private const byte ModRegExtensionBit = 0b0100;
		private const byte OperandSize64Bit = 0b1000;

		public byte LowNibble { get; }

		public Rex(byte @byte)
		{
			if (!Test(@byte)) throw new ArgumentException();
			LowNibble = (byte)(@byte & FlagsMask);
		}

		private Rex(Builder builder)
		{
			LowNibble = 0;
			if (builder.ModRegExtension) LowNibble |= ModRegExtensionBit;
			if (builder.BaseRegExtension) LowNibble |= BaseRegExtensionBit;
			if (builder.IndexRegExtension) LowNibble |= IndexRegExtensionBit;
			if (builder.OperandSize64) LowNibble |= OperandSize64Bit;
		}
		
		public byte Byte => (byte)(ReservedValue | LowNibble);
		public bool BaseRegExtension => (LowNibble & BaseRegExtensionBit) != 0;
		public bool IndexRegExtension => (LowNibble & IndexRegExtensionBit) != 0;
		public bool ModRegExtension => (LowNibble & ModRegExtensionBit) != 0;
		public bool OperandSize64 => (LowNibble & OperandSize64Bit) != 0;

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

		public static implicit operator byte(Rex rex) => rex.Byte;
		public static explicit operator Rex(byte @byte) => new Rex(@byte);
	}
}
