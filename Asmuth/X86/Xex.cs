using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	// XEX is a made-up name for all non-legacy prefixes and escape bytes.
	// This includes escape bytes, REX + escape bytes, VEX, XOP and EVEX

	public enum XexType : byte
	{
		Escapes,
		RexAndEscapes,
		Vex2,
		Vex3,
		Xop,
		EVex,
	}

	[Flags]
	public enum Rex : byte
	{
		Default = Reserved_Value,

		ByteCount = 1,
		HighNibble = 0x40,

		Reserved_Mask = 0xF0,
		Reserved_Value = 0x40,

		BaseRegExtension = 1 << 0, // B
		IndexRegExtension = 1 << 1, // X
		ModRegExtension = 1 << 2, // R
		OperandSize64 = 1 << 3 // W
	}

	[Flags]
	public enum Vex2 : ushort
	{
		ByteCount = 2,
		FirstByte = 0xC5,

		Reserved_Mask = 0xFF00,
		Reserved_Value = 0xC500,

		// pp
		SimdPrefix_Shift = 0,
		SimdPrefix_None = 0 << SimdPrefix_Shift,
		SimdPrefix_66 = 1 << SimdPrefix_Shift,
		SimdPrefix_F2 = 2 << SimdPrefix_Shift,
		SimdPrefix_F3 = 3 << SimdPrefix_Shift,
		SimdPrefix_Mask = 3 << SimdPrefix_Shift,

		VectorSize256 = 1 << 2, // L

		// vvvv
		NonDestructiveReg_Shift = 3,
		NonDestructiveReg_Mask = 15 << NonDestructiveReg_Shift,

		NotModRegExtension = 1 << 7, // R
	}

	[Flags]
	public enum Vex3 : uint
	{
		ByteCount = 3,
		FirstByte = 0xC4,

		Reserved_Mask = 0xFF0000,
		Reserved_Value = 0xC40000,

		// pp
		SimdPrefix_Shift = 0,
		SimdPrefix_None = 0U << (int)SimdPrefix_Shift,
		SimdPrefix_66 = 1U << (int)SimdPrefix_Shift,
		SimdPrefix_F2 = 2U << (int)SimdPrefix_Shift,
		SimdPrefix_F3 = 3U << (int)SimdPrefix_Shift,
		SimdPrefix_Mask = 3U << (int)SimdPrefix_Shift,

		VectorSize256 = 1 << 2, // L

		// vvvv
		NonDestructiveReg_Shift = 3,
		NonDestructiveReg_Mask = 15U << (int)NonDestructiveReg_Shift,

		OperandSize64 = 1 << 7,

		// Opcode map
		OpcodeMap_Shift = 8,
		OpcodeMap_0F = 1 << (int)OpcodeMap_Shift,
		OpcodeMap_0F38 = 2 << (int)OpcodeMap_Shift,
		OpcodeMap_0F3A = 3 << (int)OpcodeMap_Shift,
		OpcodeMap_Mask = 0x1FU << (int)OpcodeMap_Shift,

		NotBaseRegExtension = 1 << 13,
		NotIndexRegExtension = 1 << 14,
		NotModRegExtension = 1 << 15,
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

	public static class XexEnums
	{
		#region XexType
		[Pure]
		public static bool AllowsEscapes(this XexType xexType) => xexType <= XexType.RexAndEscapes;

		[Pure]
		public static bool IsVex(this XexType xexType)
			=> xexType == XexType.Vex2 || xexType == XexType.Vex3;

		[Pure]
		public static XexType GetTypeFromByte(byte value)
		{
			switch (value)
			{
				case (byte)Vex2.FirstByte: return XexType.Vex2;
				case (byte)Vex3.FirstByte: return XexType.Vex3;
				case (byte)Xop.FirstByte: return XexType.Xop;
				case (byte)EVex.FirstByte: return XexType.EVex;
				default: return ((value & 0xF0) == (byte)Rex.HighNibble) ? XexType.RexAndEscapes : XexType.Escapes;
			}
		}

		[Pure]
		public static int GetMinSizeInBytes(this XexType type)
		{
			switch (type)
			{
				case XexType.Escapes: return 0;
				case XexType.RexAndEscapes: return 1;
				case XexType.Vex2: return 2;
				case XexType.Vex3: return 3;
				case XexType.Xop: return 3;
				case XexType.EVex: return 4;
				default: throw new ArgumentException("Invalid XexType", nameof(type));
			}
		}

		[Pure]
		public static bool CanEncodeMap(this XexType type, OpcodeMap map)
		{
			switch (type)
			{
				case XexType.Escapes:
				case XexType.RexAndEscapes:
					return map <= OpcodeMap.Escape0F3A;

				case XexType.Vex2:
				case XexType.Vex3:
				case XexType.EVex:
					return map >= OpcodeMap.Escape0F && map <= OpcodeMap.Escape0F3A;

				case XexType.Xop:
					return map == OpcodeMap.Xop8 || map == OpcodeMap.Xop9;

				default:
					throw new ArgumentException(nameof(map));
			}
		} 
		#endregion

		#region Vex2
		[Pure]
		public static byte GetFirstByte(this Vex2 vex) => (byte)Vex2.FirstByte;

		[Pure]
		public static byte GetSecondByte(this Vex2 vex) => unchecked((byte)vex);

		[Pure]
		public static SimdPrefix GetSimdPrefix(this Vex2 vex)
			=> (SimdPrefix)Bits.MaskAndShiftRight((uint)vex, (uint)Vex2.SimdPrefix_Mask, (int)Vex2.SimdPrefix_Shift);

		[Pure]
		public static byte GetNonDestructiveReg(this Vex2 vex)
			=> (byte)Bits.MaskAndShiftRight((uint)vex, (uint)Vex2.NonDestructiveReg_Mask, (int)Vex2.NonDestructiveReg_Shift);
		#endregion

		#region Vex3
		[Pure]
		public static byte GetFirstByte(this Vex3 vex) => (byte)Vex3.FirstByte;

		[Pure]
		public static byte GetSecondByte(this Vex3 vex) => unchecked((byte)((uint)vex >> 8));

		[Pure]
		public static byte GetThirdByte(this Vex3 vex) => unchecked((byte)vex);

		[Pure]
		public static SimdPrefix GetSimdPrefix(this Vex3 vex)
			=> (SimdPrefix)Bits.MaskAndShiftRight((uint)vex, (uint)Vex3.SimdPrefix_Mask, (int)Vex3.SimdPrefix_Shift);

		[Pure]
		public static OpcodeMap GetOpcodeMap(this Vex3 vex)
			=> (OpcodeMap)Bits.MaskAndShiftRight((uint)vex, (uint)Vex3.OpcodeMap_Mask, (int)Vex3.OpcodeMap_Shift);

		[Pure]
		public static byte GetNonDestructiveReg(this Vex3 vex)
			=> (byte)Bits.MaskAndShiftRight((uint)vex, (uint)Vex3.NonDestructiveReg_Mask, (int)Vex3.NonDestructiveReg_Shift); 
		#endregion
	}

	[StructLayout(LayoutKind.Sequential, Size = 4)]
	public struct Xex : IEquatable<Xex>
	{
		[Flags]
		private enum Flags : uint
		{
			XexType_Shift = 0,
			XexType_Mask = 7 << (int)XexType_Shift,

			SimdPrefix_Shift = XexType_Shift + 3,
			SimdPrefix_Mask = 3U << (int)SimdPrefix_Shift,

			OpcodeMap_Shift = SimdPrefix_Shift + 2,
			OpcodeMap_Mask = 0x1F << (int)OpcodeMap_Shift,

			NonDestructiveReg_Shift = OpcodeMap_Shift + 5,
			NonDestructiveReg_Mask = 0x1F << (int)NonDestructiveReg_Shift,

			VectorSize_Shift = NonDestructiveReg_Shift + 5,
			VectorSize_128 = 0 << (int)VectorSize_Shift,
			VectorSize_256 = 1 << (int)VectorSize_Shift,
			VectorSize_512 = 2 << (int)VectorSize_Shift,
			VectorSize_Mask = 3 << (int)VectorSize_Shift,

			OperandSize64 = 1 << (int)(VectorSize_Shift + 2),
			ModRegExtension = OperandSize64 << 1,
			BaseRegExtension = ModRegExtension << 1,
			IndexRegExtension = BaseRegExtension << 1,
		}

		#region Fields
		public static readonly Xex None = new Xex();

		private readonly Flags flags;
		#endregion

		#region Constructors
		public Xex(OpcodeMap escapes)
		{
			Contract.Requires(escapes.IsEncodableAsEscapeBytes());
			flags = BaseFlags(XexType.Escapes, X86.SimdPrefix.None, escapes);
		}

		public Xex(Rex rex, OpcodeMap escapes = OpcodeMap.Default)
		{
			Contract.Requires(escapes.IsEncodableAsEscapeBytes());
			flags = BaseFlags(XexType.Escapes, X86.SimdPrefix.None, escapes);
			if ((rex & Rex.OperandSize64) != 0) flags |= Flags.OperandSize64;
			if ((rex & Rex.ModRegExtension) != 0) flags |= Flags.ModRegExtension;
			if ((rex & Rex.BaseRegExtension) != 0) flags |= Flags.BaseRegExtension;
			if ((rex & Rex.IndexRegExtension) != 0) flags |= Flags.IndexRegExtension;
		}

		public Xex(Vex2 vex2)
		{
			flags = BaseFlags(XexType.Vex2, vex2.GetSimdPrefix(), OpcodeMap.Default);
			flags |= (Flags)((uint)vex2.GetNonDestructiveReg() << (int)Flags.NonDestructiveReg_Shift);
			if ((vex2 & Vex2.NotModRegExtension) == 0) flags |= Flags.ModRegExtension;
			if ((vex2 & Vex2.VectorSize256) != 0) flags |= Flags.VectorSize_256;
		}

		public Xex(Vex3 vex3)
		{
			flags = BaseFlags(XexType.Vex3, vex3.GetSimdPrefix(), vex3.GetOpcodeMap());
			flags |= (Flags)((uint)vex3.GetNonDestructiveReg() << (int)Flags.NonDestructiveReg_Shift);
			if ((vex3 & Vex3.NotModRegExtension) == 0) flags |= Flags.ModRegExtension;
			if ((vex3 & Vex3.NotBaseRegExtension) == 0) flags |= Flags.BaseRegExtension;
			if ((vex3 & Vex3.NotIndexRegExtension) == 0) flags |= Flags.IndexRegExtension;
			if ((vex3 & Vex3.OperandSize64) != 0) flags |= Flags.OperandSize64;
			if ((vex3 & Vex3.VectorSize256) != 0) flags |= Flags.VectorSize_256;
		}

		public Xex(Xop xop)
		{
			throw new NotImplementedException();
		}

		public Xex(EVex evex)
		{
			throw new NotImplementedException();
		}

		private Xex(Flags flags)
		{
			this.flags = flags;
		}
		#endregion

		#region Properties
		public XexType Type => (XexType)GetField(Flags.XexType_Mask, Flags.XexType_Shift);
		public OpcodeMap OpcodeMap => (OpcodeMap)GetField(Flags.OpcodeMap_Mask, Flags.OpcodeMap_Shift);

		public SimdPrefix? SimdPrefix
		{
			get
			{
				if (Type <= XexType.RexAndEscapes) return null;
				return (SimdPrefix)GetField(Flags.SimdPrefix_Mask, Flags.SimdPrefix_Shift);
			}
		}

		public bool OperandSize64 => (flags & Flags.OperandSize64) != 0;
		public bool ModRegExtension => (flags & Flags.ModRegExtension) != 0;
		public bool BaseRegExtension => (flags & Flags.BaseRegExtension) != 0;
		public bool IndexRegExtension => (flags & Flags.IndexRegExtension) != 0;

		public OperandSize VectorSize => (OperandSize)((uint)OperandSize._128 + GetField(Flags.VectorSize_Mask, Flags.VectorSize_Shift));

		public byte? NonDestructiveReg
		{
			get
			{
				switch (Type)
				{
					case XexType.Vex2:
					case XexType.Vex3:
					case XexType.Xop:
					case XexType.EVex:
						return (byte)GetField(Flags.NonDestructiveReg_Mask, Flags.NonDestructiveReg_Shift);

					default: return null;
				}
			}
		}
		
		public int SizeInBytes
		{
			get
			{
				switch (Type)
				{
					case XexType.Escapes: return OpcodeMap.GetEscapeByteCount();
					case XexType.RexAndEscapes: return 1 + OpcodeMap.GetEscapeByteCount();
					case XexType.Vex2: return 2;
					case XexType.Vex3: return 3;
					case XexType.Xop: return 4;
					case XexType.EVex: return 4;
					default: throw new UnreachableException();
				}
			}
		}
		#endregion

		#region Methods
		public Xex WithOpcodeMap(OpcodeMap map)
		{
			Contract.Requires(Type.CanEncodeMap(map));
			return new Xex((flags & ~Flags.OpcodeMap_Mask)
				| (Flags)((uint)map << (int)Flags.OpcodeMap_Shift));
		}

		public static bool Equals(Xex first, Xex second) => first.Equals(second);
		public bool Equals(Xex other) => flags == other.flags;
		public override bool Equals(object obj) => obj is Xex && Equals((Xex)obj);
		public override int GetHashCode() => unchecked((int)flags);
		public override string ToString() => Type.ToString();

		private static T NotImplemented<T>() { throw new NotImplementedException(); }

		private uint GetField(Flags mask, Flags shift)
			=> Bits.MaskAndShiftRight((uint)flags, (uint)mask, (int)shift);

		private static Flags BaseFlags(XexType type, SimdPrefix simdPrefix, OpcodeMap opcodeMap)
			=> (Flags)(((uint)type << (int)Flags.XexType_Shift)
				| ((uint)simdPrefix << (int)Flags.SimdPrefix_Shift)
				| ((uint)opcodeMap << (int)Flags.OpcodeMap_Shift));
		#endregion

		#region Operators
		public static bool operator ==(Xex lhs, Xex rhs) => Equals(lhs, rhs);
		public static bool operator !=(Xex lhs, Xex rhs) => !Equals(lhs, rhs);
		#endregion
	}
}
