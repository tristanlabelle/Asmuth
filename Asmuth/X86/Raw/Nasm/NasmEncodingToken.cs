﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw.Nasm
{
	[StructLayout(LayoutKind.Sequential, Size = 2)]
	public struct NasmEncodingToken
	{
		public readonly NasmEncodingTokenType Type;
		public readonly byte Byte;

		public NasmEncodingToken(NasmEncodingTokenType type, byte @byte = 0)
		{
			this.Type = type;
			this.Byte = @byte;
		}

		public override string ToString()
		{
			switch (Type)
			{
				case NasmEncodingTokenType.Byte: return Byte.ToString("X2", CultureInfo.InvariantCulture);
				case NasmEncodingTokenType.Byte_PlusRegister: return Byte.ToString("X2", CultureInfo.InvariantCulture) + "+r";
				case NasmEncodingTokenType.Byte_PlusConditionCode: return Byte.ToString("X2", CultureInfo.InvariantCulture) + "+cc";
				default: return NasmEnum<NasmEncodingTokenType>.GetNameOrNull(Type) ?? Type.ToString();
			}
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
		LegacyPrefix_DisassembleRepAsRepE,
		[NasmName("norep")]
		LegacyPrefix_NoRep,

		[NasmName("norexb")]
		Rex_NoB = Category_Rex,
		[NasmName("norexw")]
		Rex_NoW,
		[NasmName("rex.l")]
		Rex_LockAsRexR,	// See AMD APM, MOV CRn

		Byte = Category_Byte, // "42", value is the byte itself
		Byte_PlusRegister, // "40+r", value is the base byte (0b11111000)
		Byte_PlusConditionCode, // "40+c", value is the base byte (0b11110000)

		[NasmName("/r")]
		ModRM = Category_ModRM,
		ModRM_FixedReg,	// "/4", value is the digit that follows the slash (value of the reg field)

		[NasmName("jmp8")]
		Jump_8 = Category_Jump,
		[NasmName("jcc8")]
		Jump_Conditional8,
		[NasmName("jlen")]
		Jump_Length,

		[NasmName("ib")]
		Immediate_Byte = Category_Immediate,
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
		Misc_AssembleWaitPrefix,
		[NasmName("nohi")]
		Misc_NoHigh8Register,
		[NasmName("vsiby")]
		Misc_Vsiby,
		[NasmName("vsibz")]
		Misc_Vsibz,
	}
}
