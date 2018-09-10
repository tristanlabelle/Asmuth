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
		IA32_Default16,

		// Code segment flags L = 0, D = 1
		// Defaults to 32-bits addresses/operands, overridable to 16-bits
		// Protected/Compatibility Mode (32-bit code segment flag)
		IA32_Default32,

		// Code segment flags L = 1, D = 0
		// Defaults to 64-bits addresses, overridable to 32-bits
		// Defaults to 32-bits operands, overridable to 16-bits
		// Intel64 / Long Mode
		X64
	}

	public static class CodeSegmentTypeEnum
	{
		#region OperandSize
		public static IntegerSize GetDefaultIntegerOperandSize(this CodeSegmentType type)
			=> type == CodeSegmentType.IA32_Default16 ? IntegerSize.Word : IntegerSize.Dword;

		public static IntegerSize GetIntegerOperandSize(this CodeSegmentType type,
			ImmutableLegacyPrefixList legacyPrefixes, NonLegacyPrefixes nonLegacyPrefixes)
		{
			return GetIntegerOperandSize(type,
				@override: legacyPrefixes.HasOperandSizeOverride,
				promotion: nonLegacyPrefixes.OperandSizePromotion);
		}
		public static IntegerSize GetIntegerOperandSize(this CodeSegmentType type,
			bool @override, bool promotion)
		{
			if (promotion)
			{
				if (type != CodeSegmentType.X64) throw new ArgumentException();
				// 2.2.1.2
				// • For non-byte operations: if a 66H prefix is used with prefix (REX.W = 1),
				//   66H is ignored.
				// • If a 66H override is used with REX and REX.W = 0, the operand size is 16 bits.
				return IntegerSize.Qword;
			}

			return (type == CodeSegmentType.IA32_Default16) == @override
				? IntegerSize.Dword : IntegerSize.Word;
		}

		public static IntegerSize GetWordOrDwordIntegerOperandSize(this CodeSegmentType type,
			ImmutableLegacyPrefixList legacyPrefixes, NonLegacyPrefixes nonLegacyPrefixes)
		{
			return GetWordOrDwordIntegerOperandSize(type,
				@override: legacyPrefixes.HasOperandSizeOverride,
				promotion: nonLegacyPrefixes.OperandSizePromotion);
		}
		public static IntegerSize GetWordOrDwordIntegerOperandSize(this CodeSegmentType type,
			bool @override, bool promotion)
		{
			if (promotion)
			{
				if (type != CodeSegmentType.X64) throw new ArgumentException();
				// 2.2.1.2
				// • For non-byte operations: if a 66H prefix is used with prefix (REX.W = 1),
				//   66H is ignored.
				// • If a 66H override is used with REX and REX.W = 0, the operand size is 16 bits.
				return IntegerSize.Dword;
			}

			return (type == CodeSegmentType.IA32_Default16) == @override
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
			if (type == CodeSegmentType.IA32_Default16) return @override ? AddressSize._32 : AddressSize._16;
			if (type == CodeSegmentType.IA32_Default32) return @override ? AddressSize._16 : AddressSize._32;
			if (type == CodeSegmentType.X64) return @override ? AddressSize._32 : AddressSize._64;
			throw new ArgumentOutOfRangeException(nameof(type));
		}
		public static bool Supports(this CodeSegmentType type, AddressSize addressSize)
			=> type == CodeSegmentType.X64 
			? (addressSize != AddressSize._16) : (addressSize != AddressSize._64);
		public static bool Supports(this CodeSegmentType type, AddressSize addressSize,
			out bool withOverride)
		{
			bool supported = Supports(type, addressSize);
			withOverride = supported && GetDefaultAddressSize(type) == addressSize;
			return supported;
		}
		#endregion

		public static bool IsIA32(this CodeSegmentType type)
			=> type != CodeSegmentType.X64;

		public static bool IsX64(this CodeSegmentType type)
			=> type == CodeSegmentType.X64;
	}
}
