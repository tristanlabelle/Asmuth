using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw.Nasm
{
	[StructLayout(LayoutKind.Sequential, Size = 2)]
	public struct NasmEncodingToken
	{
		public static readonly NasmEncodingToken ModRM = new NasmEncodingToken(NasmEncodingTokenType.ModRM, 0xFF);

		public readonly NasmEncodingTokenType Type;
		public readonly byte Byte;

		public NasmEncodingToken(NasmEncodingTokenType type, byte @byte = 0)
		{
			this.Type = type;
			this.Byte = @byte;
		}

		public static NasmEncodingTokenType TryParseType(string name)
		{
			Contract.Requires(name != null);
			return NasmEnum<NasmEncodingTokenType>.GetEnumerantOrDefault(name);
		}

		public static implicit operator NasmEncodingToken(NasmEncodingTokenType type)
		{
			return new NasmEncodingToken(type);
		}
	}

	[Flags]
	public enum NasmEncodingTokenType : byte
	{
		None = 0,

		Value_Shift = 0,
		Value_Mask = 0xF,

		Category_Shift = 4,
		Category_Vex = 1 << Category_Shift,
		Category_AddressSize = 2 << Category_Shift,
		Category_OperandSize = 3 << Category_Shift,
		Category_LegacyPrefix = 4 << Category_Shift,
		Category_Rex = 5 << Category_Shift,
		Category_Byte = 6 << Category_Shift,
		Category_ModRM = 7 << Category_Shift,
		Category_Jump = 8 << Category_Shift,
		Category_Immediate = 9 << Category_Shift,
		Category_Misc = 15 << Category_Shift,
		Category_Mask = 0xF0,

		Vex = Category_Vex, // value unused, vex data stored as a VexOpcodeEncoding value

		[NasmName("/r")]
		ModRM = Category_ModRM,
		[NasmName("/0")]
		ModRM_R0 = Category_ModRM,
		[NasmName("/1")]
		ModRM_R1,
		[NasmName("/2")]
		ModRM_R2,
		[NasmName("/3")]
		ModRM_R3,
		[NasmName("/4")]
		ModRM_R4,
		[NasmName("/5")]
		ModRM_R5,
		[NasmName("/6")]
		ModRM_R6,
		[NasmName("/7")]
		ModRM_R7,

		[NasmName("a16")]
		AddressSize_Fixed16 = Category_AddressSize,
		[NasmName("a32")]
		AddressSize_Fixed32,
		[NasmName("a64")]
		AddressSize_Fixed64,
		[NasmName("adf")]
		AddressSize_NoOverride,

		[NasmName("o16")]
		OperandSize_Fixed16 = Category_OperandSize,
		[NasmName("o32")]
		OperandSize_Fixed32,
		[NasmName("o64")]
		OperandSize_Fixed64,
		[NasmName("o64nw")]
		OperandSize_Fixed64_RexExtensionsOnly,
		[NasmName("odf")]
		OperandSize_NoOverride,

		[NasmName("np")]
		LegacyPrefix_None = Category_LegacyPrefix,
		[NasmName("f2i")]
		LegacyPrefix_F2,
		[NasmName("f3i")]
		LegacyPrefix_F3,
		[NasmName("nof3")]
		LegacyPrefix_NoF3,
		[NasmName("hle")]
		LegacyPrefix_HleWithLock,
		[NasmName("hlenl")]
		LegacyPrefix_HleAlways,
		[NasmName("hlexr")]
		LegacyPrefix_XReleaseAlways,
		[NasmName("mustrep")]
		LegacyPrefix_MustRep,
		[NasmName("repe")]
		LegacyPrefix_RepE,
		[NasmName("norep")]
		LegacyPrefix_NoRep,

		[NasmName("norexb")]
		Rex_NoB = Category_Rex,
		[NasmName("norexw")]
		Rex_NoW,
		[NasmName("rex.l")]
		Rex_LockAsRexR,

		Byte = Category_Byte,  // "42", value is the byte itself
		Byte_PlusRegister,	// "40+r", value is the base byte (0b11111000)
		Byte_PlusCondition,	  // "40+c", value is the base byte (0b11110000)

		[NasmName("jmp8")]
		Jump_8 = Category_Jump,
		[NasmName("jcc8")]
		Jump_Conditional8,
		[NasmName("jlen")]
		Jump_Length,

		[NasmName("ib")]
		Immediate_Byte = Category_Byte,
		[NasmName("ib,s")]
		Immediate_Byte_Signed,
		[NasmName("ib,u")]
		Immediate_Byte_Unsigned,
		[NasmName("iw")]
		Immediate_Word,
		[NasmName("id")]
		Immediate_Dword,
		[NasmName("id,s")]
		Immediate_Dword_Signed,
		[NasmName("iq")]
		Immediate_Qword,
		[NasmName("iwd")]
		Immediate_WordOrDword,
		[NasmName("iwdq")]
		Immediate_WordOrDwordOrQword,
		[NasmName("/is4")]
		Immediate_Is4,
		[NasmName("seg")]
		Immediate_Segment,
		[NasmName("rel")]
		Immediate_RelativeOffset,
		[NasmName("rel8")]
		Immediate_RelativeOffset8,

		[NasmName("vm32x")]
		Misc_VM32x = Category_Misc,
		[NasmName("vm64x")]
		Misc_VM64x,
		[NasmName("vm32y")]
		Misc_VM32y,
		[NasmName("vm64y")]
		Misc_VM64y,

		[NasmName("wait")]
		Misc_Wait,
		[NasmName("nohi")]
		Misc_NoHi,
		[NasmName("vsiby")]
		Misc_Vsiby,
		[NasmName("vsibz")]
		Misc_Vsibz,
	}
}
