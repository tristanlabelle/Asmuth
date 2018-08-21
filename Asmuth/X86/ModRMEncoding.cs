using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Asmuth.X86
{
	public readonly struct ModRMEncoding : IEquatable<ModRMEncoding>
	{
		// High nibble:
		//   0: special values (no ModRM and MainByteReg)
		//   1: direct (mod == 11)
		//   2: indirect (mod != 11)
		//   3: any
		//   4+: fixed value+4
		// Low nibble:
		//   0: no ModRM
		//   1: main byte reg
		//   2: any reg
		//   3+: fixed value+3
		private readonly byte data;

		private ModRMEncoding(byte data)
		{
			this.data = data;
		}

		public ModRMEncoding(byte? reg, bool rmRegAllowed, bool rmMemAllowed, byte rmValue = 0)
		{
			if (reg.HasValue)
			{
				if (reg.Value > 7) throw new ArgumentOutOfRangeException(nameof(reg));
				data = (byte)(3 + reg.Value);
			}
			else
			{
				data = 2;
			}

			if (rmRegAllowed) data |= 0x10;
			if (rmMemAllowed) data |= 0x20;
			if (!rmRegAllowed && !rmMemAllowed)
			{
				if (rmValue > 7) throw new ArgumentOutOfRangeException(nameof(rmValue));
				data |= (byte)((rmValue + 4) << 4);
			}
		}

		private byte RegNibble => (byte)(data & 0xF);
		private byte RMNibble => (byte)(data >> 4);

		public byte MainByteMask => this == MainByteReg ? (byte)0xF8 : (byte)0xFF;
		public bool IsPresent => RMNibble != 0;
		public byte? Reg => RegNibble >= 3 ? (byte?)(RegNibble - 3) : null;
		public bool AllowsOnlyRegRM => RMNibble == 1;
		public bool AllowsOnlyMemRM => RMNibble == 2;
		public bool AllowsAnyRM => RMNibble == 3;
		public bool AllowsRegRM => AllowsOnlyRegRM || AllowsAnyRM;
		public bool AllowsMemRM => AllowsOnlyMemRM || AllowsAnyRM;
		public bool DirectMod => RMNibble == 1 || RMNibble >= 4;
		public byte? FixedRM => RMNibble > 3 ? (byte?)(RMNibble - 4) : null;
		public ModRM? FixedValue => RMNibble > 3 && RegNibble >= 3
			? (ModRM?)(0b11_000_000 | ((RegNibble - 3) << 3) | (RMNibble - 4)) : null;

		public bool IsValid(ModRM modRM)
		{
			if (RegNibble != 2 && modRM.Reg != (RegNibble - 3)) return false;
			if (modRM.IsDirect ? RMNibble == 2 : DirectMod) return false;
			return RMNibble < 4 || (modRM.RM == RMNibble - 4);
		}

		public bool IsValid(ModRM? modRM)
			=> modRM.HasValue == IsPresent && IsValid(modRM.Value);

		public bool Equals(ModRMEncoding other) => data == other.data;
		public override bool Equals(object obj) => obj is ModRMEncoding && Equals((ModRMEncoding)obj);
		public override int GetHashCode() => data;
		public static bool Equals(ModRMEncoding lhs, ModRMEncoding rhs) => lhs.Equals(rhs);
		public static bool operator ==(ModRMEncoding lhs, ModRMEncoding rhs) => Equals(lhs, rhs);
		public static bool operator !=(ModRMEncoding lhs, ModRMEncoding rhs) => !Equals(lhs, rhs);

		public override string ToString()
		{
			if (this == MainByteReg) return "+r";
			if (!IsPresent) return "";
			
			if (Reg.HasValue && DirectMod)
			{
				var value = (byte)new ModRM(
					mod: ModRMMod.Direct, reg: Reg.Value, rm: FixedRM.GetValueOrDefault());
				var str = value.ToString("x2", CultureInfo.InvariantCulture);
				if (AllowsOnlyRegRM) str += "+r";
				return str;
			}
			else
			{
				var str = AllowsOnlyMemRM ? "m/" : "/";
				if (Reg.HasValue) str += (char)('0' + Reg.Value);
				else str += 'r';
				return str;
			}
		}

		public static ModRMEncoding FromFixedRegAnyRM(byte reg)
		{
			if (reg > 7) throw new ArgumentOutOfRangeException(nameof(reg));
			return new ModRMEncoding((byte)(0x30 | (reg + 3)));
		}

		public static ModRMEncoding FromFixedRegMemRM(byte reg)
		{
			if (reg > 7) throw new ArgumentOutOfRangeException(nameof(reg));
			return new ModRMEncoding((byte)(0x20 | (reg + 3)));
		}

		public static ModRMEncoding FromFixedRegDirectRM(byte reg)
		{
			if (reg > 7) throw new ArgumentOutOfRangeException(nameof(reg));
			return new ModRMEncoding((byte)(0x10 | (reg + 3)));
		}

		public static ModRMEncoding FromFixedValue(ModRM value)
		{
			if (value.IsIndirect) throw new ArgumentOutOfRangeException(nameof(value));
			return new ModRMEncoding(
				(byte)(((value.RM + 4) << 4) | (value.Reg + 3)));
		}

		public static SetComparisonResult Compare(ModRMEncoding lhs, ModRMEncoding rhs)
		{
			// Compare ModRM presence
			if (lhs.IsPresent != rhs.IsPresent) return SetComparisonResult.Overlapping;
			if (!lhs.IsPresent) return SetComparisonResult.Equal;
			return CompareReg(lhs.Reg, rhs.Reg).Combine(CompareRM(lhs, rhs));
		}

		private static SetComparisonResult CompareReg(byte? lhs, byte? rhs)
		{
			if (lhs == rhs) return SetComparisonResult.Equal;
			if (!lhs.HasValue) return SetComparisonResult.SupersetSubset;
			if (!rhs.HasValue) return SetComparisonResult.SubsetSuperset;
			return SetComparisonResult.Disjoint;
		}

		private static SetComparisonResult CompareRM(ModRMEncoding lhs, ModRMEncoding rhs)
		{
			// Handle 'any' ModRMs
			if (lhs.AllowsAnyRM && rhs.AllowsAnyRM)
				return SetComparisonResult.Equal;
			if (lhs.AllowsAnyRM && !rhs.AllowsAnyRM)
				return SetComparisonResult.SupersetSubset;
			if (!lhs.AllowsAnyRM && rhs.AllowsAnyRM)
				return SetComparisonResult.SubsetSuperset;

			// Handle indirect ModRMs
			if (lhs.AllowsOnlyMemRM != rhs.AllowsOnlyMemRM) return SetComparisonResult.Disjoint;
			if (lhs.AllowsOnlyMemRM) return SetComparisonResult.Equal;

			// Handle fixed vs direct
			if (!lhs.FixedRM.HasValue && rhs.FixedRM.HasValue) return SetComparisonResult.SupersetSubset;
			if (lhs.FixedRM.HasValue && !rhs.FixedRM.HasValue) return SetComparisonResult.SubsetSuperset;
			
			return lhs.FixedRM == rhs.FixedRM
				? SetComparisonResult.Equal : SetComparisonResult.Disjoint;
		}

		public static readonly ModRMEncoding None = new ModRMEncoding(0);
		public static readonly ModRMEncoding MainByteReg = new ModRMEncoding(1);
		public static readonly ModRMEncoding Any = new ModRMEncoding(0x32);
		public static readonly ModRMEncoding AnyReg_MemRM = new ModRMEncoding(0x22);
	}
}
