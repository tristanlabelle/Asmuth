using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;

namespace Asmuth.X86.Xed
{
	public struct XedInstructionStringResolvers
	{
		public Func<string, string> State;
		public Func<string, XedField> Field;
		public Func<string, XedOperandWidth> OperandWidth;
		public Func<string, XedXType> XType;
	}

	public sealed partial class XedInstruction
	{
		private readonly Dictionary<string, string> stringFields = new Dictionary<string, string>();

		private readonly HashSet<string> attributes = new HashSet<string>();
		public IReadOnlyCollection<string> Attributes => attributes;

		private byte privilegeLevel = 3;
		public int PrivilegeLevel => privilegeLevel;

		private byte version = 0;
		public int Version => version;

		private readonly List<XedFlagsRecord> flags = new List<XedFlagsRecord>();
		public IReadOnlyList<XedFlagsRecord> Flags => flags;

		private readonly List<XedInstructionForm> forms = new List<XedInstructionForm>();
		public IReadOnlyList<XedInstructionForm> Forms => forms;

		private XedInstruction() { }

		public string Class => stringFields.GetValueOrDefault("ICLASS");
		public string Comment => stringFields.GetValueOrDefault("COMMENT");
		public string Category => stringFields.GetValueOrDefault("CATEGORY");
		public string Exceptions => stringFields.GetValueOrDefault("EXCEPTIONS");
		public string Extension => stringFields.GetValueOrDefault("EXTENSION");
		public string IsaSet => stringFields.GetValueOrDefault("ISA_SET");
		public string UniqueName => stringFields.GetValueOrDefault("UNAME");
	}

	public sealed partial class XedInstructionForm
	{
		public struct Strings
		{
			public string Pattern;
			public string Operands;
			public string Name;
		}

		public ImmutableArray<XedBlot> Pattern { get; }
		public ImmutableArray<XedOperand> Operands { get; }
		public string Name { get; private set; }

		public XedInstructionForm(ImmutableArray<XedBlot> pattern,
			ImmutableArray<XedOperand> operands,
			string name = null)
		{
			this.Pattern = pattern;
			this.Operands = operands;
			this.Name = name;
		}
	}
}
