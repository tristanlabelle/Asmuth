using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth
{
	public partial struct Bitfield32 : IEquatable<Bitfield32>
	{
		public sealed class Builder
		{
			private byte shift;

			public Builder Clone() => new Builder { shift = shift };
			
			internal byte Alloc(byte width)
			{
				if (shift + width > 32) throw new InvalidOperationException();
				var fieldShift = shift;
				shift += width;
				return fieldShift;
			}
		}
		
		public uint RawValue { get; set; }

		public bool Equals(Bitfield32 other) => RawValue == other.RawValue;
		public override bool Equals(object obj) => obj is Bitfield32 && Equals((Bitfield32)obj);
		public override int GetHashCode() => (int)RawValue;
		public static bool Equals(Bitfield32 lhs, Bitfield32 rhs) => lhs.Equals(rhs);
		public static bool operator ==(Bitfield32 lhs, Bitfield32 rhs) => Equals(lhs, rhs);
		public static bool operator !=(Bitfield32 lhs, Bitfield32 rhs) => !Equals(lhs, rhs);

		private uint Get(byte width, byte shift) => (RawValue >> shift) & GetUnshiftedMask(width);
		private uint GetUnshiftedMask(byte width) => (1U << width) - 1;
		private uint GetShiftedMask(byte width, byte shift) => GetUnshiftedMask(width) << shift;
		private void Clear(byte width, byte shift) => RawValue &= ~GetShiftedMask(width, shift);
		private void Or(byte width, byte shift, uint value) => RawValue |= (value & GetUnshiftedMask(width)) << shift;
		private void Set(byte width, byte shift, uint value)
		{
			Clear(width, shift);
			Or(width, shift, value);
		}

		private byte? GetAsNullableByte(byte width, byte shift)
		{
			var value = Get(width, shift);
			return value == 0 ? (byte?)null : (byte)(value - 1);
		}

		private void SetAsNullableByte(byte width, byte shift, byte? value)
			=> Set(width, shift, value.HasValue ? ((uint)value.Value + 1) : 0);

		public static Builder Build() => new Builder();
	}
}
