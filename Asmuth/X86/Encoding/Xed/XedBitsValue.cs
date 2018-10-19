using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Encoding.Xed
{
	public readonly struct XedBitsValue : IEquatable<XedBitsValue>
	{
		public ulong Bits { get; }
		public int Length { get; }

		public XedBitsValue(ulong bits, int length)
		{
			if ((uint)length > 64) throw new NotImplementedException();
			this.Bits = bits & GetMask(length);
			this.Length = length;
		}

		public ulong Mask => GetMask(Length);

		public bool Equals(XedBitsValue other) => Bits == other.Bits && Length == other.Length;
		public override bool Equals(object obj) => obj is XedBitsValue && Equals((XedBitsValue)obj);
		public override int GetHashCode() => Bits.GetHashCode() ^ (Length << 25);
		public static bool Equals(XedBitsValue lhs, XedBitsValue rhs) => lhs.Equals(rhs);
		public static bool operator ==(XedBitsValue lhs, XedBitsValue rhs) => Equals(lhs, rhs);
		public static bool operator !=(XedBitsValue lhs, XedBitsValue rhs) => !Equals(lhs, rhs);

		private static ulong GetMask(int length) => length == 64 ? ulong.MaxValue : (1U << length) - 1;
	}
}
