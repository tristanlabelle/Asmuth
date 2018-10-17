using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86
{
	/// <summary>
	/// Specifies how an opcode encodes adressing forms, as one of:
	/// no encoding, main byte-embedded register or a ModR/M and potential SIB byte.
	/// </summary>
	public readonly struct AddressingFormEncoding : IEquatable<AddressingFormEncoding>
	{
		private readonly byte data;

		private AddressingFormEncoding(byte value) => this.data = value;
		public AddressingFormEncoding(ModRMEncoding modRM) => this.data = (byte)(modRM.data + 2);

		public bool IsNone => data == 0;
		public bool IsMainByteEmbeddedRegister => data == 1;
		public bool HasModRM => data >= 2;
		public byte MainByteMask => IsMainByteEmbeddedRegister ? (byte)0xF8 : (byte)0xFF;
		public ModRMEncoding? ModRM => data >= 2 ? new ModRMEncoding((byte)(data - 2)) : (ModRMEncoding?)null;

		public bool IsValid(ModRM modRM) => HasModRM && ModRM.Value.IsValid(modRM);
		public bool IsValid(ModRM? modRM) => modRM.HasValue ? IsValid(modRM.Value) : !HasModRM;

		public bool Equals(AddressingFormEncoding other) => data == other.data;
		public override bool Equals(object obj) => obj is AddressingFormEncoding && Equals((AddressingFormEncoding)obj);
		public override int GetHashCode() => data;
		public static bool Equals(AddressingFormEncoding lhs, AddressingFormEncoding rhs) => lhs.Equals(rhs);
		public static bool operator ==(AddressingFormEncoding lhs, AddressingFormEncoding rhs) => Equals(lhs, rhs);
		public static bool operator !=(AddressingFormEncoding lhs, AddressingFormEncoding rhs) => !Equals(lhs, rhs);

		public override string ToString()
		{
			if (IsNone) return string.Empty;
			if (IsMainByteEmbeddedRegister) return "+r";
			return ModRM.Value.ToString();
		}

		public static readonly AddressingFormEncoding None = new AddressingFormEncoding(0);
		public static readonly AddressingFormEncoding MainByteEmbeddedRegister = new AddressingFormEncoding(1);
		public static readonly AddressingFormEncoding AnyModRM = ModRMEncoding.Any;

		public static SetComparisonResult Compare(AddressingFormEncoding lhs, AddressingFormEncoding rhs)
		{
			if (lhs.HasModRM && rhs.HasModRM) return ModRMEncoding.Compare(lhs.ModRM.Value, rhs.ModRM.Value);
			return lhs.data == rhs.data ? SetComparisonResult.Equal : SetComparisonResult.Overlapping;
		}

		public static implicit operator AddressingFormEncoding(ModRMEncoding modRM)
			=> new AddressingFormEncoding(modRM);
	}
}
