using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Asmuth.X86
{
	[StructLayout(LayoutKind.Sequential, Size = sizeof(byte))]
	public readonly struct IS4 : IEquatable<IS4>
	{
		public byte Byte { get; }

		public IS4(byte @byte) => Byte = @byte;

		public byte SourceReg => (byte)(Byte >> 4);
		public byte Payload => (byte)(Byte & 0xF);
		
		public bool Equals(IS4 other) => Byte == other.Byte;
		public override bool Equals(object obj) => obj is IS4 && Equals((IS4)obj);
		public override int GetHashCode() => Byte;
		public static bool Equals(IS4 lhs, IS4 rhs) => lhs.Equals(rhs);
		public static bool operator ==(IS4 lhs, IS4 rhs) => Equals(lhs, rhs);
		public static bool operator !=(IS4 lhs, IS4 rhs) => !Equals(lhs, rhs);
		
		public static implicit operator byte(IS4 rex) => rex.Byte;
		public static implicit operator IS4(byte @byte) => new IS4(@byte);
	}
}
