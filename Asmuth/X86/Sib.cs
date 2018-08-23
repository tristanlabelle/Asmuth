using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	public enum SibScale : byte
	{
		_1,
		_2,
		_4,
		_8
	}

	public static class SibScaleEnum
	{
		public static int ToInt(this SibScale value) => 1 << (int)value;
	}

	[StructLayout(LayoutKind.Sequential, Size = sizeof(byte))]
	public readonly struct Sib : IEquatable<Sib>
	{
		public const byte ZeroIndex = 4;
		public const byte SpecialBase = 5;

		public readonly byte Value;

		public Sib(byte value) => Value = value;

		public Sib(SibScale scale, byte index, byte @base)
		{
			if ((byte)scale > 3) throw new ArgumentOutOfRangeException(nameof(scale));
			if (index > 7) throw new ArgumentOutOfRangeException(nameof(index));
			if (@base > 7) throw new ArgumentOutOfRangeException(nameof(@base));
			Value = (byte)(((byte)scale << 6) | (index << 3) | @base);
		}

		public Sib(SibScale scale, GprCode? index, GprCode? @base)
		{
			if ((byte)scale > 3) throw new ArgumentOutOfRangeException(nameof(scale));
			if (index == (GprCode)ZeroIndex) throw new ArgumentException();
			if (index > (GprCode)7) throw new ArgumentOutOfRangeException(nameof(index));
			if (@base == (GprCode)SpecialBase) throw new ArgumentException();
			if (@base > (GprCode)7) throw new ArgumentOutOfRangeException(nameof(@base));
			Value = (byte)(((byte)scale << 6)
				| ((byte)index.GetValueOrDefault((GprCode)ZeroIndex) << 3)
				| (byte)@base.GetValueOrDefault((GprCode)SpecialBase));
		}

		public SibScale Scale => (SibScale)(Value >> 6);
		public byte Index => (byte)((Value >> 3) & 7);
		public byte Base => (byte)(Value & 7);

		public bool IsSpecialBase => Base == SpecialBase;
		public bool IsZeroIndex => Index == ZeroIndex;
		public GprCode? IndexReg => Index == ZeroIndex ? null : (GprCode?)Index;
		public bool IsIndexed => Index != ZeroIndex;

		public GprCode? GetBaseReg(ModRM modRM)
		{
			if (Base == SpecialBase && modRM.Mod == ModRMMod.Indirect)
				return null; // No base
			return (GprCode)Base;
		}

		public bool Equals(Sib other) => Value == other.Value;
		public override bool Equals(object obj) => obj is Sib && Equals((Sib)obj);
		public override int GetHashCode() => Value;
		public static bool Equals(Sib lhs, Sib rhs) => lhs.Equals(rhs);
		public static bool operator ==(Sib lhs, Sib rhs) => Equals(lhs, rhs);
		public static bool operator !=(Sib lhs, Sib rhs) => !Equals(lhs, rhs);

		public override string ToString()
			=> new string(new char[] { (char)('0' + (byte)Scale), ':', (char)('0' + Index), ':', (char)('0' + Base) });
		
		public static implicit operator byte(Sib sib) => sib.Value;
		public static explicit operator Sib(byte value) => new Sib(value);
	}
}
