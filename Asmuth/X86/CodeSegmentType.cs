using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	public enum CodeSegmentType : byte
	{
		// Code segment flags L = 0, D = 0
		// Defaults to 16-bits addresses/operands, overridable to 32-bits
		// Real 8086, Virtual 8086, System Management
		// Protected/Compatibility Mode (16-bit code segment flag)
		_16Bits,

		// Code segment flags L = 0, D = 1
		// Defaults to 32-bits addresses/operands, overridable to 16-bits
		// Protected/Compatibility Mode (32-bit code segment flag)
		_32Bits,

		// Code segment flags L = 1, D = 0
		// Defaults to 64-bits addresses, overridable to 32-bits
		// Defaults to 32-bits operands, overridable to 16-bits
		// Long Mode
		_64Bits
	}

	public static class CodeSegmentTypeEnum
	{
		#region OperandSize
		public static IntegerSize GetDefaultIntegerOperandSize(this CodeSegmentType type)
			=> type == CodeSegmentType._16Bits ? IntegerSize.Word : IntegerSize.Dword;

		public static IntegerSize GetIntegerOperandSize(this CodeSegmentType type,
			ImmutableLegacyPrefixList legacyPrefixes, NonLegacyPrefixes nonLegacyPrefixes)
		{
			return GetIntegerOperandSize(type,
				@override: legacyPrefixes.HasOperandSizeOverride,
				rexW: nonLegacyPrefixes.OperandSize64);
		}
		public static IntegerSize GetIntegerOperandSize(this CodeSegmentType type,
			bool @override, bool rexW)
		{
			if (rexW)
			{
				if (type != CodeSegmentType._64Bits) throw new ArgumentException();
				// 2.2.1.2
				// • For non-byte operations: if a 66H prefix is used with prefix (REX.W = 1),
				//   66H is ignored.
				// • If a 66H override is used with REX and REX.W = 0, the operand size is 16 bits.
				return IntegerSize.Qword;
			}

			return (type == CodeSegmentType._16Bits) == @override
				? IntegerSize.Dword : IntegerSize.Word;
		}

		public static IntegerSize GetWordOrDwordIntegerOperandSize(this CodeSegmentType type,
			ImmutableLegacyPrefixList legacyPrefixes, NonLegacyPrefixes nonLegacyPrefixes)
		{
			return GetWordOrDwordIntegerOperandSize(type,
				@override: legacyPrefixes.HasOperandSizeOverride,
				rexW: nonLegacyPrefixes.OperandSize64);
		}
		public static IntegerSize GetWordOrDwordIntegerOperandSize(this CodeSegmentType type,
			bool @override, bool rexW)
		{
			if (rexW)
			{
				if (type != CodeSegmentType._64Bits) throw new ArgumentException();
				// 2.2.1.2
				// • For non-byte operations: if a 66H prefix is used with prefix (REX.W = 1),
				//   66H is ignored.
				// • If a 66H override is used with REX and REX.W = 0, the operand size is 16 bits.
				return IntegerSize.Dword;
			}

			return (type == CodeSegmentType._16Bits) == @override
				? IntegerSize.Dword : IntegerSize.Word;
		}
		#endregion

		#region AddressSize
		public static AddressSize GetDefaultAddressSize(this CodeSegmentType type)
			=> (AddressSize)type;
		public static AddressSize GetEffectiveAddressSize(this CodeSegmentType type, ImmutableLegacyPrefixList legacyPrefixes)
			=> GetEffectiveAddressSize(type, @override: legacyPrefixes.HasAddressSizeOverride);
		public static AddressSize GetEffectiveAddressSize(this CodeSegmentType type, bool @override)
		{
			if (type == CodeSegmentType._16Bits) return @override ? AddressSize._32Bits : AddressSize._16Bits;
			if (type == CodeSegmentType._32Bits) return @override ? AddressSize._16Bits : AddressSize._32Bits;
			if (type == CodeSegmentType._64Bits) return @override ? AddressSize._32Bits : AddressSize._64Bits;
			throw new ArgumentOutOfRangeException(nameof(type));
		}
		public static bool Supports(this CodeSegmentType type, AddressSize addressSize)
			=> type == CodeSegmentType._64Bits 
			? (addressSize != AddressSize._16Bits) : (addressSize != AddressSize._64Bits);
		public static bool Supports(this CodeSegmentType type, AddressSize addressSize,
			out bool withOverride)
		{
			bool supported = Supports(type, addressSize);
			withOverride = supported && GetDefaultAddressSize(type) == addressSize;
			return supported;
		}
		#endregion

		public static bool IsIA32(this CodeSegmentType type)
			=> type != CodeSegmentType._64Bits;

		public static bool IsLongMode(this CodeSegmentType type)
			=> type == CodeSegmentType._64Bits;
	}
}
