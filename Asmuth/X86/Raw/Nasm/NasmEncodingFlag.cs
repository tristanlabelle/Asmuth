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
	public enum NasmEncodingFlag : byte
	{
		None = 0,

		Value_Shift = 0,
		Value_Mask = 0x1F << Value_Shift,

		Category_Shift = 5,
		Category_Misc = 0 << Category_Shift,
		Category_AddressSize = 1 << Category_Shift,
		Category_OperandSize = 2 << Category_Shift,
		Category_LegacyPrefix = 3 << Category_Shift,
		Category_Rex = 4 << Category_Shift,
		Category_Immediate = 5 << Category_Shift,
		Category_Mask = 7 << Category_Shift,

		[NasmName("a16")]
		AddressSize_Fixed16 = Category_AddressSize | 0,
		[NasmName("a32")]
		AddressSize_Fixed32 = Category_AddressSize | 1,
		[NasmName("a64")]
		AddressSize_Fixed64 = Category_AddressSize | 2,
		[NasmName("adf")]
		AddressSize_NoOverride = Category_AddressSize | 3,

		[NasmName("o16")]
		OperandSize_Fixed16 = Category_OperandSize | 0,
		[NasmName("o32")]
		OperandSize_Fixed32 = Category_OperandSize | 1,
		[NasmName("o64")]
		OperandSize_Fixed64 = Category_OperandSize | 2,
		[NasmName("o64nw")]
		OperandSize_Fixed64_RexExtensionsOnly = Category_OperandSize | 3,
		[NasmName("odf")]
		OperandSize_NoOverride = Category_OperandSize | 4,

		[NasmName("np")]
		LegacyPrefix_None = Category_LegacyPrefix | 0,
		[NasmName("f2i")]
		LegacyPrefix_F2 = Category_LegacyPrefix | 1,
		[NasmName("f3i")]
		LegacyPrefix_F3 = Category_LegacyPrefix | 2,
		[NasmName("nof3")]
		LegacyPrefix_NoF3 = Category_LegacyPrefix | 3,
		[NasmName("hle")]
		LegacyPrefix_HleWithLock = Category_LegacyPrefix | 4,
		[NasmName("hlenl")]
		LegacyPrefix_HleAlways = Category_LegacyPrefix | 5,
		[NasmName("hlexr")]
		LegacyPrefix_XReleaseAlways = Category_LegacyPrefix | 6,
		[NasmName("mustrep")]
		LegacyPrefix_MustRep = Category_LegacyPrefix | 7,
		[NasmName("repe")]
		LegacyPrefix_RepE = Category_LegacyPrefix | 8,
		[NasmName("norep")]
		LegacyPrefix_NoRep = Category_LegacyPrefix | 9,

		[NasmName("norexb")]
		Rex_NoB = Category_Rex | 0,
		[NasmName("norexw")]
		Rex_NoW = Category_Rex | 1,

		[NasmName("ib")]
		Immediate_Byte = Category_Immediate | 0,
		[NasmName("ib,s")]
		Immediate_Byte_Signed = Category_Immediate | 1,
		[NasmName("ib,u")]
		Immediate_Byte_Unsigned = Category_Immediate | 2,
		[NasmName("iw")]
		Immediate_Word = Category_Immediate | 3,
		[NasmName("id")]
		Immediate_Dword = Category_Immediate | 4,
		[NasmName("id,s")]
		Immediate_Dword_Signed = Category_Immediate | 5,
		[NasmName("iq")]
		Immediate_Qword = Category_Immediate | 6,
		[NasmName("iwd")]
		Immediate_WordOrDword = Category_Immediate | 7,
		[NasmName("iwdq")]
		Immediate_WordOrDwordOrQword = Category_Immediate | 8,
		[NasmName("/is4")]
		Immediate_Is4 = Category_Immediate | 9,
		[NasmName("seg")]
		Immediate_Seg = Category_Immediate | 10,
        [NasmName("rel")]
		Immediate_RelativeOffset = Category_Immediate | 11,
		[NasmName("rel8")]
		Immediate_RelativeOffset8 = Category_Immediate | 12,

		//VM32x = NoRexW << 1,
		//VM64x = VM32x << 1,
		//Wait = VM64x << 1,
		//Jmp8 = Wait << 1,
		//NoHi = Jmp8 << 1,
	}

	public static class NasmEncodingFlagsEnum
	{
		private static Dictionary<string, NasmEncodingFlag> namesToFlags;

		[Pure]
		public static NasmEncodingFlag TryFromNasmName(string name)
		{
			Contract.Requires(name != null);

			LazyInitializeDictionaries();
			NasmEncodingFlag encodingFlag;
			namesToFlags.TryGetValue(name, out encodingFlag);
			return encodingFlag;
		}

		private static void LazyInitializeDictionaries()
		{
			if (NasmEncodingFlagsEnum.namesToFlags != null) return;

			var namesToFlags = NasmNameAttribute.BuildNamesToEnumsDictionary<NasmEncodingFlag>();
			Interlocked.MemoryBarrier();
			NasmEncodingFlagsEnum.namesToFlags = namesToFlags;
        }
	}
}
