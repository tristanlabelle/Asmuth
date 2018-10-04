using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Asmuth.X86.Xed
{
	public enum XedOperandComponentType : byte
	{
		[XedEnumName("AGEN")] AGen,
		[XedEnumName("PTR")] Pointer,
		[XedEnumName("RELBR")] RelativeBranch,
		[XedEnumName("REG")] Register,
		[XedEnumName("MEM")] Memory,
		[XedEnumName("SEG")] MemorySegment,
		[XedEnumName("BASE")] MemoryBase,
		[XedEnumName("INDEX")] MemoryIndex,
		[XedEnumName("IMM")] Immediate
	}

	public struct XedOperandComponent
	{
		public XedOperandComponentType Type;
		public byte? Index;
		public string ValueString;
		public bool IsCallableValue;
		public XedOperandAccess Access;
		public XedOperandWidth? Width;
		public XedOperandVisibility? Visibility;
		public string Text;

		// AGEN:r
		// PTR:r:p
		// RELBR:r:b:i8
		// REG0=XED_REG_DX:r:SUPP
		// REG0=XED_REG_ST0:r:SUPP:f80
		// REG0=XED_REG_ST0:r:IMPL:f80
		// REG1=XMM_N():r:dq:i32
		// REG1=MASK1():r:mskw:TXT=ZEROSTR
		// MEM0:w:d
		// MEM0:cw:SUPP:b BASE0=ArDI():rcw:SUPP SEG0=FINAL_ESEG():r:SUPP
		// INDEX=XED_REG_AL:r:SUPP
		// IMM0:r:b
		// IMM0:r:b:i8

		private static readonly Regex typeRegex = new Regex(
			@"^(?<t>[A-Z]+) (?<i>\d)? (= (?<v>\w+) (?<vc>\(\))? )?$",
			RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace);

		public static XedOperandComponent Parse(string str,
			Func<string, XedOperandWidth> widthResolver,
			Func<string, XedXType> xtypeResolver)
		{
			var components = str.Split(':');
			if (components.Length < 2) throw new FormatException();

			var component = new XedOperandComponent();

			// Type field, e.g. "REG1=XMM_N()"
			var typeMatch = typeRegex.Match(components[0]);
			if (!typeMatch.Success) throw new FormatException();

			component.Type = XedEnumNameAttribute.GetEnumerantOrNull<XedOperandComponentType>(typeMatch.Groups["t"].Value).Value;
			if (typeMatch.Groups["i"].Success) component.Index = (byte)(typeMatch.Groups["i"].Value[0] - '0');

			component.Access = XedOperandAccess.Parse(components[1]);
			if (components.Length >= 3) component.Width = widthResolver(components[2]);

			throw new NotImplementedException();
		}
	}
}
