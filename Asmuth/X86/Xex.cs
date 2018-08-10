using System;
using System.Collections.Generic;
using System.Diagnostics;
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
		SimdPrefix_F3 = 2 << SimdPrefix_Shift,
		SimdPrefix_F2 = 3 << SimdPrefix_Shift,
		SimdPrefix_Mask = 3 << SimdPrefix_Shift,

		VectorSize256 = 1 << 2, // L

		// vvvv
		NotNonDestructiveReg_Shift = 3,
		NotNonDestructiveReg_Unused = 0xF << NotNonDestructiveReg_Shift,
		NotNonDestructiveReg_Mask = 0xF << NotNonDestructiveReg_Shift,

		NotModRegExtension = 1 << 7, // R
	}

	/// <summary>
	/// Represents either Intel's 3-byte VEX prefix or AMD's XOP prefix.
	/// </summary>
	[Flags]
	public enum Vex3Xop
	{
		ByteCount = 3,
		FirstByte_Vex3 = 0xC4,
		FirstByte_Xop = 0x8F,

		Header_Mask = 0xFF0000,
		Header_Vex3 = 0xC40000,
		Header_Xop = 0x8F0000,

		// pp
		SimdPrefix_Shift = 0,
		SimdPrefix_None = 0 << SimdPrefix_Shift,
		SimdPrefix_66 = 1 << SimdPrefix_Shift,
		SimdPrefix_F3 = 2 << SimdPrefix_Shift,
		SimdPrefix_F2 = 3 << SimdPrefix_Shift,
		SimdPrefix_Mask = 3 << SimdPrefix_Shift,

		VectorSize256 = 1 << 2, // L

		// vvvv
		NotNonDestructiveReg_Shift = 3,
		NotNonDestructiveReg_Unused = 0xF << NotNonDestructiveReg_Shift,
		NotNonDestructiveReg_Mask = 0xF << NotNonDestructiveReg_Shift,

		OperandSize64 = 1 << 7, // W

		// Opcode map
		OpcodeMap_Shift = 8,
		OpcodeMap_0F = 1 << OpcodeMap_Shift, // Used with VEX
		OpcodeMap_0F38 = 2 << OpcodeMap_Shift,
		OpcodeMap_0F3A = 3 << OpcodeMap_Shift,
		OpcodeMap_8 = 8 << OpcodeMap_Shift, // Used with XOP
		OpcodeMap_9 = 9 << OpcodeMap_Shift,
		OpcodeMap_10 = 10 << OpcodeMap_Shift,
		OpcodeMap_Mask = 0x1F << OpcodeMap_Shift,

		NotBaseRegExtension = 1 << 13, // B
		NotIndexRegExtension = 1 << 14, // X
		NotModRegExtension = 1 << 15, // R

		NoRegExtensions = NotBaseRegExtension | NotIndexRegExtension | NotModRegExtension,
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
		pp_F3 = 2U << (int)pp_Shift,
		pp_F2 = 3U << (int)pp_Shift,
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
		public static bool AllowsEscapes(this XexType xexType) => xexType <= XexType.RexAndEscapes;

		public static bool IsVex(this XexType xexType)
			=> xexType == XexType.Vex2 || xexType == XexType.Vex3;

		public static bool IsVex3Xop(this XexType xexType)
			=> xexType == XexType.Vex3 || xexType == XexType.Xop;

		public static XexType SniffType(CodeSegmentType codeSegmentType, byte @byte)
			=> SniffOrGetType(codeSegmentType, @byte, second: null);

		public static XexType GetType(CodeSegmentType codeSegmentType, byte first, byte second)
			=> SniffOrGetType(codeSegmentType, first, second);

		private static XexType SniffOrGetType(CodeSegmentType codeSegmentType, byte first, byte? second)
		{
			ModRM secondRM = (ModRM)second.GetValueOrDefault();
			switch (first)
			{
				case (byte)Vex2.FirstByte:
					if (codeSegmentType.IsIA32() && second.HasValue && secondRM.IsMemoryRM())
						return XexType.Escapes; // This is no VEX2, it's an LDS (C5 /r)
					return XexType.Vex2;

				case (byte)Vex3Xop.FirstByte_Vex3:
					if (codeSegmentType.IsIA32() && second.HasValue && secondRM.IsMemoryRM())
						return XexType.Escapes; // This is no VEX3, it's an LES (C4 /r)
					return XexType.Vex3;

				case (byte)Vex3Xop.FirstByte_Xop:
					if (second.HasValue && secondRM.GetReg() == 0)
						return XexType.Escapes; // This is no XOP, it's a POP (8F /0)
					return XexType.Xop;

				case (byte)EVex.FirstByte:
					if (codeSegmentType.IsIA32() && second.HasValue && secondRM.IsMemoryRM())
						return XexType.Escapes; // This is no EVEX, it's a BOUND (62 /r)
					return XexType.EVex;

				default:
					if (codeSegmentType.IsLongMode() && (first & 0xF0) == (byte)Rex.HighNibble)
						return XexType.RexAndEscapes;
					return XexType.Escapes;
			}
		}

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
		public static byte GetFirstByte(this Vex2 vex) => (byte)Vex2.FirstByte;

		public static byte GetSecondByte(this Vex2 vex) => unchecked((byte)vex);

		public static SimdPrefix GetSimdPrefix(this Vex2 vex)
			=> (SimdPrefix)Bits.MaskAndShiftRight((uint)vex, (uint)Vex2.SimdPrefix_Mask, (int)Vex2.SimdPrefix_Shift);

		public static byte GetNonDestructiveReg(this Vex2 vex)
			=> (byte)(~Bits.MaskAndShiftRight((uint)vex, (uint)Vex2.NotNonDestructiveReg_Mask, (int)Vex2.NotNonDestructiveReg_Shift) & 0xF);
		#endregion

		#region Vex3
		public static bool IsVex3(this Vex3Xop xop)
		{
			var header = (xop & Vex3Xop.Header_Mask);
			return header == Vex3Xop.Header_Xop || header == 0;
		}

		public static bool IsXop(this Vex3Xop xop) => (xop & Vex3Xop.Header_Mask) == Vex3Xop.Header_Xop;

		public static byte GetFirstByte(this Vex3Xop vex) => unchecked((byte)((uint)vex >> 16));

		public static byte GetSecondByte(this Vex3Xop vex) => unchecked((byte)((uint)vex >> 8));

		public static byte GetThirdByte(this Vex3Xop vex) => unchecked((byte)vex);

		public static SimdPrefix GetSimdPrefix(this Vex3Xop vex)
			=> (SimdPrefix)Bits.MaskAndShiftRight((uint)vex, (uint)Vex3Xop.SimdPrefix_Mask, (int)Vex3Xop.SimdPrefix_Shift);

		public static OpcodeMap GetOpcodeMap(this Vex3Xop vex)
			=> (OpcodeMap)Bits.MaskAndShiftRight((uint)vex, (uint)Vex3Xop.OpcodeMap_Mask, (int)Vex3Xop.OpcodeMap_Shift);

		public static byte GetNonDestructiveReg(this Vex3Xop vex)
			=> (byte)(~Bits.MaskAndShiftRight((uint)vex, (uint)Vex3Xop.NotNonDestructiveReg_Mask, (int)Vex3Xop.NotNonDestructiveReg_Shift) & 0xF); 
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
			if (!escapes.IsEncodableAsEscapeBytes()) throw new ArgumentException();
			flags = BaseFlags(XexType.Escapes, X86.SimdPrefix.None, escapes);
		}

		public Xex(Rex rex, OpcodeMap escapes = OpcodeMap.Default)
		{
			if (!escapes.IsEncodableAsEscapeBytes()) throw new ArgumentException();
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

		public Xex(Vex3Xop vex3)
		{
			flags = BaseFlags(vex3.IsXop() ? XexType.Xop : XexType.Vex3,
				vex3.GetSimdPrefix(), vex3.GetOpcodeMap());
			flags |= (Flags)((uint)vex3.GetNonDestructiveReg() << (int)Flags.NonDestructiveReg_Shift);
			if ((vex3 & Vex3Xop.NotModRegExtension) == 0) flags |= Flags.ModRegExtension;
			if ((vex3 & Vex3Xop.NotBaseRegExtension) == 0) flags |= Flags.BaseRegExtension;
			if ((vex3 & Vex3Xop.NotIndexRegExtension) == 0) flags |= Flags.IndexRegExtension;
			if ((vex3 & Vex3Xop.OperandSize64) != 0) flags |= Flags.OperandSize64;
			if ((vex3 & Vex3Xop.VectorSize256) != 0) flags |= Flags.VectorSize_256;
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
					case XexType.Xop: return 3;
					case XexType.EVex: return 4;
					default: throw new UnreachableException();
				}
			}
		}
		#endregion

		#region Methods
		public Xex WithOpcodeMap(OpcodeMap map)
		{
			if (!Type.CanEncodeMap(map)) throw new ArgumentException("The current XEX type cannot encode this opcode map.", nameof(map));
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
