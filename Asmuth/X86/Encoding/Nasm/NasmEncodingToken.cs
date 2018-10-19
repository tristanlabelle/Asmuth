using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Encoding.Nasm
{
	[StructLayout(LayoutKind.Sequential, Size = 2)]
	public readonly struct NasmEncodingToken : IEquatable<NasmEncodingToken>
	{
		public NasmEncodingTokenType Type { get; }
		public byte Byte { get; }

		public NasmEncodingToken(NasmEncodingTokenType type, byte @byte = 0)
		{
			this.Type = type;
			this.Byte = @byte;
		}

		public bool Equals(NasmEncodingToken other) => Type == other.Type && Byte == other.Byte;
		public override bool Equals(object obj) => obj is NasmEncodingToken && Equals((NasmEncodingToken)obj);
		public override int GetHashCode() => ((int)Type << 8) | Byte;

		public override string ToString()
		{
			switch (Type)
			{
				case NasmEncodingTokenType.Vex: return "vex";
				case NasmEncodingTokenType.Byte: return Byte.ToString("X2", CultureInfo.InvariantCulture);
				case NasmEncodingTokenType.Byte_PlusRegister: return Byte.ToString("X2", CultureInfo.InvariantCulture) + "+r";
				case NasmEncodingTokenType.Byte_PlusConditionCode: return Byte.ToString("X2", CultureInfo.InvariantCulture) + "+cc";
				case NasmEncodingTokenType.ModRM_FixedReg: return "/" + (char)('0' + Byte);
				default: return NasmEnumNameAttribute.GetNameOrNull(Type) ?? Type.ToString();
			}
		}

		public static bool Equals(NasmEncodingToken first, NasmEncodingToken second) => first.Equals(second);
		public static bool operator ==(NasmEncodingToken lhs, NasmEncodingToken rhs) => Equals(lhs, rhs);
		public static bool operator !=(NasmEncodingToken lhs, NasmEncodingToken rhs) => !Equals(lhs, rhs);

		public static NasmEncodingTokenType TryParseType(string name)
		{
			if (name == null) throw new ArgumentNullException(nameof(name));
			return NasmEnumNameAttribute.GetEnumerantOrNull<NasmEncodingTokenType>(name)
				?? NasmEncodingTokenType.None;
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
		Category_VectorSib = 10 << Category_Shift,
		Category_Misc = 15 << Category_Shift,
		Category_Mask = 0xF0,

		Vex = Category_Vex, // value unused, vex data stored as a VexOpcodeEncoding value

		[NasmEnumName("a16")]
		AddressSize_16 = Category_AddressSize,
		[NasmEnumName("a32")]
		AddressSize_32,
		[NasmEnumName("a64")]
		AddressSize_64,
		[NasmEnumName("adf")]
		AddressSize_NoOverride,

		[NasmEnumName("o16")]
		OperandSize_16 = Category_OperandSize,
		[NasmEnumName("o32")]
		OperandSize_32,
		[NasmEnumName("o64")]
		OperandSize_64,
		[NasmEnumName("o64nw")]
		OperandSize_64WithoutW,
		[NasmEnumName("odf")]
		OperandSize_NoOverride,

		[NasmEnumName("np")]
		LegacyPrefix_NoSimd = Category_LegacyPrefix,
		[NasmEnumName("f2i")]
		LegacyPrefix_F2,
		[NasmEnumName("f3i")]
		LegacyPrefix_F3,
		[NasmEnumName("nof3")]
		LegacyPrefix_NoF3,
		[NasmEnumName("hle")]
		LegacyPrefix_HleWithLock,
		[NasmEnumName("hlenl")]
		LegacyPrefix_HleAlways,
		[NasmEnumName("hlexr")]
		LegacyPrefix_XReleaseAlways,
		[NasmEnumName("mustrep")]
		LegacyPrefix_MustRep,
		[NasmEnumName("repe")]
		LegacyPrefix_DisassembleRepAsRepE,
		[NasmEnumName("norep")]
		LegacyPrefix_NoRep,

		[NasmEnumName("norexb")]
		Rex_NoB = Category_Rex,
		[NasmEnumName("norexx")]
		Rex_NoX,
		[NasmEnumName("norexr")]
		Rex_NoR,
		[NasmEnumName("norexw")]
		Rex_NoW,
		[NasmEnumName("rex.l")]
		Rex_LockAsRexR, // See AMD APM, MOV CRn

		Byte = Category_Byte, // "42", value is the byte itself
		Byte_PlusRegister, // "40+r", value is the base byte (0b11111000)
		Byte_PlusConditionCode, // "40+c", value is the base byte (0b11110000)

		[NasmEnumName("/r")]
		ModRM = Category_ModRM,
		ModRM_FixedReg, // "/4", value is the digit that follows the slash (value of the reg field)

		[NasmEnumName("jmp8")]
		Jump_8 = Category_Jump,
		[NasmEnumName("jcc8")]
		Jump_Conditional8,
		[NasmEnumName("jlen")]
		Jump_Length,

		[NasmEnumName("ib")]
		Immediate_Byte = Category_Immediate,
		[NasmEnumName("ib,s")]
		Immediate_Byte_Signed,
		[NasmEnumName("ib,u")]
		Immediate_Byte_Unsigned,
		[NasmEnumName("iw")]
		Immediate_Word,
		[NasmEnumName("id")]
		Immediate_Dword,
		[NasmEnumName("id,s")]
		Immediate_Dword_Signed,
		[NasmEnumName("iq")]
		Immediate_Qword,
		[NasmEnumName("iwd")]
		Immediate_WordOrDword,
		[NasmEnumName("iwdq")]
		Immediate_WordOrDwordOrQword,
		[NasmEnumName("/is4")]
		Immediate_Is4,
		[NasmEnumName("seg")]
		Immediate_Segment,
		[NasmEnumName("rel")]
		Immediate_RelativeOffset,
		[NasmEnumName("rel8")]
		Immediate_RelativeOffset8,

		[NasmEnumName("vm32x")]
		VectorSib_XmmDwordIndices = Category_VectorSib,
		[NasmEnumName("vm64x")]
		VectorSib_XmmQwordIndices,
		[NasmEnumName("vm32y")]
		VectorSib_YmmDwordIndices,
		[NasmEnumName("vm64y")]
		VectorSib_YmmQwordIndices,

		[NasmEnumName("vsibx")]
		VectorSib_Xmm,
		[NasmEnumName("vsiby")]
		VectorSib_Ymm,
		[NasmEnumName("vsibz")]
		VectorSib_Zmm,

		[NasmEnumName("wait")]
		Misc_WaitPrefix = Category_Misc,
		[NasmEnumName("nohi")]
		Misc_NoHigh8Register,
		[NasmEnumName("resb")]
		Misc_Resb // The RESB pseudo-instruction
	}
}
