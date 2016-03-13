using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	public enum CodeContext : byte
	{
		Real8086,
		Virtual8086,
		SystemManagement,

		Protected_Default16,
		Protected_Default32,

		Compatibility_Default16,
		Compatibility_Default32,
		
		SixtyFourBit
	}

	public static class CodeContextEnum
	{
		#region OperandSize
		[Pure]
		[Obsolete]
		public static OperandSize GetDefaultOperandSize(this CodeContext context)
		{
			return context == CodeContext.Protected_Default32
				|| context == CodeContext.Compatibility_Default32
				|| context == CodeContext.SixtyFourBit
				? OperandSize.Dword : OperandSize.Word;
		}

		[Pure]
		[Obsolete]
		public static OperandSize GetEffectiveOperandSize(this CodeContext context,
			ImmutableLegacyPrefixList legacyPrefixes, Xex xex)
		{
			return GetEffectiveOperandSize(context,
				@override: legacyPrefixes.HasOperandSizeOverride,
				rexW: xex.OperandSize64);
		}

		[Pure]
		[Obsolete]
		public static OperandSize GetEffectiveOperandSize(this CodeContext context, bool @override, bool rexW)
		{
			if (rexW)
			{
				if (context != CodeContext.SixtyFourBit) throw new ArgumentException();
				return OperandSize.Qword;
			}

			return GetDefaultOperandSize(context).OverrideWordDword(@override);
		} 
		#endregion

		#region AddressSize
		[Pure]
		public static AddressSize GetDefaultAddressSize(this CodeContext context)
		{
			switch (context)
			{
				case CodeContext.Real8086:
				case CodeContext.Virtual8086:
				case CodeContext.SystemManagement:
				case CodeContext.Protected_Default16:
				case CodeContext.Compatibility_Default16:
					return AddressSize._16;

				case CodeContext.Protected_Default32:
				case CodeContext.Compatibility_Default32:
					return AddressSize._32;

				case CodeContext.SixtyFourBit:
					return AddressSize._64;

				default: throw new ArgumentOutOfRangeException(nameof(context));
			}
		}

		[Pure]
		public static AddressSize GetEffectiveAddressSize(this CodeContext mode, ImmutableLegacyPrefixList legacyPrefixes)
			=> GetEffectiveAddressSize(mode, @override: legacyPrefixes.HasAddressSizeOverride);

		[Pure]
		public static AddressSize GetEffectiveAddressSize(this CodeContext mode, bool @override)
			=> GetDefaultAddressSize(mode).GetEffective(@override);
		#endregion

		[Pure]
		public static bool IsRealOrVirtual8086(this CodeContext context)
		{
			return context == CodeContext.Real8086
				|| context == CodeContext.Virtual8086;
		}

		[Pure]
		public static bool IsCompatibility(this CodeContext context)
		{
			return context == CodeContext.Compatibility_Default16
				|| context == CodeContext.Compatibility_Default32;
		}

		[Pure]
		public static bool IsProtected(this CodeContext context)
		{
			return context == CodeContext.Protected_Default16
				|| context == CodeContext.Protected_Default32;
		}

		[Pure]
		public static bool IsProtectedOrCompatibility(this CodeContext context)
			=> IsProtected(context) || IsCompatibility(context);

		[Pure]
		public static bool IsIA32e(this CodeContext context)
			=> IsCompatibility(context) || context == CodeContext.SixtyFourBit;

		[Pure]
		public static CodeContext GetIA32e(AddressSize defaultAddressSize)
		{
			switch (defaultAddressSize)
			{
				case AddressSize._16: return CodeContext.Compatibility_Default16;
				case AddressSize._32: return CodeContext.Compatibility_Default32;
				case AddressSize._64: return CodeContext.SixtyFourBit;
				default: throw new ArgumentOutOfRangeException(nameof(defaultAddressSize));
			}
		}
	}
}
