using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Xed
{
	public enum XedOperandVisibility : byte
	{
		[XedEnumName("EXPL")]
		Explicit, // Represented in assembly and encoding
		[XedEnumName("IMPL")]
		Implicit, // Represented in assembly but not in an encoding field
		[XedEnumName("SUPP")]
		Suppressed // Not represented in encoding nor assembly
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
		// REG0=XED_REG_DX:r:SUPP
		// REG0=XED_REG_ST0:r:SUPP:f80
		// REG0=XED_REG_ST0:r:IMPL:f80
		// REG1=XMM_N():r:dq:i32
		// REG1=MASK1():r:mskw:TXT=ZEROSTR
		// MEM0:w:d
		// MEM0:cw:SUPP:b BASE0=ArDI():rcw:SUPP SEG0=FINAL_ESEG():r:SUPP
		// IMM0:r:b
		// IMM0:r:b:i8

		public XedOperandType Type { get; }
		private string registerString;
		public string ImplicitRegisterName => Type == XedOperandType.ImplicitRegister
			? registerString : throw new InvalidOperationException();
		public string ExplicitRegisterPatternName => Type == XedOperandType.ExplicitRegister
			? registerString : throw new InvalidOperationException();
		public AccessType Access { get; }
		public XedOperandWidth? Width { get; }
		public XedOperandVisibility Visibility { get; }

		private XedOperand(XedOperandType type, string registerString, AccessType access, XedOperandWidth? width,
			XedOperandVisibility visilibity)
		{
			this.Type = type;
			this.registerString = registerString;
			this.Access = access;
			this.Width = width;
			this.Visibility = visilibity;
		}

		public bool IsMemory => Type == XedOperandType.Memory;
		public bool IsRegister => Type != XedOperandType.Memory;

		public static XedOperand MakeImplicitRegister(string name, AccessType access, XedOperandWidth? width, bool visible)
			=> new XedOperand(XedOperandType.ImplicitRegister, name, access, width,
				visible ? XedOperandVisibility.Implicit : XedOperandVisibility.Suppressed);
		public static XedOperand MakeExplicitRegister(string patternName, AccessType access, XedOperandWidth? width)
			=> new XedOperand(XedOperandType.ExplicitRegister, patternName, access, width, XedOperandVisibility.Explicit);
		public static XedOperand MakeMemory(AccessType access, XedOperandWidth? width)
			=> new XedOperand(XedOperandType.Memory, null, access, width);
		public static XedOperand MakeImmediate(XedOperandWidth width, XedOperandVisibility visibility = XedOperandVisibility.Explicit)
			=> new XedOperand(XedOperandType.Immediate, null, AccessType.Read, width, visibility);

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
			if (components.Length < 2) throw new FormatException();

			var typeMatch = typeRegex.Match(components[0]);
			if (!typeMatch.Success) throw new FormatException();

			int index = typeMatch.Groups["i"].Value[0] - '0';
			var accessType = accessTypes[components[1]];
			var width = components.Length >= 3 ? widthResolver(components[2]) : (XedOperandWidth?)null;

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
				if (!width.HasValue) throw new FormatException();
				operand = MakeImmediate(width.Value);
			}
			else throw new UnreachableException();

			return new KeyValuePair<int, XedOperand>(index, operand);
		}
	}
}
