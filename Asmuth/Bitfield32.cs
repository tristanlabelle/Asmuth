using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth
{
	public partial struct Bitfield32
	{
		public sealed class Builder
		{
			private byte shift;
			
			internal byte Alloc(byte width)
			{
				if (shift + width > 32) throw new InvalidOperationException();
				var fieldShift = shift;
				shift += width;
				return fieldShift;
			}
		}


		public uint RawValue { get; set; }
		
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
