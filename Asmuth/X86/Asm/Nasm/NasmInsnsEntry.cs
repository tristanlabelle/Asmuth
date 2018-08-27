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
		public struct Data
		{
			public string Mnemonic;
			public IReadOnlyList<NasmOperand> Operands;
			public IReadOnlyList<NasmEncodingToken> EncodingTokens;
			public IReadOnlyCollection<string> Flags;
			public VexEncoding? VexEncoding;
			public NasmEVexTupleType EVexTupleType;
		}

		#region Fields
		private readonly Data data;
		#endregion

		public NasmInsnsEntry(in Data data)
		{
			this.data.Mnemonic = data.Mnemonic;
			this.data.Operands = data.Operands == null
				? EmptyArray<NasmOperand>.Rank1 : data.Operands.ToArray();
			this.data.EncodingTokens = data.EncodingTokens == null
				? EmptyArray<NasmEncodingToken>.Rank1 : data.EncodingTokens.ToArray();
			this.data.Flags = data.Flags == null
				? EmptyArray<string>.Rank1 : data.Flags.ToArray();
			this.data.VexEncoding = data.VexEncoding;
			this.data.EVexTupleType = data.EVexTupleType;
		}

		#region Properties
		public string Mnemonic => data.Mnemonic;
		public IReadOnlyList<NasmOperand> Operands => data.Operands;
		public IReadOnlyList<NasmEncodingToken> EncodingTokens => data.EncodingTokens;
		public VexEncoding? VexEncoding => data.VexEncoding;
		public VexType VexType => data.VexEncoding.HasValue ? data.VexEncoding.Value.Type : VexType.None;
		public NasmEVexTupleType EVexTupleType => data.EVexTupleType;
		public IReadOnlyCollection<string> Flags => data.Flags;
		public bool IsAssembleOnly => Flags.Contains(NasmInstructionFlags.AssemblerOnly);
		public bool IsFuture => Flags.Contains(NasmInstructionFlags.Future);
		public bool IsPseudo => EncodingTokens.Count == 0 || (EncodingTokens.Count == 1 && EncodingTokens[0].Type == NasmEncodingTokenType.Misc_Resb);
		#endregion

		#region Methods
		public bool HasFlag(string flag) => Flags.Contains(flag, StringComparer.InvariantCultureIgnoreCase);

		public string GetEncodingString()
		{
			var str = new StringBuilder();
			foreach (var token in EncodingTokens)
			{
				if (str.Length > 0) str.Append(' ');
				if (token.Type == NasmEncodingTokenType.Vex)
					str.Append(VexEncoding.Value.ToIntelStyleString());
				else
					str.Append(token.ToString());
			}
			return str.ToString();
		}

		public InstructionDefinition[] ToInstructionDefinitions()
		{
			if (!CanConvertToOpcodeEncoding) throw new InvalidOperationException();

			int operandSizeVariantCount = OpcodeEncodingOperandSizeVariantCount;
			bool hasConditionCodeVariants = HasConditionCodeVariants;
			int conditionCodeVariantCount = hasConditionCodeVariants ? ConditionCodeEnum.Count : 1;
			int variantCount = operandSizeVariantCount * conditionCodeVariantCount;

			var variants = new InstructionDefinition[variantCount];
			int variantIndex = 0;

			var data = new InstructionDefinition.Data();

			// Fill up mnemonic, taking into account +cc cases
			string[] conditionCodeMnemonics = null;
			if (hasConditionCodeVariants && Mnemonic.EndsWith("cc", StringComparison.InvariantCultureIgnoreCase))
			{
				string baseMnemonic = Mnemonic.Substring(0, Mnemonic.Length - 2);
				conditionCodeMnemonics = new string[conditionCodeVariantCount];
				for (int i = 0; i < conditionCodeVariantCount; ++i)
					conditionCodeMnemonics[i] = baseMnemonic + ((ConditionCode)i).GetMnemonicSuffix().ToUpperInvariant();
			}
			else
			{
				data.Mnemonic = Mnemonic;
			}

			// For each variant
			for (int j = 0; j < operandSizeVariantCount; j++)
			{
				var operandSize = operandSizeVariantCount == 1 ? null
					: (IntegerSize?)((int)IntegerSize.Word + j);

				data.Operands = GetOperandDefinitions(operandSize);

				for (int i = 0; i < conditionCodeVariantCount; i++)
				{
					ConditionCode? conditionCode = null;
					if (hasConditionCodeVariants)
					{
						conditionCode = (ConditionCode)i;
						if (conditionCodeMnemonics != null)
							data.Mnemonic = conditionCodeMnemonics[i];
					}
					
					data.Encoding = GetOpcodeEncoding(conditionCode, operandSize);

					variants[variantIndex] = new InstructionDefinition(in data);
					variantIndex++;
				}
			}

			return variants;
		}

		public override string ToString()
		{
			return Mnemonic + ": " + GetEncodingString();
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
