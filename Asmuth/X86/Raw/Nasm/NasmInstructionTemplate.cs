using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw.Nasm
{
	/// <summary>
	/// NASM instruction template.
	/// </summary>
	public struct NasmInstructionTemplate
	{
		public ulong LowFlags, HighFlags;  // Each bit corresponds to a NasmInstructionFlag
		public string Mnemonic;
		public NasmOperand[] Operands;
		public VexOpcodeEncoding VexOpcodeEncoding;
		public NasmEVexTupleType EVexTupleType;
	}

	public enum NasmEVexTupleType : byte
	{
		None = 0,
		FV = 1,
		HV = 2,
		Fvm = 3,
		T1S8 = 4,
		T1S16 = 5,
		T1S = 6,
		T1F32 = 7,
		T1F64 = 8,
		T2 = 9,
		T4 = 10,
		T8 = 11,
		Hvm = 12,
		Qvm = 13,
		Ovm = 14,
		M128 = 15,
		Dup = 16,
	}
}
