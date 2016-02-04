using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Nasm
{
	/// <summary>
	/// An entry in NASM's insns.dat file.
	/// </summary>
	[StructLayout(LayoutKind.Auto)]
	public sealed partial class NasmInsnsEntry
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
		public bool IsAssembleOnly => flags.Contains(NasmInstructionFlag.ND);
		#endregion

		#region Methods
		public string GetEncodingString()
		{
			var str = new StringBuilder();
			foreach (var token in encodingTokens)
			{
				if (str.Length > 0) str.Append(' ');
				if (token.Type == NasmEncodingTokenType.Vex)
					str.Append(vexEncoding.ToIntelStyleString(vexOnly: true));
				else
					str.Append(token.ToString());
			}
			return str.ToString();
		}

		public override string ToString() => Mnemonic;
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
