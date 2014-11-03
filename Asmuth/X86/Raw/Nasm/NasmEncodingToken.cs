using System;
using System.Collections.Generic;
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
		public readonly byte Value;

		public NasmEncodingToken(NasmEncodingTokenType type, byte value = 0)
		{
			this.Type = type;
			this.Value = value;
		}

		public NasmEncodingToken(NasmImmediateType immediateType)
		{
			this.Type = NasmEncodingTokenType.Immediate;
			this.Value = (byte)immediateType;
		}
	}

	public enum NasmEncodingTokenType : byte
	{
		Byte, // "42", value is the byte itself
		BytePlusRegister, // "40+r", value is the base byte
		BytePlusCondition, // "40+c", value is the base byte
		ModRM, // "/r or /[0-7]", value is 0-7 or 0xFF for /r
		Immediate // value is a NasmImmediateType
	}
}
