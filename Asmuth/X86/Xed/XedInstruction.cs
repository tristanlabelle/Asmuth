using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Xed
{
	public sealed class XedInstruction
	{
		public sealed class Builder
		{
			private XedInstruction instruction = new XedInstruction();

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

			public IList<XedBlot> PatternBaseBlots
				=> (List<XedBlot>)instruction.PatternBaseBlots;

			public XedInstruction Build(bool reuse)
			{
				var result = instruction;
				instruction = reuse ? new XedInstruction() : null;
				return result;
			}
		}

		public string Class { get; private set; }
		public string Disasm { get; private set; }
		public string DisasmIntel { get; private set; }
		public string DisasmAttSV { get; private set; }
		public IReadOnlyCollection<string> Attributes { get; private set; }
		public byte PrivilegeLevel { get; private set; }
		public string Category { get; private set; }
		public string Extension { get; private set; }
		public IReadOnlyList<XedFlagsRecord> FlagsRecords { get; private set; }
		public string IsaSet { get; private set; }
		public IReadOnlyList<XedBlot> PatternBaseBlots { get; } = new List<XedBlot>();
	}
}
