using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Asmuth.X86.Xed
{
	public enum XedOperandVisibility : byte
	{
		[XedEnumName("EXPL")]
		Explicit, // Represented in assembly and encoding
		[XedEnumName("IMPL")]
		Implicit, // Represented in assembly but not in an encoding field
		[XedEnumName("SUPP")]
		Suppressed, // Not represented in encoding nor assembly
		[XedEnumName("ECOND")]
		ECond // ??
	}

	public sealed class XedOperand
	{
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

		[Flags]
		private enum StateFlags : byte { None = 0, HasValue = 1, HasWidth = 2 }

		private readonly XedOperandWidth width;
		private readonly XedBlotValue value;
		public XedField Field { get; }
		public string Text { get; }
		public XedOperandAccess Access { get; }
		public XedOperandVisibility Visibility { get; }
		private readonly StateFlags stateFlags;

		public XedOperand(XedField field, XedBlotValue? value, XedOperandAccess access,
			XedOperandVisibility visibility, XedOperandWidth? width = null, string txt = null)
		{
			this.Field = field ?? throw new ArgumentNullException(nameof(field));
			this.value = value.GetValueOrDefault();
			this.Access = access;
			this.Visibility = visibility;
			this.width = width.GetValueOrDefault();
			this.Text = txt;

			this.stateFlags = StateFlags.None;
			if (value.HasValue) this.stateFlags |= StateFlags.HasValue;
			if (width.HasValue) this.stateFlags |= StateFlags.HasWidth;
		}

		public XedBlotValue? Value => (stateFlags & StateFlags.HasValue) == 0
			? (XedBlotValue?)null : value;
		public XedOperandWidth? Width => (stateFlags & StateFlags.HasWidth) == 0
			? (XedOperandWidth?)null : width;

		private static readonly Regex typeRegex = new Regex(
			@"^(?<n>[A-Z]+\d?) (= (?<v>\w+) (?<vc>\(\))? )?$",
			RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace);

		public static XedOperand Parse(string str,
			Func<string, XedField> fieldResolver,
			Func<string, XedOperandWidth> widthResolver,
			Func<string, XedXType> xtypeResolver)
		{
			var strParts = new Queue<string>(str.Split(':'));
			if (strParts.Count < 2) throw new FormatException();

			// Type field, e.g. "REG1=XMM_N()"
			var typeMatch = typeRegex.Match(strParts.Dequeue());
			if (!typeMatch.Success) throw new FormatException();

			var field = fieldResolver(typeMatch.Groups["n"].Value);
			XedBlotValue? value = null;
			if (typeMatch.Groups["v"].Success)
			{
				var valueStr = typeMatch.Groups["v"].Value;
				var registerType = (XedRegisterFieldType)field.Type;
				value = typeMatch.Groups["vc"].Success
					? XedBlotValue.MakeCallResult(valueStr)
					: XedBlotValue.MakeConstant(registerType.GetValue(valueStr));
			}

			// Access field
			var access = XedOperandAccess.Parse(strParts.Dequeue());

			// Visibility field (optional)
			XedOperandVisibility? visibility = TryParseHeadAsVisibility(strParts);

			// Width field (optional)
			XedOperandWidth? width = null;
			if (strParts.Count > 0)
			{
				width = widthResolver(strParts.Dequeue());

				// XType override part (optional)
				if (strParts.Count > 0)
				{
					try
					{
						var xtype = xtypeResolver(strParts.Peek());
						strParts.Dequeue();
						width = width.Value.WithXType(xtype);
					}
					catch (KeyNotFoundException) { }
				}
			}

			// Visibility field (optional, can be in two places)
			if (!visibility.HasValue) visibility = TryParseHeadAsVisibility(strParts);

			// TXT field (optional)
			string text = null;
			if (strParts.Count > 0 && strParts.Peek().StartsWith("TXT="))
				text = strParts.Dequeue().Substring("TXT=".Length);

			if (strParts.Count > 0) throw new FormatException();

			if (!visibility.HasValue) visibility = XedOperandVisibility.Explicit;
			return new XedOperand(field, value, access, visibility.Value, width, text);
		}

		private static XedOperandVisibility? TryParseHeadAsVisibility(Queue<string> strParts)
		{
			if (strParts.Count == 0) return null;
			
			var visibility = XedEnumNameAttribute.GetEnumerantOrNull<XedOperandVisibility>(strParts.Peek());
			if (!visibility.HasValue) return null;

			strParts.Dequeue();
			return visibility;
		}
	}
}
