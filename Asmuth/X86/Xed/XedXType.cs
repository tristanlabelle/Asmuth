using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Xed
{
	public readonly struct XedXType : IEquatable<XedXType>
	{
		public XedBaseType BaseType { get; }
		private readonly ushort bitsPerElement;

		public XedXType(XedBaseType baseType, int bitsPerElement)
		{
			this.BaseType = baseType;
			this.bitsPerElement = (ushort)bitsPerElement;
		}

		public bool IsSized => bitsPerElement > 0;
		public bool IsUnsized => bitsPerElement == 0;
		public int BitsPerElement => bitsPerElement;

		public bool Equals(XedXType other) => BaseType == other.BaseType && bitsPerElement == other.bitsPerElement;
		public override bool Equals(object obj) => obj is XedXType && Equals((XedXType)obj);
		public override int GetHashCode() => ((int)BaseType << 16) | bitsPerElement;
		public static bool Equals(XedXType lhs, XedXType rhs) => lhs.Equals(rhs);
		public static bool operator ==(XedXType lhs, XedXType rhs) => Equals(lhs, rhs);
		public static bool operator !=(XedXType lhs, XedXType rhs) => !Equals(lhs, rhs);

		public override string ToString() => IsSized ? $"{BaseType}:{bitsPerElement}" : BaseType.ToString();
	}
}
