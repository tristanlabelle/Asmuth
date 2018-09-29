using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
		public string Disasm { get; private set; }
		public string DisasmIntel { get; private set; }
		public string DisasmAttSV { get; private set; }
		public IReadOnlyCollection<string> Attributes { get; private set; }
		public byte PrivilegeLevel { get; private set; }
		public string Category { get; private set; }
		public string Extension { get; private set; }
		public string IsaSet { get; private set; }
		public IReadOnlyList<XedFlagsRecord> FlagsRecords { get; private set; }
		public IReadOnlyList<XedInstructionForm> Forms { get; private set; } = new List<XedInstructionForm>();
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
	
	public enum XedOperandType : byte
	{
		ImplicitRegister,
		ExplicitRegister,
		Memory,
		Immediate
	}

	public sealed class XedOperand
	{
		// REG0=XED_REG_ST0:r:IMPL:f80
		// REG1=XMM_N():r:dq:i32
		// REG1=MASK1():r:mskw:TXT=ZEROSTR
		// MEM0:w:d
		// IMM0:r:b

		public XedOperandType Type { get; }
		private string registerString;
		public string ImplicitRegisterName => Type == XedOperandType.ImplicitRegister
			? registerString : throw new InvalidOperationException();
		public string ExplicitRegisterPatternName => Type == XedOperandType.ExplicitRegister
			? registerString : throw new InvalidOperationException();
		public AccessType Access { get; }
		public XedOperandWidth Width { get; }

		private XedOperand(XedOperandType type, string registerString, AccessType access, XedOperandWidth width)
		{
			this.Type = type;
			this.registerString = registerString;
			this.Access = access;
			this.Width = width;
		}

		public bool IsMemory => Type == XedOperandType.Memory;
		public bool IsRegister => Type != XedOperandType.Memory;

		public static XedOperand MakeImplicitRegister(string name, AccessType access, XedOperandWidth width)
			=> new XedOperand(XedOperandType.ImplicitRegister, name, access, width);
		public static XedOperand MakeExplicitRegister(string patternName, AccessType access, XedOperandWidth width)
			=> new XedOperand(XedOperandType.ExplicitRegister, patternName, access, width);
		public static XedOperand MakeMemory(AccessType access, XedOperandWidth width)
			=> new XedOperand(XedOperandType.Memory, null, access, width);
		public static XedOperand MakeImmediate(XedOperandWidth width)
			=> new XedOperand(XedOperandType.Immediate, null, AccessType.Read, width);
	}
}
