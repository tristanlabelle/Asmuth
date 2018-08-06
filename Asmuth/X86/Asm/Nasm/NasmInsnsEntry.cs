using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Asm.Nasm
{
	/// <summary>
	/// An entry in NASM's insns.dat file.
	/// </summary>
	public sealed partial class NasmInsnsEntry
	{
		#region Fields
		private string mnemonic;
		private IList<NasmOperand> operands;
		private IList<NasmEncodingToken> encodingTokens;
		private ICollection<string> flags;
		private VexEncoding vexEncoding;
		private NasmOperandFlags operandFlags;
		private NasmEVexTupleType evexTupleType; 
		#endregion

		// Use builder nested class
		private NasmInsnsEntry() { }

		#region Properties
		public string Mnemonic => mnemonic;
		public IReadOnlyList<NasmOperand> Operands => (IReadOnlyList<NasmOperand>)operands;
		public IReadOnlyList<NasmEncodingToken> EncodingTokens => (IReadOnlyList<NasmEncodingToken>)encodingTokens;
		public VexEncoding VexEncoding => vexEncoding;
		public NasmOperandFlags OperandFlags => operandFlags;
		public NasmEVexTupleType EVexTupleType => evexTupleType;
		public IReadOnlyCollection<string> Flags => (IReadOnlyCollection<string>)flags;
		public bool IsAssembleOnly => flags.Contains(NasmInstructionFlags.AssemblerOnly);
		public bool IsFuture => flags.Contains(NasmInstructionFlags.Future);
		public bool IsPseudo => encodingTokens.Count == 0 || (encodingTokens.Count == 1 && encodingTokens[0].Type == NasmEncodingTokenType.Misc_Resb);
		#endregion

		#region Methods
		public string GetEncodingString()
		{
			var str = new StringBuilder();
			foreach (var token in encodingTokens)
			{
				if (str.Length > 0) str.Append(' ');
				if (token.Type == NasmEncodingTokenType.Vex)
					str.Append(vexEncoding.ToIntelStyleString());
				else
					str.Append(token.ToString());
			}
			return str.ToString();
		}

		public override string ToString()
		{
			return Mnemonic + ": " + GetEncodingString();
		}
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
			public ICollection<string> Flags => entry.flags;

			public VexEncoding VexEncoding
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
				if (Mnemonic == null) throw new ArgumentNullException(nameof(Mnemonic));

				var result = entry;
				result.operands = result.operands.ToArray();
				result.encodingTokens = result.encodingTokens.ToArray();
				entry = reuse ? CreateEmptyEntry() : null;
				return result;
			}

			private static NasmInsnsEntry CreateEmptyEntry()
			{
				return new NasmInsnsEntry
				{
					flags = new HashSet<string>(),
					operands = new List<NasmOperand>(),
					encodingTokens = new List<NasmEncodingToken>()
				};
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
