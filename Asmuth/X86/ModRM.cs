using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	public enum ModRMMod : byte
	{
		Indirect,
		IndirectDisp8,
		IndirectLongDisp,
		Direct
	}

	[StructLayout(LayoutKind.Sequential, Size = sizeof(byte))]
	public readonly struct ModRM : IEquatable<ModRM>
	{
		public const byte SibRM = 4;
		public const byte AbsoluteRM_16 = 6;
		public const byte AbsoluteRM_32 = 5;
		public const byte RipRelativeRM_64 = 5;

		public readonly byte Value;

		public ModRM(byte value) => Value = value;
		public ModRM(ModRMMod mod, byte reg, byte rm)
		{
			if ((byte)mod > 3) throw new ArgumentOutOfRangeException(nameof(mod));
			if (reg > 7) throw new ArgumentOutOfRangeException(nameof(reg));
			if (rm > 7) throw new ArgumentOutOfRangeException(nameof(rm));
			Value = (byte)(((byte)mod << 6) | (reg << 3) | rm);
		}
		public ModRM(ModRMMod mod, GprCode reg, GprCode rm)
			: this(mod, (byte)reg, (byte)rm) { }
		
		public ModRMMod Mod => (ModRMMod)(Value >> 6);
		public byte Reg => (byte)((Value >> 3) & 7);
		public GprCode RegGpr => (GprCode)Reg;
		public byte RM => (byte)(Value & 7);
		public GprCode RMGpr => (GprCode)RM;

		public bool IsIndirect => Mod != ModRMMod.Direct;
		public bool IsDirect => Mod == ModRMMod.Direct;
		public bool IsMemoryRM => IsIndirect;
		public bool IsRegRM => IsDirect;
		public bool IsAbsoluteRM_16 => Mod == ModRMMod.Indirect && RM == AbsoluteRM_16;
		public bool IsAbsoluteRM_32 => Mod == ModRMMod.Indirect && RM == AbsoluteRM_32;
		public bool IsRipRelativeRM_64 => IsAbsoluteRM_32;

		public bool IsAbsoluteRM(AddressSize addressSize)
		{
			if (addressSize == AddressSize._16Bits) return IsAbsoluteRM_16;
			if (addressSize == AddressSize._32Bits) return IsAbsoluteRM_32;
			return false;
		}

		public bool ImpliesSib(AddressSize addressSize)
			=> addressSize >= AddressSize._32Bits && IsIndirect && RM == 4;
		
		public DisplacementSize GetDisplacementSize(AddressSize addressSize, Sib sib)
		{
			switch (Mod)
			{
				case ModRMMod.IndirectDisp8:
					return DisplacementSize._8Bits;
				case ModRMMod.IndirectLongDisp:
					return addressSize == AddressSize._16Bits
						? DisplacementSize._16Bits : DisplacementSize._32Bits;
				case ModRMMod.Direct: return DisplacementSize.None;
			}

			// Mod = 0
			if (addressSize == AddressSize._16Bits)
				return RM == 6 ? DisplacementSize._16Bits : DisplacementSize.None;

			if (RM == 5) return DisplacementSize._32Bits;

			// 32-bit mode, mod = 0, RM = 6 (sib byte)
			return (sib & Sib.Base_Mask) == Sib.Base_Special
				? DisplacementSize._32Bits : DisplacementSize.None;
		}

		public bool Equals(ModRM other) => Value == other.Value;
		public override bool Equals(object obj) => obj is ModRM && Equals((ModRM)obj);
		public override int GetHashCode() => Value;
		public static bool Equals(ModRM lhs, ModRM rhs) => lhs.Equals(rhs);
		public static bool operator ==(ModRM lhs, ModRM rhs) => Equals(lhs, rhs);
		public static bool operator !=(ModRM lhs, ModRM rhs) => !Equals(lhs, rhs);

		public override string ToString()
			=> new string(new char[] { (char)('0' + (byte)Mod), ':', (char)('0' + Reg), ':', (char)('0' + RM) });

		public static ModRM WithAbsoluteRM_16(byte reg)
		{
			if (reg > 7) throw new ArgumentOutOfRangeException(nameof(reg));
			return new ModRM((byte)((reg << 3) | AbsoluteRM_16));
		}

		public static ModRM WithAbsoluteRM_32(byte reg)
		{
			if (reg > 7) throw new ArgumentOutOfRangeException(nameof(reg));
			return new ModRM((byte)((reg << 3) | AbsoluteRM_32));
		}

		public static ModRM WithRipRelativeRM_64(byte reg)
			=> WithAbsoluteRM_32(reg);

		public static ModRM WithDirectRM(byte reg, byte rm)
		{
			if (reg > 7) throw new ArgumentOutOfRangeException(nameof(reg));
			if (rm > 7) throw new ArgumentOutOfRangeException(nameof(rm));
			return new ModRM((byte)(0b11_000_000 | (reg << 3) | rm));
		}

		public static ModRM WithDirectRM(GprCode reg, GprCode rm)
			=> WithDirectRM((byte)reg, (byte)rm);

		public static ModRM WithSib(ModRMMod mod, byte reg)
		{
			if (mod >= ModRMMod.Direct) throw new ArgumentOutOfRangeException(nameof(mod));
			if (reg > 7) throw new ArgumentOutOfRangeException(nameof(reg));
			return new ModRM(mod, reg, SibRM);
		}

		public static ModRM FromReg(byte reg)
		{
			if (reg > 7) throw new ArgumentOutOfRangeException(nameof(reg));
			return new ModRM((byte)(reg << 3));
		}

		public static implicit operator byte(ModRM modRM) => modRM.Value;
		public static implicit operator ModRM(byte value) => new ModRM(value);
	}
}
