using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Xed
{
	/// <summary>
	/// Represents a XED operand width - a type associated with an instruction operand.
	/// In addition of encoding a base type, this can represents varying sizes based
	/// on operand/address/stack sizes and vector values.
	/// </summary>
	public readonly struct XedOperandWidth : IEquatable<XedOperandWidth>
	{
		private readonly XedXType xtype;
		private readonly ushort widthInBits_16, widthInBits_32, widthInBits_64;

		public XedOperandWidth(XedXType xtype, int widthInBits)
		{
			if (widthInBits >= 8 && widthInBits % 8 != 0)
				throw new ArgumentOutOfRangeException(nameof(widthInBits));
			this.xtype = xtype;
			widthInBits_16 = widthInBits_32 = widthInBits_64 = (ushort)widthInBits;
		}

		public XedOperandWidth(XedXType xtype, int widthInBits_16,
			int widthInBits_32, int widthInBits_64)
		{
			// Must be multiples of the XType bits per element (vector)
			if (xtype.BitsPerElement > 0)
			{
				if (widthInBits_16 % xtype.BitsPerElement != 0)
					throw new ArgumentOutOfRangeException(nameof(widthInBits_16));
				if (widthInBits_32 % xtype.BitsPerElement != 0)
					throw new ArgumentOutOfRangeException(nameof(widthInBits_32));
				if (widthInBits_64 % xtype.BitsPerElement != 0)
					throw new ArgumentOutOfRangeException(nameof(widthInBits_64));
			}

			// Bits sized are only between 1 and 7
			if (widthInBits_16 >= 8 && widthInBits_16 % 8 != 0)
				throw new ArgumentOutOfRangeException(nameof(widthInBits_16));
			if (widthInBits_32 % 8 != 0 && widthInBits_32 != widthInBits_16)
				throw new ArgumentOutOfRangeException(nameof(widthInBits_32));
			if (widthInBits_64 % 8 != 0 && widthInBits_64 != widthInBits_16)
				throw new ArgumentOutOfRangeException(nameof(widthInBits_64));
			if (widthInBits_32 < widthInBits_16 && widthInBits_32 != 0)
				throw new ArgumentOutOfRangeException(nameof(widthInBits_32));
			if ((widthInBits_64 < widthInBits_16 || widthInBits_64 < widthInBits_32) && widthInBits_64 != 0)
				throw new ArgumentOutOfRangeException(nameof(widthInBits_64));

			this.xtype = xtype;
			this.widthInBits_16 = (ushort)widthInBits_16;
			this.widthInBits_32 = (ushort)widthInBits_32;
			this.widthInBits_64 = (ushort)widthInBits_64;
		}

		public XedXType XType => xtype;
		public XedBaseType BaseType => xtype.BaseType;
		public int BitsPerElement => xtype.BitsPerElement;
		public int WidthInBits_16 => widthInBits_16;
		public int WidthInBits_32 => widthInBits_32;
		public int WidthInBits_64 => widthInBits_64;
		public int? WidthInBits => (widthInBits_16 == widthInBits_32 && widthInBits_32 == widthInBits_64)
			? widthInBits_16 : (int?)null;
		public bool IsVector => BitsPerElement > 0 && WidthInBits_16 > BitsPerElement;

		public bool Equals(XedOperandWidth other) => xtype == other.xtype
			&& widthInBits_16 == other.widthInBits_16
			&& widthInBits_32 == other.widthInBits_32
			&& widthInBits_64 == other.widthInBits_64;
		public override bool Equals(object obj) => obj is XedOperandWidth && Equals((XedOperandWidth)obj);
		public override int GetHashCode() => (xtype.GetHashCode() << 17) ^ widthInBits_16;
		public static bool Equals(XedOperandWidth lhs, XedOperandWidth rhs) => lhs.Equals(rhs);
		public static bool operator ==(XedOperandWidth lhs, XedOperandWidth rhs) => Equals(lhs, rhs);
		public static bool operator !=(XedOperandWidth lhs, XedOperandWidth rhs) => !Equals(lhs, rhs);

		public override string ToString()
		{
			if (xtype.BitsPerElement == WidthInBits) return xtype.ToString();
			return WidthInBits.HasValue
				? $"{xtype}<{WidthInBits}>"
				: $"{xtype}<{WidthInBits_16}/{WidthInBits_32}/{WidthInBits_64}>";
		}
	}
}
