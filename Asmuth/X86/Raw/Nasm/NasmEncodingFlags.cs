using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw.Nasm
{
	[Flags]
	public enum NasmEncodingFlags : uint
	{
		None = 0,

		XexForm_Shift = 0,
		XexForm_Legacy = 0 << (int)XexForm_Shift,
		XexForm_Vex = 1 << (int)XexForm_Shift,
		XexForm_Xop = 2 << (int)XexForm_Shift,
		XexForm_EVex = 3 << (int)XexForm_Shift,
		XexForm_Mask = 3 << (int)XexForm_Shift,

		AddressSize_Shift = XexForm_Shift + 2,
		AddressSize_Unspecified = 0 << (int)AddressSize_Shift,
		[NasmName("a16")]
		AddressSize_Fixed16 = 1 << (int)AddressSize_Shift,
		[NasmName("a32")]
		AddressSize_Fixed32 = 2 << (int)AddressSize_Shift,
		[NasmName("a64")]
		AddressSize_Fixed64 = 3 << (int)AddressSize_Shift,
		[NasmName("adf")]
		AddressSize_NoOverride = 4 << (int)AddressSize_Shift,
		AddressSize_Mask = 7 << (int)AddressSize_Shift,

		OperandSize_Shift = AddressSize_Shift + 3,
		OperandSize_Unspecified = 0 << (int)OperandSize_Shift,
		[NasmName("o16")]
		OperandSize_Fixed16 = 1 << (int)OperandSize_Shift,
		[NasmName("o32")]
		OperandSize_Fixed32 = 2 << (int)OperandSize_Shift,
		[NasmName("o64")]
		OperandSize_Fixed64 = 3 << (int)OperandSize_Shift,
		[NasmName("odf")]
		OperandSize_NoOverride = 4 << (int)OperandSize_Shift,
		OperandSize_Mask = 7 << (int)OperandSize_Shift,

		LegacyPrefix_Shift = OperandSize_Shift + 3,
		LegacyPrefix_Unspecified = 0 << (int)LegacyPrefix_Shift, 
		[NasmName("np")]
		LegacyPrefix_None = 1 << (int)LegacyPrefix_Shift,
		[NasmName("f2i")]
		LegacyPrefix_F2 = 2 << (int)LegacyPrefix_Shift,
		[NasmName("f3i")]
		LegacyPrefix_F3 = 3 << (int)LegacyPrefix_Shift,
		[NasmName("nof3")]
		LegacyPrefix_NoF3 = 4 << (int)LegacyPrefix_Shift,
		[NasmName("hle")]
		LegacyPrefix_HleWithLock = 5 << (int)LegacyPrefix_Shift,
		[NasmName("hlenl")]
		LegacyPrefix_HleAlways = 6 << (int)LegacyPrefix_Shift,
		[NasmName("hlexr")]
		LegacyPrefix_XReleaseAlways = 7 << (int)LegacyPrefix_Shift,
		[NasmName("mustrep")]
		LegacyPrefix_MustRep = 8 << (int)LegacyPrefix_Shift,
		[NasmName("repe")]
		LegacyPrefix_RepE = 9 << (int)LegacyPrefix_Shift,
		[NasmName("norep")]
		LegacyPrefix_NoRep = 10 << (int)LegacyPrefix_Shift,
		LegacyPrefix_Mask = 0xF << (int)LegacyPrefix_Shift,

		Rex_Shift = LegacyPrefix_Shift + 4,
		Rex_Unspecified = 0 << (int)Rex_Shift,
		[NasmName("norexb")]
		Rex_NoB = 1 << (int)Rex_Shift,
		[NasmName("norexw")]
		Rex_NoW = 2 << (int)Rex_Shift,
		Rex_Mask = 3 << (int)Rex_Shift,

		Jump_Shift = LegacyPrefix_Shift + 2,
		Jump_Unspecified = 0 << (int)Jump_Shift,
		[NasmName("jmp8")]
		Jump_8 = 1 << (int)Jump_Shift,
		[NasmName("jcc8")]
		Jump_Conditional8 = 2 << (int)Jump_Shift,
		[NasmName("jlen")]
		Jump_Length = 3 << (int)Jump_Shift,
		Jump_Mask = 3 << (int)Jump_Shift,

		[NasmName("vm32x")]
		Misc_VM32x = 1 << (int)(Rex_Shift + 2),
		[NasmName("vm64x")]
		Misc_VM64x = Misc_VM32x << 1,
		[NasmName("wait")]
		Misc_Wait = Misc_VM64x << 1,
		[NasmName("nohi")]
		Misc_NoHi = Misc_Wait << 1,
		[NasmName("o64nw")]
		Misc_Fixed64_RexExtensionsOnly = Misc_NoHi << 1,
		[NasmName("rex.l")]
		Misc_LockAsRexR = Misc_Fixed64_RexExtensionsOnly << 1,
	}

	public static class NasmEncodingFlagsEnum
	{
		[Pure]
		public static NasmEncodingFlags TryFromNasmName(string name)
			=> NasmEnum<NasmEncodingFlags>.GetEnumerantOrDefault(name);
    }
}
