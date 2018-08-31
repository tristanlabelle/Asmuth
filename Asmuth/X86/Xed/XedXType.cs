using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Xed
{
	public readonly struct XedXType : IEquatable<XedXType>
	{
		private readonly XedType type;
		private readonly ushort bitsPerElement;

		public XedXType(XedType type, int bitsPerElement)
		{
			this.type = type;
			this.bitsPerElement = (ushort)bitsPerElement;
		}

		public XedType Type => type;
		public int BitsPerElement => bitsPerElement;

		public bool Equals(XedXType other) => type == other.type && bitsPerElement == other.bitsPerElement;
		public override bool Equals(object obj) => obj is XedXType && Equals((XedXType)obj);
		public override int GetHashCode() => ((int)type << 16) | bitsPerElement;
		public static bool Equals(XedXType lhs, XedXType rhs) => lhs.Equals(rhs);
		public static bool operator ==(XedXType lhs, XedXType rhs) => Equals(lhs, rhs);
		public static bool operator !=(XedXType lhs, XedXType rhs) => !Equals(lhs, rhs);

		public override string ToString() => $"{type}:{bitsPerElement}";
	}
}
