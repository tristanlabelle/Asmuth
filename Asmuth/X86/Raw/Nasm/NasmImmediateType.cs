using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw.Nasm
{
	public enum NasmImmediateType : byte
	{
		[NasmName("ib")]
		Byte,
		[NasmName("ib,s")]
		Byte_Signed,
		[NasmName("ib,u")]
		Byte_Unsigned,
		[NasmName("iw")]
		Word,
		[NasmName("id")]
		Dword,
		[NasmName("id,s")]
		Dword_Signed,
		[NasmName("iq")]
		Qword,
		[NasmName("iwd")]
		WordOrDword,
		[NasmName("iwdq")]
		WordOrDwordOrQword,
		[NasmName("/is4")]
		Is4,
		[NasmName("seg")]
		Segment,
		[NasmName("rel")]
		RelativeOffset,
		[NasmName("rel8")]
		RelativeOffset8,
	}

	public static class NasmImmediateTypeEnum
	{
		[Pure]
		public static NasmImmediateType? TryFromNasmName(string name)
			=> NasmEnum<NasmImmediateType>.GetEnumerantOrNull(name);
	}
}
