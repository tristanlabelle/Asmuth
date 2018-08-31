using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Xed
{
	public readonly struct XedOperandWidth : IEquatable<XedOperandWidth>
	{
		private readonly XedXType xtype;
		private readonly ushort widthInBits16, widthInBits32, widthInBits64;

		public XedOperandWidth(XedXType xtype, int widthInBits)
		{
			if (widthInBits >= 8 && widthInBits % 8 != 0)
				throw new ArgumentOutOfRangeException(nameof(widthInBits));
			this.xtype = xtype;
			widthInBits16 = widthInBits32 = widthInBits64 = (ushort)widthInBits;
		}

		public XedOperandWidth(XedXType xtype, int widthInBits16, int widthInBits32, int widthInBits64)
		{
			if (widthInBits16 >= 8 && widthInBits16 % 8 != 0)
				throw new ArgumentOutOfRangeException(nameof(widthInBits16));
			if (widthInBits32 % 8 != 0 && widthInBits32 != widthInBits16)
				throw new ArgumentOutOfRangeException(nameof(widthInBits32));
			if (widthInBits64 % 8 != 0 && widthInBits64 != widthInBits16)
				throw new ArgumentOutOfRangeException(nameof(widthInBits64));
			if (widthInBits32 < widthInBits16 && widthInBits32 != 0)
				throw new ArgumentOutOfRangeException(nameof(widthInBits32));
			if ((widthInBits64 < widthInBits16 || widthInBits64 < widthInBits32) && widthInBits64 != 0)
				throw new ArgumentOutOfRangeException(nameof(widthInBits64));

			this.xtype = xtype;
			this.widthInBits16 = (ushort)widthInBits16;
			this.widthInBits32 = (ushort)widthInBits32;
			this.widthInBits64 = (ushort)widthInBits64;
		}

		public XedXType XType => xtype;
		public int WidthInBits16 => widthInBits16;
		public int WidthInBits32 => widthInBits32;
		public int WidthInBits64 => widthInBits64;
		public int? WidthInBits => (widthInBits16 == widthInBits32 && widthInBits32 == widthInBits64)
			? widthInBits16 : (int?)null;

		public bool Equals(XedOperandWidth other) => xtype == other.xtype
			&& widthInBits16 == other.widthInBits16
			&& widthInBits32 == other.widthInBits32
			&& widthInBits64 == other.widthInBits64;
		public override bool Equals(object obj) => obj is XedOperandWidth && Equals((XedOperandWidth)obj);
		public override int GetHashCode() => (xtype.GetHashCode() << 17) ^ widthInBits16;
		public static bool Equals(XedOperandWidth lhs, XedOperandWidth rhs) => lhs.Equals(rhs);
		public static bool operator ==(XedOperandWidth lhs, XedOperandWidth rhs) => Equals(lhs, rhs);
		public static bool operator !=(XedOperandWidth lhs, XedOperandWidth rhs) => !Equals(lhs, rhs);

		public override string ToString()
		{
			if (xtype.BitsPerElement == WidthInBits) return xtype.ToString();
			return WidthInBits.HasValue
				? $"{xtype}<{WidthInBits}>"
				: $"{xtype}<{WidthInBits16}/{WidthInBits32}/{WidthInBits64}>";
		}
	}
}
