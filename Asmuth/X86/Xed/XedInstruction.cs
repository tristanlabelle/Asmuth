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

		private static readonly Regex typeRegex = new Regex(
			@"^( (?<t>REG)(?<i>\d)=(?<v>\w+)(?<e>\(\))? | (?<t>(MEM|IMM))(?<i>\d))$",
			RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace);

		private static readonly Dictionary<string, AccessType> accessTypes = new Dictionary<string, AccessType>
		{
			{ "r", AccessType.Read },
			{ "rw", AccessType.ReadWrite },
			{ "w", AccessType.Write },
		};

		public static KeyValuePair<int, XedOperand> Parse(string str, Func<string, XedOperandWidth> widthResolver)
		{
			var components = str.Split(':');
			if (components.Length < 3) throw new FormatException();

			var typeMatch = typeRegex.Match(components[0]);
			if (!typeMatch.Success) throw new FormatException();

			int index = typeMatch.Groups["i"].Value[0] - '0';
			var accessType = accessTypes[components[1]];
			var width = widthResolver(components[2]);

			var typeName = typeMatch.Groups["t"].Value;
			XedOperand operand;
			if (typeName == "REG")
			{
				if (components.Length > 3) throw new NotImplementedException();
				var valueStr = typeMatch.Groups["v"].Value;
				if (typeMatch.Groups["e"].Success)
				{
					operand = MakeExplicitRegister(valueStr, accessType, width);
				}
				else
				{
					operand = MakeImplicitRegister(valueStr, accessType, width);
				}
			}
			else if (typeName == "MEM")
			{
				operand = MakeMemory(accessType, width);
			}
			else if (typeName == "IMM")
			{
				if (accessType != AccessType.Read) throw new FormatException();
				operand = MakeImmediate(width);
			}
			else throw new UnreachableException();

			return new KeyValuePair<int, XedOperand>(index, operand);
		}
	}
}
