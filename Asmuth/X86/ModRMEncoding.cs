using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Asmuth.X86
{
	public enum ModRMModEncoding : byte
	{
		RegisterOrMemory,
		Register, // MOD = 11, RM = Register operand
		Memory, // MOD != 11, RM = Memory operand
		DirectFixedRM // MOD == 11, RM = Fixed value
	}
	
	public static class ModRMModEncodingEnum
	{
		public static bool CanEncodeRegister(this ModRMModEncoding mod)
			=> (byte)mod <= 1;

		public static bool CanEncodeMemory(this ModRMModEncoding mod)
			=> ((byte)mod & 1) == 0;

		public static bool CanHaveSibByte(this ModRMModEncoding mod)
			=> CanEncodeMemory(mod);

		public static bool IsValid(this ModRMModEncoding encoding, ModRMMod mod)
		{
			if (encoding == ModRMModEncoding.RegisterOrMemory) return true;
			return (encoding == ModRMModEncoding.Memory) != (mod == ModRMMod.Direct);
		}

		public static bool IsValid(this ModRMModEncoding encoding, ModRM modRM, byte fixedRM)
		{
			if (encoding == ModRMModEncoding.DirectFixedRM)
				return modRM.Mod == ModRMMod.Direct && modRM.RM == fixedRM;
			return IsValid(encoding, modRM.Mod);
		}
	}

	public readonly struct ModRMEncoding : IEquatable<ModRMEncoding>
	{
		// High nibble (Mod & RM):
		//   0: register or memory
		//   1: memory (mod == 11)
		//   2: register (mod != 11)
		//   3+: direct with fixed rm+3
		// Low nibble (reg):
		//   0: any reg
		//   1+: fixed value+1
		internal readonly byte data;

		internal ModRMEncoding(byte data) => this.data = data;

		public ModRMEncoding(ModRMModEncoding mod, byte? reg = null, byte rm = 0)
		{
			if (reg.HasValue)
			{
				if (reg.Value > 7) throw new ArgumentOutOfRangeException(nameof(reg));
				data = (byte)(reg.Value + 1);
			}
			else
			{
				data = 0;
			}

			if (mod == ModRMModEncoding.DirectFixedRM)
			{
				if (rm > 7) throw new ArgumentOutOfRangeException(nameof(rm));
				data |= (byte)((rm + 3) << 4);
			}
			else
			{
				if (mod >= ModRMModEncoding.DirectFixedRM)
					throw new ArgumentOutOfRangeException(nameof(mod));
				data |= (byte)((byte)mod << 4);
			}
		}

		private byte RegNibble => (byte)(data & 0xF);
		private byte RMNibble => (byte)(data >> 4);
		
		public byte? FixedReg => RegNibble >= 1 ? (byte?)(RegNibble - 1) : null;

		public ModRMModEncoding Mod => RMNibble >= 3
			? ModRMModEncoding.DirectFixedRM
			: (ModRMModEncoding)RMNibble;

		public byte? FixedRM => RMNibble >= 3 ? (byte?)(RMNibble - 3) : null;
		public ModRM? FixedValue => RMNibble >= 3 && RegNibble >= 1
			? (ModRM?)(0b11_000_000 | ((RegNibble - 1) << 3) | (RMNibble - 3)) : null;

		public bool IsValid(ModRM modRM)
		{
			return Mod.IsValid(modRM, fixedRM: (byte)(RMNibble - 3))
				&& FixedReg.GetValueOrDefault(modRM.Reg) == modRM.Reg;
		}

		public bool Equals(ModRMEncoding other) => data == other.data;
		public override bool Equals(object obj) => obj is ModRMEncoding && Equals((ModRMEncoding)obj);
		public override int GetHashCode() => data;
		public static bool Equals(ModRMEncoding lhs, ModRMEncoding rhs) => lhs.Equals(rhs);
		public static bool operator ==(ModRMEncoding lhs, ModRMEncoding rhs) => Equals(lhs, rhs);
		public static bool operator !=(ModRMEncoding lhs, ModRMEncoding rhs) => !Equals(lhs, rhs);

		public override string ToString()
		{
			if (FixedReg.HasValue && !Mod.CanEncodeMemory())
			{
				var value = (byte)new ModRM(
					mod: ModRMMod.Direct, reg: FixedReg.Value, rm: FixedRM.GetValueOrDefault());
				var str = value.ToString("x2", CultureInfo.InvariantCulture);
				if (Mod == ModRMModEncoding.Register) str += "+r";
				return str;
			}
			else
			{
				var str = "/";
				if (FixedReg.HasValue) str += (char)('0' + FixedReg.Value);
				else str += 'r';
				if (Mod == ModRMModEncoding.Memory) str += 'm';
				else if (Mod == ModRMModEncoding.Register) str += 'r';
				return str;
			}
		}
		
		public static ModRMEncoding FromFixedValue(ModRM value)
		{
			if (value.IsIndirect) throw new ArgumentOutOfRangeException(nameof(value));
			return new ModRMEncoding(
				(byte)(((value.RM + 3) << 4) | (value.Reg + 1)));
		}

		public static ModRMEncoding FromFixedValue(byte value) => FromFixedValue((ModRM)value);

		public static SetComparisonResult Compare(ModRMEncoding lhs, ModRMEncoding rhs)
			=> CompareReg(lhs.FixedReg, rhs.FixedReg).Combine(CompareRM(lhs, rhs));

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
			if (lhs.Mod == ModRMModEncoding.RegisterOrMemory && rhs.Mod == ModRMModEncoding.RegisterOrMemory)
				return SetComparisonResult.Equal;
			if (lhs.Mod == ModRMModEncoding.RegisterOrMemory)
				return SetComparisonResult.SupersetSubset;
			if (rhs.Mod == ModRMModEncoding.RegisterOrMemory)
				return SetComparisonResult.SubsetSuperset;

			// Handle indirect ModRMs
			if (lhs.Mod.CanEncodeMemory() != rhs.Mod.CanEncodeMemory()) return SetComparisonResult.Disjoint;
			if (lhs.Mod == ModRMModEncoding.Memory) return SetComparisonResult.Equal;

			// Handle fixed vs direct
			if (!lhs.FixedRM.HasValue && rhs.FixedRM.HasValue) return SetComparisonResult.SupersetSubset;
			if (lhs.FixedRM.HasValue && !rhs.FixedRM.HasValue) return SetComparisonResult.SubsetSuperset;
			
			return lhs.FixedRM == rhs.FixedRM
				? SetComparisonResult.Equal : SetComparisonResult.Disjoint;
		}
		
		public static readonly ModRMEncoding Any = new ModRMEncoding(0x00);
		public static readonly ModRMEncoding RegRM_AnyReg = new ModRMEncoding(0x10);
		public static readonly ModRMEncoding MemRM_AnyReg = new ModRMEncoding(0x20);
	}
}
