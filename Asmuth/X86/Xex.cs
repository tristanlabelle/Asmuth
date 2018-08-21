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

	public static class XexTypeEnum
	{
		public static VexType? AsVexType(this XexType xexType)
		{
			switch (xexType)
			{
				case XexType.Vex2: case XexType.Vex3: return VexType.Vex;
				case XexType.Xop: return VexType.Xop;
				case XexType.EVex: return VexType.EVex;
				default: return null;
			}
		}

		public static bool AllowsEscapes(this XexType xexType) => xexType <= XexType.RexAndEscapes;

		public static bool IsVex(this XexType xexType)
			=> xexType == XexType.Vex2 || xexType == XexType.Vex3;

		public static bool IsVex3Xop(this XexType xexType)
			=> xexType == XexType.Vex3 || xexType == XexType.Xop;

		public static XexType SniffByte(CodeSegmentType codeSegmentType, byte @byte)
			=> SniffByte(codeSegmentType, @byte, second: null);

		public static XexType FromBytes(CodeSegmentType codeSegmentType, byte first, byte second)
			=> SniffByte(codeSegmentType, first, second);

		private static XexType SniffByte(CodeSegmentType codeSegmentType, byte first, byte? second)
		{
			ModRM secondRM = (ModRM)second.GetValueOrDefault();
			switch (first)
			{
				case (byte)Vex2.FirstByte:
					if (codeSegmentType.IsIA32() && second.HasValue && secondRM.IsMemoryRM)
						return XexType.Escapes; // This is no VEX2, it's an LDS (C5 /r)
					return XexType.Vex2;

				case (byte)Vex3Xop.FirstByte_Vex3:
					if (codeSegmentType.IsIA32() && second.HasValue && secondRM.IsMemoryRM)
						return XexType.Escapes; // This is no VEX3, it's an LES (C4 /r)
					return XexType.Vex3;

				case (byte)Vex3Xop.FirstByte_Xop:
					if (second.HasValue && secondRM.Reg == 0)
						return XexType.Escapes; // This is no XOP, it's a POP (8F /0)
					return XexType.Xop;

				case (byte)EVex.FirstByte:
					if (codeSegmentType.IsIA32() && second.HasValue && secondRM.IsMemoryRM)
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
		public VexType? VexType => Type.AsVexType();
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

		public SseVectorSize VectorSize => (SseVectorSize)GetField(Flags.VectorSize_Mask, Flags.VectorSize_Shift);

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
		public Rex GetRex()
		{
			if (Type != XexType.RexAndEscapes) throw new InvalidOperationException();
			Rex rex = Rex.Reserved_Value;
			if (OperandSize64) rex |= Rex.OperandSize64;
			if (ModRegExtension) rex |= Rex.ModRegExtension;
			if (BaseRegExtension) rex |= Rex.BaseRegExtension;
			if (IndexRegExtension) rex |= Rex.IndexRegExtension;
			return rex;
		}

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
