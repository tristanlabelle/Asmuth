﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw.Nasm
{
	/// <summary>
	/// An entry in NASM's insns.dat file.
	/// </summary>
	public sealed class NasmInsnsEntry
	{
		#region Fields
		private string mnemonic;
		private IList<NasmOperand> operands;
		private IList<NasmEncodingToken> encodingTokens;
		private ICollection<NasmInstructionFlag> flags;
		private VexOpcodeEncoding vexEncoding;
		private NasmOperandFlags operandFlags;
		private NasmEVexTupleType evexTupleType; 
		#endregion

		// Use builder nested class
		private NasmInsnsEntry() { }

		#region Properties
		public string Mnemonic => mnemonic;
		public IReadOnlyList<NasmOperand> Operands => (IReadOnlyList<NasmOperand>)operands;
		public IReadOnlyList<NasmEncodingToken> EncodingTokens => (IReadOnlyList<NasmEncodingToken>)encodingTokens;
		public VexOpcodeEncoding VexEncoding => vexEncoding;
		public NasmOperandFlags OperandFlags => operandFlags;
		public NasmEVexTupleType EVexTupleType => evexTupleType;
		public IReadOnlyCollection<NasmInstructionFlag> Flags => (IReadOnlyCollection<NasmInstructionFlag>)flags;
		#endregion

		#region Methods
		public override string ToString() => mnemonic;
		#endregion

		#region Builder Class
		public sealed class Builder
		{
			private NasmInsnsEntry entry = CreateEmptyEntry();

			#region Properties
			public string Mnemonic
			{
				get { return entry.mnemonic; }
				set { entry.mnemonic = value; }
			}

			public IList<NasmOperand> Operands => entry.operands;
			public IList<NasmEncodingToken> EncodingTokens => entry.encodingTokens;
			public ICollection<NasmInstructionFlag> Flags => entry.flags;

			public VexOpcodeEncoding VexEncoding
			{
				get { return entry.vexEncoding; }
				set { entry.vexEncoding = value; }
			}

			public NasmOperandFlags OperandFlags
			{
				get { return entry.operandFlags; }
				set { entry.operandFlags = value; }
			}

			public NasmEVexTupleType EVexTupleType
			{
				get { return entry.evexTupleType; }
				set { entry.evexTupleType = value; }
			}
			#endregion

			#region Methods
			public NasmInsnsEntry Build(bool reuse = true)
			{
				Contract.Requires(Mnemonic != null);

				var result = entry;
				result.operands = result.operands.ToArray();
				result.encodingTokens = result.encodingTokens.ToArray();
				entry = reuse ? CreateEmptyEntry() : null;
				return result;
			}

			private static NasmInsnsEntry CreateEmptyEntry()
			{
				var entry = new NasmInsnsEntry();
				entry.flags = new HashSet<NasmInstructionFlag>();
				entry.operands = new List<NasmOperand>();
				entry.encodingTokens = new List<NasmEncodingToken>();
				return entry;
			}
			#endregion
		}
		#endregion
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
