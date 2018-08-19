using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86
{
	public enum VexType : byte
	{
		Vex, Xop, EVex
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

	public static class VexEnums
	{
		#region VexType
		public static XexType AsLargestXexType(this VexType type)
		{
			switch (type)
			{
				case VexType.Vex: return XexType.Vex3;
				case VexType.Xop: return XexType.Xop;
				case VexType.EVex: return XexType.EVex;
				default: throw new ArgumentOutOfRangeException(nameof(type));
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

		public static VexEncoding AsVexEncoding(this Vex3Xop vex)
		{
			return new VexEncoding.Builder
			{
				Type = vex.IsVex3() ? VexType.Vex
					: vex.IsXop() ? VexType.Xop
					: throw new ArgumentException(),
				VectorSize = (vex & Vex3Xop.VectorSize256) == 0
					? SseVectorSize._128Bits : SseVectorSize._256Bits,
				SimdPrefix = vex.GetSimdPrefix(),
				OpcodeMap = vex.GetOpcodeMap(),
				RexW = (vex & Vex3Xop.OperandSize64) != 0
			}.Build();
		}
		#endregion
	}
}
