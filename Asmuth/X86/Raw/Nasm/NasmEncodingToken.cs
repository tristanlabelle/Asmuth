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
		public readonly NasmEncodingTokenType Type;
		public readonly byte Value;

		public NasmEncodingToken(NasmEncodingTokenType type, byte value = 0)
		{
			this.Type = type;
			this.Value = value;
		}

		public NasmEncodingToken(NasmEncodingFlag encodingFlag)
		{
			this.Type = NasmEncodingTokenType.Flag;
			this.Value = (byte)encodingFlag;
		}
	}

	public enum NasmEncodingTokenType : byte
	{
		Flag, // "a64", value is a NasmEncodingFlag
		Byte, // "42", value is the byte itself
		BytePlusRegister, // "40+r", value is the base byte
		ModRM, // "/r or /[0-7]", value is 0-7 or 0xFF for /r
		Vex, // "vex.nds.128.66.0f", value is the VexOpcodeEncoding in the NasmInsnsEntry
	}
}
