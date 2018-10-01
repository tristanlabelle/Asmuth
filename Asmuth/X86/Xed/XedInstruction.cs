using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;

namespace Asmuth.X86.Xed
{
	public sealed class XedInstruction
	{
		public sealed class Builder
		{
			private XedInstruction instruction = new XedInstruction();

			public string Category
			{
				get => instruction.Category;
				set => instruction.Category = value;
			}

			public string Comment
			{
				get => instruction.Comment;
				set => instruction.Comment = value;
			}

			public string Class
			{
				get => instruction.Class;
				set => instruction.Class = value;
			}

			public string Disasm
			{
				get => instruction.Disasm;
				set => instruction.Disasm = value;
			}

			public string Exceptions
			{
				get => instruction.Exceptions;
				set => instruction.Exceptions = value;
			}

			public string Extension
			{
				get => instruction.Extension;
				set => instruction.Extension = value;
			}

			public string IsaSet
			{
				get => instruction.IsaSet;
				set => instruction.IsaSet = value;
			}

			public int PrivilegeLevel
			{
				get => instruction.privilegeLevel;
				set => instruction.privilegeLevel = value >= 0 && value <= 3 ? (byte)value
					: throw new ArgumentOutOfRangeException(nameof(PrivilegeLevel));
			}

			public ICollection<string> Attributes => (List<string>)instruction.Attributes;

			public IList<XedFlagsRecord> FlagsRecords => (List<XedFlagsRecord>)instruction.FlagsRecords;

			public IList<XedInstructionForm> Forms => (List<XedInstructionForm>)instruction.Forms;

			public XedInstruction Build(bool reuse)
			{
				var result = instruction;
				instruction = reuse ? new XedInstruction() : null;
				return result;
			}
		}

		public string Class { get; private set; }
		public string Comment { get; private set; }
		public string Disasm { get; private set; }
		public string DisasmIntel { get; private set; }
		public string DisasmAttSV { get; private set; }
		public IReadOnlyCollection<string> Attributes { get; } = new List<string>();
		private byte privilegeLevel;
		public int PrivilegeLevel => privilegeLevel;
		public string Category { get; private set; }
		public string Exceptions { get; private set; }
		public string Extension { get; private set; }
		public string IsaSet { get; private set; }
		public IReadOnlyList<XedFlagsRecord> FlagsRecords { get; } = new List<XedFlagsRecord>();
		public IReadOnlyList<XedInstructionForm> Forms { get; } = new List<XedInstructionForm>();
	}

	public sealed class XedInstructionForm
	{
		public ImmutableArray<XedBlot> Pattern { get; }
		public ImmutableArray<XedOperand> Operands { get; }
		public string Name { get; private set; }

		public XedInstructionForm(ImmutableArray<XedBlot> pattern, ImmutableArray<XedOperand> operands, string name = null)
		{
			this.Pattern = pattern;
			this.Operands = operands;
			this.Name = name;
		}
	}
}
