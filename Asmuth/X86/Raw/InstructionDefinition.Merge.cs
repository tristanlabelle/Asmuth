using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	partial class InstructionDefinition
	{
		private struct MergeCondition
		{
			public readonly InstructionEncoding EncodingMask;
			public readonly InstructionEncoding EncodingValue;
			public readonly Opcode OpcodeMask;
			public readonly Opcode[] OpcodeValues;

			public MergeCondition(
				InstructionEncoding encodingMask, InstructionEncoding encodingValue,
				Opcode opcodeMask, Opcode[] opcodeValues)
			{
				this.EncodingMask = encodingMask;
				this.EncodingValue = encodingValue;
				this.OpcodeMask = opcodeMask;
				this.OpcodeValues = opcodeValues;
			}
		}

		private static readonly MergeCondition[] mergeConditions = new[]
		{
			new MergeCondition(InstructionEncoding.RexW_Mask, InstructionEncoding.RexW_Fixed, Opcode.RexW, new[] { (Opcode)0, Opcode.RexW }),
			new MergeCondition(InstructionEncoding.VexL_Mask, InstructionEncoding.VexL_Fixed, Opcode.VexL_Mask, new[] { Opcode.VexL_0, Opcode.VexL_1, Opcode.VexL_2 }),
			new MergeCondition(InstructionEncoding.VexL_Mask, InstructionEncoding.VexL_Fixed, Opcode.VexL_Mask, new[] { Opcode.VexL_0, Opcode.VexL_1 })
		};

		public static IEnumerable<InstructionDefinition> MergeHeterogeneous(IEnumerable<InstructionDefinition> instructions)
		{
			Contract.Requires(instructions != null);

			// Group by masked opcode
			var groups = MultiValueDictionary<ulong, InstructionDefinition>.Create<List<InstructionDefinition>>();
			foreach (var instruction in instructions)
				groups.Add(GetMergeGroupKey(instruction), instruction);

			// Merge every group
			foreach (List<InstructionDefinition> group in groups.Values)
			{
				MergeHomogeneous(group);
				foreach (var instruction in group)
					yield return instruction;
			}
		}

		private static void MergeHomogeneous(IList<InstructionDefinition> group)
		{
			Contract.Requires(group != null);

			if (group.Count <= 1) return;

			throw new NotImplementedException();
		}

		private static ulong GetMergeGroupKey(InstructionDefinition instruction)
		{
			Contract.Requires(instruction != null);

			// Mergeable instructions can only differ on these fields:
			// REX.W, VEX.L'L, operand size and immediate types
			var opcode = instruction.Opcode & instruction.OpcodeFixedMask;
			opcode &= ~(Opcode.RexW | Opcode.VexL_Mask);
			var encoding = instruction.Encoding;
			encoding &= ~(InstructionEncoding.OperandSize_Mask | InstructionEncoding.ImmediateTypes_Mask);

			return ((ulong)opcode << 32) | (ulong)encoding;
		}
	}
}
