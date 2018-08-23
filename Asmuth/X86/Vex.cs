using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Asmuth.X86
{
	public enum VexType : byte
	{
		Vex, Xop, EVex
	}

	[StructLayout(LayoutKind.Sequential, Size = sizeof(byte))]
	public readonly struct Vex2 : IEquatable<Vex2>
	{
		public const int SizeInBytes = 2;
		public const byte FirstByte = 0xC5;
		private const byte XorMask = 0b1_1111_000;
		private const byte ModRegExtensionBit = 0b1000_0000;
		private const byte VectorSize256Bit = 0b0000_0100;

		// RvvvvLpp
		private readonly byte xoredValue;

		public Vex2(byte secondByte) => xoredValue = (byte)(secondByte ^ XorMask);
		public Vex2(byte firstByte, byte secondByte)
			: this(secondByte)
		{
			if (firstByte != FirstByte) throw new ArgumentException();
		}

		public byte SecondByte => (byte)(xoredValue ^ XorMask);
		public bool ModRegExtension => (xoredValue & ModRegExtensionBit) != 0;
		public byte NonDestructiveReg => (byte)((xoredValue >> 3) & 0xF);
		public bool VectorSize256 => (xoredValue & VectorSize256Bit) != 0;
		public SseVectorSize VectorSize => VectorSize256 ? SseVectorSize._256Bits : SseVectorSize._128Bits;
		public SimdPrefix SimdPrefix => (SimdPrefix)(xoredValue & 0x3);
		
		public bool Equals(Vex2 other) => xoredValue == other.xoredValue;
		public override bool Equals(object obj) => obj is Vex2 && Equals((Vex2)obj);
		public override int GetHashCode() => xoredValue;
		public static bool Equals(Vex2 lhs, Vex2 rhs) => lhs.Equals(rhs);
		public static bool operator ==(Vex2 lhs, Vex2 rhs) => Equals(lhs, rhs);
		public static bool operator !=(Vex2 lhs, Vex2 rhs) => !Equals(lhs, rhs);

		public static bool Test(CodeSegmentType codeSegmentType, byte firstByte, byte secondByte)
			=> (firstByte == FirstByte) &&
				(codeSegmentType.IsLongMode() || !((ModRM)secondByte).IsMemoryRM);
	}

	[StructLayout(LayoutKind.Sequential, Size = sizeof(ushort))]
	public readonly struct Vex3Xop : IEquatable<Vex3Xop>
	{
		public struct Builder
		{
			public bool ModRegExtension;
			public bool BaseRegExtension;
			public bool IndexRegExtension;
			public bool RexW;
			public SseVectorSize VectorSize;
			public OpcodeMap OpcodeMap;
			public SimdPrefix SimdPrefix;
			public byte NonDestructiveReg;

			public void Validate()
			{
				if (VectorSize >= SseVectorSize._512Bits)
					throw new ArgumentOutOfRangeException(nameof(VectorSize));
			}

			public Vex3Xop Build() => new Vex3Xop(ref this);

			public static implicit operator Vex3Xop(Builder builder) => builder.Build();
		}

		public const int ByteCount = 3;
		public const byte FirstByte_Vex3 = 0xC4;
		public const byte FirstByte_Xop = 0x8F;
		private const ushort XorMask = 0b11100000_01111000;
		private const ushort ModRegExtensionBit = 1 << 15;
		private const ushort BaseRegExtensionBit = 1 << 14;
		private const ushort IndexRegExtensionBit = 1 << 13;
		private const ushort RexWBit = 1 << 7;
		private const ushort VectorSize256Bit = 1 << 2;
		private const int OpcodeMapShift = 8;
		private const int NonDestructiveRegShift = 3;
		private const int SimdPrefixShift = 0;

		// 0bRXBmmmmm_WvvvvLpp
		private readonly ushort xoredValue;

		public Vex3Xop(byte secondByte, byte thirdByte)
		{
			xoredValue = (ushort)((((ushort)secondByte << 8) | thirdByte) ^ XorMask);
		}

		public Vex3Xop(byte firstByte, byte secondByte, byte thirdByte)
			: this(secondByte, thirdByte)
		{
			switch (firstByte)
			{
				case FirstByte_Vex3:
					if (!IsVex3) throw new ArgumentException("Invalid VEX3 opcode map.");
					break;

				case FirstByte_Xop:
					if (!IsXop) throw new ArgumentException("Invalid XOP opcode map.");
					break;

				default: throw new ArgumentException("Invalid first VEX3/XOP byte.", nameof(firstByte));
			}
		}

		public Vex3Xop(ref Builder builder)
		{
			builder.Validate();

			xoredValue = (ushort)(((int)builder.OpcodeMap << OpcodeMapShift)
				| ((int)builder.NonDestructiveReg << NonDestructiveRegShift)
				| ((int)builder.SimdPrefix << SimdPrefixShift));
			if (builder.ModRegExtension) xoredValue |= ModRegExtensionBit;
			if (builder.BaseRegExtension) xoredValue |= BaseRegExtensionBit;
			if (builder.IndexRegExtension) xoredValue |= IndexRegExtensionBit;
			if (builder.RexW) xoredValue |= RexWBit;
			if (builder.VectorSize == SseVectorSize._256Bits) xoredValue |= VectorSize256Bit;
		}

		public bool IsVex3 => OpcodeMap <= OpcodeMap.Escape0F3A;
		public bool IsXop => OpcodeMap >= OpcodeMap.Xop8;
		public VexType VexType => IsVex3 ? VexType.Vex : VexType.Xop;
		public bool ModRegExtension => (xoredValue & ModRegExtensionBit) != 0;
		public bool BaseRegExtension => (xoredValue & BaseRegExtensionBit) != 0;
		public bool IndexRegExtension => (xoredValue & IndexRegExtensionBit) != 0;
		public bool VectorSize256 => (xoredValue & VectorSize256Bit) != 0;
		public SseVectorSize VectorSize => VectorSize256 ? SseVectorSize._256Bits : SseVectorSize._128Bits;
		public bool RexW => (xoredValue & RexWBit) != 0;
		public OpcodeMap OpcodeMap => (OpcodeMap)((xoredValue >> OpcodeMapShift) & 0x1F);
		public byte NonDestructiveReg => (byte)((xoredValue >> NonDestructiveRegShift) & 0xF);
		public SimdPrefix SimdPrefix => (SimdPrefix)(xoredValue & 3);

		public byte FirstByte => IsVex3 ? FirstByte_Vex3 : FirstByte_Xop;
		public byte SecondByte => (byte)((xoredValue ^ XorMask) >> 8);
		public byte ThirdByte => (byte)(xoredValue ^ XorMask);

		public VexEncoding AsVexEncoding()
		{
			return new VexEncoding.Builder
			{
				Type = VexType,
				VectorSize = VectorSize,
				SimdPrefix = SimdPrefix,
				OpcodeMap = OpcodeMap,
				RexW = RexW
			}.Build();
		}

		public bool Equals(Vex3Xop other) => xoredValue == other.xoredValue;
		public override bool Equals(object obj) => obj is Vex3Xop && Equals((Vex3Xop)obj);
		public override int GetHashCode() => xoredValue;
		public static bool Equals(Vex3Xop lhs, Vex3Xop rhs) => lhs.Equals(rhs);
		public static bool operator ==(Vex3Xop lhs, Vex3Xop rhs) => Equals(lhs, rhs);
		public static bool operator !=(Vex3Xop lhs, Vex3Xop rhs) => !Equals(lhs, rhs);
		
		public static bool Test(CodeSegmentType codeSegmentType, byte firstByte, byte secondByte)
		{
			// VEX3 is ambiguous with LES (C4 /r)
			// XOP is ambiguous with POP (8F /0)
			if (firstByte == FirstByte_Vex3)
				return codeSegmentType.IsLongMode() || ((ModRM)secondByte).IsRegRM;
			else if (firstByte == FirstByte_Xop)
				return ((ModRM)secondByte).Reg != 0;
			else
				return false;
		}
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
	}
}
