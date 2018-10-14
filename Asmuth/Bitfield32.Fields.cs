using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth
{
	partial struct Bitfield32
	{
		public struct Bool
		{
			internal byte shift;
			public static implicit operator Bool(Builder b) => new Bool { shift = b.Alloc(1) };
		}

		public bool this[Bool field]
		{
			get => Get(1, field.shift) != 0;
			set => Set(1, field.shift, value ? 1U : 0U);
		}
		
		public struct NullableBool
		{
			internal byte shift;
			public static implicit operator NullableBool(Builder b) => new NullableBool { shift = b.Alloc(2) };
		}

		public bool? this[NullableBool field]
		{
			get
			{
				var value = Get(2, field.shift);
				return value == 0 ? (bool?)null : value > 1;
			}
			set => Set(2, field.shift, value.HasValue ? (value.Value ? 2U : 1U) : 0U);
		}

		public struct UInt2
		{
			internal byte shift;
			public static implicit operator UInt2(Builder b) => new UInt2 { shift = b.Alloc(2) };
		}

		public byte this[UInt2 field]
		{
			get => (byte)Get(2, field.shift);
			set => Set(2, field.shift, value);
		}

		public struct NullableUIntMaxValue2
		{
			internal byte shift;
			public static implicit operator NullableUIntMaxValue2(Builder b) => new NullableUIntMaxValue2 { shift = b.Alloc(2) };
		}

		public byte? this[NullableUIntMaxValue2 field]
		{
			get => GetAsNullableByte(2, field.shift);
			set => SetAsNullableByte(2, field.shift, value);
		}
		
		public struct UInt3
		{
			internal byte shift;
			public static implicit operator UInt3(Builder b) => new UInt3 { shift = b.Alloc(3) };
		}

		public byte this[UInt3 field]
		{
			get => (byte)Get(3, field.shift);
			set => Set(3, field.shift, value);
		}

		public struct NullableUIntMaxValue6
		{
			internal byte shift;
			public static implicit operator NullableUIntMaxValue6(Builder b) => new NullableUIntMaxValue6 { shift = b.Alloc(3) };
		}

		public byte? this[NullableUIntMaxValue6 field]
		{
			get => GetAsNullableByte(3, field.shift);
			set => SetAsNullableByte(3, field.shift, value);
		}
		
		public struct UInt4
		{
			internal byte shift;
			public static implicit operator UInt4(Builder b) => new UInt4 { shift = b.Alloc(4) };
		}

		public byte this[UInt4 field]
		{
			get => (byte)Get(4, field.shift);
			set => Set(4, field.shift, value);
		}
		
		public struct Byte
		{
			internal byte shift;
			public static implicit operator Byte(Builder b) => new Byte { shift = b.Alloc(8) };
		}

		public byte this[Byte field]
		{
			get => (byte)Get(8, field.shift);
			set => Set(8, field.shift, value);
		}
	}
}
