using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	partial class InstructionDefinition
	{
		private struct Merge
		{
			public readonly InstructionEncoding Mask;
			public readonly InstructionEncoding A, B;
			public readonly InstructionEncoding Result;

			public Merge(InstructionEncoding mask, InstructionEncoding a, InstructionEncoding b, InstructionEncoding result)
			{
				this.Mask = mask;
				this.A = a & mask;
				this.B = b & mask;
				this.Result = result & mask;
			}

			public bool AppliesTo(InstructionEncoding first, InstructionEncoding second)
			{
				if ((first & ~Mask) != (second & ~Mask)) return false;

				return ((first & Mask) == A && (second & Mask) == B)
					|| (first & Mask) == B && (second & Mask) == A;
			}
		}

		private static readonly Merge[] merges =
		{
			new Merge(InstructionEncoding.OperandSize_Mask, InstructionEncoding.OperandSize_Fixed16, InstructionEncoding.OperandSize_Fixed32, InstructionEncoding.OperandSize_16Or32),
			new Merge(InstructionEncoding.OperandSize_Mask, InstructionEncoding.OperandSize_Fixed32, InstructionEncoding.OperandSize_Fixed64, InstructionEncoding.OperandSize_32Or64),
			new Merge(InstructionEncoding.OperandSize_Mask, InstructionEncoding.OperandSize_16Or32, InstructionEncoding.OperandSize_Fixed64, InstructionEncoding.OperandSize_16Or32Or64),
			new Merge(InstructionEncoding.OperandSize_Mask, InstructionEncoding.OperandSize_Fixed16, InstructionEncoding.OperandSize_32Or64, InstructionEncoding.OperandSize_16Or32Or64),
		};

		public static IEnumerable<InstructionDefinition> TryMerge(IEnumerable<InstructionDefinition> definitions)
		{
			Contract.Requires(definitions != null);

			foreach (var opcodeDefs in definitions.GroupBy(d => d.Opcode))
			{
				foreach (var defs in TryMergeGroup(opcodeDefs))
					yield return defs;
			}
		}

		public static IEnumerable<InstructionDefinition> TryMergeGroup(IEnumerable<InstructionDefinition> definitions)
		{
			var defs = definitions.ToList();
			for (int i = 0; i < defs.Count; ++i)
			{
				for (int j = i + 1; j < defs.Count; ++j)
				{
					var merged = TryMerge(defs[i], defs[j]);
					if (merged != null)
					{
						defs[i] = merged;
						defs.RemoveAt(j);
						i = -1;
						break;
					}
				}
			}
			return defs;
		}

		public static InstructionDefinition TryMerge(InstructionDefinition first, InstructionDefinition second)
		{
			Contract.Requires(first != null);
			Contract.Requires(second != null);

			if (first.Mnemonic != second.Mnemonic
				|| first.Opcode != second.Opcode
				|| first.AffectedFlags != second.AffectedFlags)
				return null;
			// TODO: Make sure operands fuzzily match

			foreach (var merge in merges)
			{
				if (merge.AppliesTo(first.Encoding, second.Encoding))
				{
					var data = new Data
					{
						AffectedFlags = first.AffectedFlags,
						Encoding = merge.Result,
						Mnemonic = first.Mnemonic,
						Opcode = first.Opcode,
						RequiredFeatureFlags = first.RequiredFeatureFlags
					};
					return new InstructionDefinition(ref data, first.Operands);
				}
			}

			return null;
		}
	}
}
