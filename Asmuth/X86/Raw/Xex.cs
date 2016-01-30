using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	// XEX is a made-up name for the "main prefixes",
	// which sit between the legacy prefixes and the main opcode byte.
	// This includes escape bytes, REX + escape bytes, VEX, XOP and EVEX

	public enum XexForm : byte
	{
		Legacy = 0,
		LegacyWithRex = (byte)Raw.Rex.HighNibble,
		Vex2 = (byte)Raw.Vex2.FirstByte,
		Vex3 = (byte)Raw.Vex3.FirstByte,
		Xop = (byte)Raw.Xop.FirstByte,
		EVex = (byte)Raw.EVex.FirstByte,
	}

	[Flags]
	public enum Rex : byte
	{
		Default = Reserved_Value,

		ByteCount = 1,
		HighNibble = 0x40,

		Reserved_Mask = 0xF0,
		Reserved_Value = 0x40,

		B = 1 << 0,
		X = 1 << 1,
		R = 1 << 2,
		W = 1 << 3
	}

	[Flags]
	public enum Vex2 : ushort
	{
		ByteCount = 2,
		FirstByte = 0xC5,

		Reserved_Mask = 0xFF00,
		Reserved_Value = 0xC700,

		// Compressed SIMD prefix
		pp_Shift = 0,
		pp_None = 0 << pp_Shift,
		pp_66 = 1 << pp_Shift,
		pp_F2 = 2 << pp_Shift,
		pp_F3 = 3 << pp_Shift,
		pp_Mask = 3 << pp_Shift,

		L = 1 << 2,

		// NDS Register specifier
		vvvv_Shift = 3,
		vvvv_Mask = 15 << vvvv_Shift,

		R = 1 << 7,
	}

	[Flags]
	public enum Vex3 : uint
	{
		ByteCount = 3,
		FirstByte = 0xC4,

		Reserved_Mask = 0xFF0000,
		Reserved_Value = 0xC40000,

		// Compressed SIMD prefix
		pp_Shift = 0,
		pp_None = 0U << (int)pp_Shift,
		pp_66 = 1U << (int)pp_Shift,
		pp_F2 = 2U << (int)pp_Shift,
		pp_F3 = 3U << (int)pp_Shift,
		pp_Mask = 3U << (int)pp_Shift,

		L = 1 << 2,

		// NDS Register specifier
		vvvv_Shift = 3,
		vvvv_Mask = 15U << (int)vvvv_Shift,

		W = 1 << 7,

		// Opcode map
		mmmmm_Shift = 8,
		mmmmm_0F = 1 << (int)mmmmm_Shift,
		mmmmm_0F38 = 2 << (int)mmmmm_Shift,
		mmmmm_0F3A = 3 << (int)mmmmm_Shift,
		mmmmm_Mask = 0x1FU << (int)mmmmm_Shift,

		B = 1 << 13,
		X = 1 << 14,
		R = 1 << 15,
	}

	[Flags]
	public enum Xop : uint
	{
		ByteCount = 3,
		FirstByte = 0x8F,

		Reserved_Mask = 0xFF0400,
		Reserved_Value = 0x8F0400,

		// Compressed SIMD prefix
		pp_Shift = 0,
		pp_None = 0U << (int)pp_Shift,
		pp_66 = 1U << (int)pp_Shift,
		pp_F2 = 2U << (int)pp_Shift,
		pp_F3 = 3U << (int)pp_Shift,
		pp_Mask = 3U << (int)pp_Shift,

		L = 1 << 2,

		// NDS Register specifier, in one's complement
		vvvv_Shift = 3,
		vvvv_Mask = 15U << (int)vvvv_Shift,

		W = 1 << 7,

		// Opcode map. Careful, this is not the same as in Vex3.
		mmmmm_Shift = 8,
		mmmmm_Map8 = 8 << (int)mmmmm_Shift,
		mmmmm_Map9 = 9 << (int)mmmmm_Shift,
		mmmmm_Map10 = 10 << (int)mmmmm_Shift,
		mmmmm_Mask = 0x1FU << (int)mmmmm_Shift,

		B = 1 << 13,
		X = 1 << 14,
		R = 1 << 15,
	}

	[Flags]
	public enum EVex : uint
	{
		ByteCount = 4,
		FirstByte = 0x62,

		Reserved_Mask = (0xFFU << 24) | (3 << 2) | (1 << 10),
		Reserved_Value = (0x62U << 24) | (1 << 10),

		// Compressed legacy escape
		mm_Shift = 0,
		mm_None = 0U << (int)mm_Shift,
		mm_0F = 1U << (int)mm_Shift,
		mm_0F3B = 2U << (int)mm_Shift,
		mm_0F3A = 3U << (int)mm_Shift,
		mm_Mask = 3U << (int)mm_Shift,

		R2 = 1 << 4,
		B = 1 << 5,
		X = 1 << 6,
		R = 1 << 7,

		// Compressed legacy prefix
		pp_Shift = 8,
		pp_None = 0U << (int)pp_Shift,
		pp_66 = 1U << (int)pp_Shift,
		pp_F2 = 2U << (int)pp_Shift,
		pp_F3 = 3U << (int)pp_Shift,
		pp_Mask = 3U << (int)pp_Shift,

		// NDS Register specifier
		vvvv_Shift = 11,
		vvvv_Mask = 15U << (int)vvvv_Shift,

		W = 1 << 15,
		V2 = 1 << 19,
		b = 1 << 20,
		L = 1 << 21,
		L2 = 1 << 22,
		z = 1 << 23
	}

	[Flags]
	public enum XexFields : ushort
	{
		None = 0,
		B = 1 << 0,
		X = 1 << 1,
		R = 1 << 2,
		W = 1 << 3,
		pp = 1 << 4,
		L = 1 << 5,
		vvvv = 1 << 6,
		mmmmm = 3 << 7,	// mmmmm implies mm
		mm = 2 << 7,
		mmmmmxop = 1 << 9,	 // XOP version of mmmmm
		R2 = 1 << 10,
		V2 = 1 << 11,
		b = 1 << 12,
		L2 = 1 << 13,
		z = 1 << 14,

		Mask_Escapes = pp,
		Mask_RexOnly = B | X | R | W,
		Mask_RexAndEscapes = Mask_RexOnly | Mask_Escapes,
		Mask_Vex2 = pp | L | vvvv | R,
		Mask_Vex3 = pp | L | vvvv | W | mmmmm | B | X | R,
		Mask_Xop = pp | L | vvvv | W | mmmmmxop | B | X | R,
		Mask_EVex = mm | R2 | B | X | pp | vvvv | W | V2 | b | L | L2 | z,
	}

	public static class XexEnums
	{
		[Pure]
		public static bool AllowsEscapes(this XexForm xexType) => xexType <= XexForm.LegacyWithRex;

		[Pure]
		public static XexForm GetTypeFromByte(byte value)
		{
			switch (value)
			{
				case (byte)Vex2.FirstByte: return XexForm.Vex2;
				case (byte)Vex3.FirstByte: return XexForm.Vex3;
				case (byte)Xop.FirstByte: return XexForm.Xop;
				case (byte)EVex.FirstByte: return XexForm.EVex;
				default: return ((value & 0xF0) == (byte)Rex.HighNibble) ? XexForm.LegacyWithRex : XexForm.Legacy;		
			}
		}

		[Pure]
		public static void GetByteCountRange(this XexForm type, out int min, out int max)
		{
			switch (type)
			{
				case XexForm.Legacy: min = 0; max = 2; break;
				case XexForm.LegacyWithRex: min = 1; max = 3; break;
				case XexForm.Vex2: min = max = 2; break;
				case XexForm.Vex3: min = max = 3; break;
				case XexForm.Xop: min = max = 3; break;
				case XexForm.EVex: min = max = 4; break;
				default: throw new ArgumentException("Invalid XexType", "type");
			}
		}

		[Pure]
		public static byte? GetByteCount(this XexForm type)
		{
			switch (type)
			{
				case XexForm.Legacy: case XexForm.LegacyWithRex: return null;
				case XexForm.Vex2: return 2;
				case XexForm.Vex3: return 3;
				case XexForm.EVex: return 4;
				default: throw new ArgumentException("Invalid XexType", "type");
			}
		}

		[Pure]
		public static XexFields GetFields(this XexForm type)
		{
			switch (type)
			{
				case XexForm.Legacy: return XexFields.Mask_Escapes;
				case XexForm.LegacyWithRex: return XexFields.Mask_RexAndEscapes;
				case XexForm.Vex2: return XexFields.Mask_Vex2;
				case XexForm.Vex3: return XexFields.Mask_Vex3;
				case XexForm.Xop: return XexFields.Mask_Xop;
				case XexForm.EVex: return XexFields.Mask_EVex;
				default: throw new ArgumentException("Invalid XexType.", "type");
			}
		}

		[Pure]
		public static bool HasFields(this XexForm type, XexFields mask)
			=> Has(GetFields(type), mask);

		[Pure]
		public static bool Has(this XexFields fields, XexFields mask)
			=> (fields & mask) == mask;

		[Pure]
		public static int GetBitCount(this XexFields field)
		{
			Contract.Requires(IsSingleField(field));
			switch (field)
			{
				case XexFields.mm: return 2;
				case XexFields.pp: return 2;
				case XexFields.vvvv: return 4;
				case XexFields.mmmmm: return 5;
				case XexFields.mmmmmxop: return 5;
				default: return 1;
			}
		}

		[Pure]
		public static bool IsSingleField(this XexFields fields)
			=> fields == XexFields.mmmmm || Bits.IsSingle((uint)fields);

		[Pure]
		public static int TryGetFieldOffset(this XexForm type, XexFields field)
		{
			Contract.Requires(IsSingleField(field));
			throw new NotImplementedException();
		}
	}

	[StructLayout(LayoutKind.Sequential, Size = 4)]
	public struct Xex : IEquatable<Xex>
	{
		#region Fields
		public static readonly Xex None = new Xex();

		// High byte is type, low 3 bytes are like EVEX
		private readonly uint data;
		#endregion

		#region Constructors
		public Xex(OpcodeMap map) : this(XexForm.Legacy)
		{
			Contract.Requires((map.TryAsLegacy() & OpcodeMap.Type_Mask) == OpcodeMap.Type_Legacy);
			data |= (uint)map.GetValue() << (int)EVex.mm_Shift;
		}

		public Xex(Rex rex, OpcodeMap map = OpcodeMap.Default) : this(XexForm.LegacyWithRex)
		{
			Contract.Requires((map.TryAsLegacy() & OpcodeMap.Type_Mask) == OpcodeMap.Type_Legacy);
			data |= (uint)map.GetValue() << (int)EVex.mm_Shift;
			// Could be optimized to bitwise operations
			if ((rex & Rex.B) != 0) data |= (uint)EVex.B;
			if ((rex & Rex.X) != 0) data |= (uint)EVex.X;
			if ((rex & Rex.R) != 0) data |= (uint)EVex.R;
			if ((rex & Rex.W) != 0) data |= (uint)EVex.W;
		}

		public Xex(Vex2 vex2) : this(XexForm.Vex2)
		{
			data |= ((uint)(vex2 & Vex2.pp_Mask) >> (int)Vex2.pp_Shift) << (int)EVex.pp_Shift;
			data |= ((uint)(vex2 & Vex2.vvvv_Mask) >> (int)Vex2.vvvv_Shift) << (int)EVex.vvvv_Shift;
			// Could be optimized to bitwise operations
			if ((vex2 & Vex2.R) != 0) data |= (uint)EVex.R;
			if ((vex2 & Vex2.L) != 0) data |= (uint)EVex.L;
		}

		public Xex(Vex3 vex3) : this(XexForm.Vex3)
		{
			// The mmmmm field goes from 5 to 2 bits
			var map = (byte)((uint)(vex3 & Vex3.mmmmm_Mask) >> (int)Vex3.mmmmm_Shift);
			if (map >= 4) throw new ArgumentException();
			data |= FromVex3(vex3, map);
		}

		public Xex(Xop xop) : this(XexForm.Xop)
		{
			// The mmmmm field goes from 5 to 2 bits
			var map = (byte)((uint)(xop & Xop.mmmmm_Mask) >> (int)Xop.mmmmm_Shift);
			if (map < 8 || map > 10) throw new ArgumentException();
			data |= FromVex3((Vex3)(uint)xop, (byte)(map & 4));
		}

		public Xex(EVex evex) : this(XexForm.EVex)
		{
			data |= (uint)evex & 0xFFFFFF;
		}

		public Xex(XexForm type) { data = (uint)type << 24; }

		private Xex(uint data) { this.data = data; }

		private static uint FromVex3(Vex3 vex3, byte map)
		{
			Contract.Requires(map < 4);
			uint data = 0;
			data |= ((uint)(vex3 & Vex3.pp_Mask) >> (int)Vex3.pp_Shift) << (int)EVex.pp_Shift;
			data |= (uint)map << (int)EVex.mm_Shift;
			data |= ((uint)(~vex3 & Vex3.vvvv_Mask) >> (int)Vex3.vvvv_Shift) << (int)EVex.vvvv_Shift;
			if ((vex3 & Vex3.B) != 0) data |= (uint)EVex.B;
			if ((vex3 & Vex3.X) != 0) data |= (uint)EVex.X;
			if ((vex3 & Vex3.R) != 0) data |= (uint)EVex.R;
			if ((vex3 & Vex3.W) != 0) data |= (uint)EVex.W;
			if ((vex3 & Vex3.L) != 0) data |= (uint)EVex.L;
			return data;
		}
		#endregion

		#region Properties
		public XexForm Type => (XexForm)(data >> 24);

		public int ByteCount
		{
			get { throw new NotImplementedException(); }
		}

		public OpcodeMap OpcodeMap
		{
			get
			{
				byte pp = (byte)Bits.MaskAndShiftRight(data, (uint)EVex.pp_Mask, (int)EVex.pp_Shift);
				switch (Type)
				{
					case XexForm.Legacy:
					case XexForm.LegacyWithRex:
						return OpcodeMap.Type_Legacy.WithValue(pp);

					case XexForm.Vex2:
					case XexForm.Vex3:
					case XexForm.EVex:
						return OpcodeMap.Type_Vex.WithValue(pp);

					case XexForm.Xop: return OpcodeMap.Type_Xop.WithValue((byte)(7 + pp));
					default: throw new UnreachableException();
				}
			}
		}
		#endregion

		#region Instance Methods
		public Xex WithOpcodeMap(OpcodeMap map)
		{
			var mapType = map & OpcodeMap.Type_Mask;
			var mapValue = (byte)((uint)(map & OpcodeMap.Value_Mask) >> (int)OpcodeMap.Value_Shift);
			switch (Type)
			{
				case XexForm.Legacy:
				case XexForm.LegacyWithRex:
					Contract.Assert(mapType == OpcodeMap.Type_Legacy);
					Contract.Assert(mapValue <= 3);
					break;

				case XexForm.Vex2:
				case XexForm.Vex3:
				case XexForm.EVex:
					Contract.Assert(mapType == OpcodeMap.Type_Vex);
					Contract.Assert(mapValue >= 1 && mapValue <= 3);
					break;

				case XexForm.Xop:
					Contract.Assert(mapType == OpcodeMap.Type_Xop);
					Contract.Assert(mapValue >= 8 && mapValue <= 10);
					mapValue -= 7;
					break;

				default: throw new UnreachableException();
			}

			return new Xex(Bits.SetMask(data, (uint)EVex.pp_Mask, (uint)mapValue << (int)EVex.pp_Shift));
		}

		public bool? TryGetFlag(XexFields field)
		{
			Contract.Requires(field.IsSingleField());
			Contract.Requires(field.GetBitCount() == 1);

			int offset = XexForm.EVex.TryGetFieldOffset(field);
			if (offset < 0) return null;

			return ((data >> offset) & 1) == 1;
		}

		public byte? TryGetField(XexFields field)
		{
			Contract.Requires(field.IsSingleField());

			int bitCount = field.GetBitCount();
			int offset = XexForm.EVex.TryGetFieldOffset(field);
			if (offset < 0) return null;

			return (byte)((data >> offset) & ((1 << bitCount) - 1));
		}

		public static bool Equals(Xex first, Xex second) => first.Equals(second);
		public bool Equals(Xex other) => data == other.data;
		public override bool Equals(object obj) => obj is Xex && Equals((Xex)obj);
		public override int GetHashCode() => unchecked((int)data);
		public override string ToString() => Type.ToString();
		#endregion

		#region Operators
		public static bool operator ==(Xex lhs, Xex rhs) => Equals(lhs, rhs);
		public static bool operator !=(Xex lhs, Xex rhs) => !Equals(lhs, rhs);
		#endregion
	}
}
