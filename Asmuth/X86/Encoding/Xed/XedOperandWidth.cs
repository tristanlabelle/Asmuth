using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Encoding.Xed
{
	/// <summary>
	/// Represents a XED operand width - a type associated with an instruction operand.
	/// In addition of encoding a base type, this can represents varying sizes based
	/// on operand/address/stack sizes and vector values.
	/// </summary>
	public readonly struct XedOperandWidth : IEquatable<XedOperandWidth>
	{
		public XedXType XType { get; }
		private readonly ushort inBits_16, inBits_32, inBits_64;

		public XedOperandWidth(XedXType xtype, int inBits)
		{
			if (inBits >= 8 && inBits % 8 != 0)
				throw new ArgumentOutOfRangeException(nameof(inBits));
			if (xtype.IsUnsized) xtype = new XedXType(xtype.BaseType, inBits);
			
			this.XType = xtype;
			inBits_16 = inBits_32 = inBits_64 = (ushort)inBits;
		}

		public XedOperandWidth(XedXType xtype, int inBits_16, int inBits_32, int inBits_64)
		{
			// Must be multiples of the XType bits per element (vector)
			if (xtype.BitsPerElement > 0)
			{
				if (inBits_16 % xtype.BitsPerElement != 0)
					throw new ArgumentOutOfRangeException(nameof(inBits_16));
				if (inBits_32 % xtype.BitsPerElement != 0)
					throw new ArgumentOutOfRangeException(nameof(inBits_32));
				if (inBits_64 % xtype.BitsPerElement != 0)
					throw new ArgumentOutOfRangeException(nameof(inBits_64));
			}

			// Bits sized are only between 1 and 7
			if (inBits_16 >= 8 && inBits_16 % 8 != 0)
				throw new ArgumentOutOfRangeException(nameof(inBits_16));
			if (inBits_32 % 8 != 0 && inBits_32 != inBits_16)
				throw new ArgumentOutOfRangeException(nameof(inBits_32));
			if (inBits_64 % 8 != 0 && inBits_64 != inBits_16)
				throw new ArgumentOutOfRangeException(nameof(inBits_64));
			if (inBits_32 < inBits_16 && inBits_32 != 0)
				throw new ArgumentOutOfRangeException(nameof(inBits_32));
			if ((inBits_64 < inBits_16 || inBits_64 < inBits_32) && inBits_64 != 0)
				throw new ArgumentOutOfRangeException(nameof(inBits_64));

			this.XType = xtype;
			this.inBits_16 = (ushort)inBits_16;
			this.inBits_32 = (ushort)inBits_32;
			this.inBits_64 = (ushort)inBits_64;
		}
		
		public XedBaseType BaseType => XType.BaseType;
		public int BitsPerElement => XType.BitsPerElement;
		public int InBits_16 => inBits_16;
		public int InBits_32 => inBits_32;
		public int InBits_64 => inBits_64;
		public int? InBits => (inBits_16 == inBits_32 && inBits_32 == inBits_64)
			? inBits_16 : (int?)null;
		public bool IsVector => BitsPerElement > 0 && InBits_16 > BitsPerElement;

		public bool IsInBits(int value16, int value32, int value64)
			=> InBits_16 == value16 && inBits_32 == value32 && inBits_64 == value64;

		public bool IsInBytes(int value16, int value32, int value64)
			=> IsInBits(value16 * 8, value32 * 8, value64 * 8);

		public XedOperandWidth WithXType(XedXType xtype)
			=> new XedOperandWidth(xtype, inBits_16, inBits_32, inBits_64);

		public bool Equals(XedOperandWidth other) => XType == other.XType
			&& inBits_16 == other.inBits_16
			&& inBits_32 == other.inBits_32
			&& inBits_64 == other.inBits_64;
		public override bool Equals(object obj) => obj is XedOperandWidth && Equals((XedOperandWidth)obj);
		public override int GetHashCode() => (XType.GetHashCode() << 17) ^ inBits_16;
		public static bool Equals(XedOperandWidth lhs, XedOperandWidth rhs) => lhs.Equals(rhs);
		public static bool operator ==(XedOperandWidth lhs, XedOperandWidth rhs) => Equals(lhs, rhs);
		public static bool operator !=(XedOperandWidth lhs, XedOperandWidth rhs) => !Equals(lhs, rhs);

		public override string ToString()
		{
			var str = new StringBuilder();
			str.Append(BaseType.ToString());
			str.Append(':');
			if (BitsPerElement == 0)
			{
				str.Append(InBits_16);
				if (InBits_64 != InBits_16)
				{
					str.Append('/');
					str.Append(InBits_32);
					str.Append('/');
					str.Append(InBits_64);
				}
			}
			else
			{
				str.Append(BitsPerElement);
				if (InBits_64 > BitsPerElement)
				{
					str.Append('x');
					str.Append(InBits_16 / BitsPerElement);
					if (InBits_64 != InBits_16)
					{
						str.Append('/');
						str.Append(InBits_32 / BitsPerElement);
						str.Append('/');
						str.Append(InBits_64 / BitsPerElement);
					}
				}
			}

			return str.ToString();
		}
	}
}
