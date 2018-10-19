using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Asmuth.X86.Xed
{
	public enum XedOperandKind : byte
	{
		[XedEnumName("REG")] Register, // REG0=XED_REG_ST0:r:SUPP:f80 / REG1=MASK1():r:mskw:TXT=ZEROSTR
		[XedEnumName("MEM")] Memory, // MEM0:cw:SUPP:b
		[XedEnumName("SEG")] MemorySegment, // SEG0=FINAL_ESEG():r:SUPP
		[XedEnumName("BASE")] MemoryBase, // BASE0=ArDI():rcw:SUPP
		[XedEnumName("INDEX")] MemoryIndex, // INDEX=XED_REG_AL:r:SUPP
		[XedEnumName("SCALE")] MemoryScale, // SCALE=1:r:SUPP
		[XedEnumName("AGEN")] AddressGeneration, // AGEN:r, used by LEA
		[XedEnumName("IMM")] Immediate, // IMM0:r:b:i8
		[XedEnumName("PTR")] Pointer, // PTR:r:p
		[XedEnumName("RELBR")] RelativeBranch, // RELBR:r:b:i8
		[XedEnumName("BCAST")] Broadcast, // EMX_BROADCAST_1TO4_32 => BCAST=10
	}

	public static class XedOperandKindEnum
	{
		public static bool IsIndexed(this XedOperandKind kind)
			=> kind == XedOperandKind.Register || kind == XedOperandKind.Memory
			|| kind == XedOperandKind.MemorySegment || kind == XedOperandKind.MemoryBase
			|| kind == XedOperandKind.Immediate;
	}

	public enum XedOperandVisibility : byte
	{
		[XedEnumName("EXPL")]
		Explicit, // Represented in assembly and encoding
		[XedEnumName("IMPL")]
		Implicit, // Represented in assembly but not in an encoding field
		[XedEnumName("SUPP")]
		Suppressed, // Not represented in encoding nor assembly
		[XedEnumName("ECOND")]
		EncoderOnlyCondition // ??
	}

	public enum XedOperandMultiRegKind : byte
	{
		[XedEnumName("SOURCE")] Source,
		[XedEnumName("DEST")] Dest,
		[XedEnumName("SOURCEDEST")] SourceDest
	}

	public readonly struct XedOperandMultiReg
	{
		private readonly byte kindAndIndex;

		public XedOperandMultiReg(XedOperandMultiRegKind kind, byte index)
		{
			this.kindAndIndex = (byte)(((byte)kind << 4) | index);
		}

		public XedOperandMultiRegKind Kind => (XedOperandMultiRegKind)(kindAndIndex >> 4);
		public byte Index => (byte)(kindAndIndex & 0xF);
	}

	public sealed class XedOperand
	{
		[Flags]
		private enum StateFlags : byte { None = 0, HasValue = 1, HasWidth = 2, HasMultiReg = 4 }

		private readonly XedOperandWidth width;
		private readonly XedBlotValue value;
		public XedField Field { get; }
		public string Text { get; }
		private readonly XedXType xtype;
		public XedOperandAccess Access { get; }
		public XedOperandVisibility Visibility { get; }
		private readonly StateFlags stateFlags;
		private readonly XedOperandMultiReg multiReg;

		public XedOperand(XedField field, XedBlotValue? value, XedOperandAccess access,
			XedOperandVisibility visibility, XedOperandWidth? width = null, XedXType? xtype = null,
			XedOperandMultiReg? multiReg = null, string txt = null)
		{
			this.Field = field ?? throw new ArgumentNullException(nameof(field));
			GetKindAndIndex(field.Name); // As validation, throws on failure

			this.value = value.GetValueOrDefault();
			this.Access = access;
			this.Visibility = visibility;
			this.width = width.GetValueOrDefault();
			this.xtype = width.HasValue ? xtype.GetValueOrDefault(this.width.XType) : default;
			this.multiReg = multiReg.GetValueOrDefault();
			this.Text = txt;

			this.stateFlags = StateFlags.None;
			if (value.HasValue) this.stateFlags |= StateFlags.HasValue;
			if (width.HasValue) this.stateFlags |= StateFlags.HasWidth;
			if (multiReg.HasValue) this.stateFlags |= StateFlags.HasMultiReg;
		}

		public XedOperandKind Kind => GetKindAndIndex(Field.Name).Item1;
		public int IndexInKind => GetKindAndIndex(Field.Name).Item2;
		public XedBlotValue? Value => (stateFlags & StateFlags.HasValue) == 0
			? (XedBlotValue?)null : value;
		public XedOperandWidth? Width => (stateFlags & StateFlags.HasWidth) == 0
			? (XedOperandWidth?)null : width;
		public XedXType? XType => (stateFlags & StateFlags.HasWidth) == 0
			? (XedXType?)null : xtype;
		public XedOperandMultiReg? MultiReg => (stateFlags & StateFlags.HasMultiReg) == 0
			? (XedOperandMultiReg?)null : multiReg;

		private static readonly Regex typeRegex = new Regex(
			@"^(?<n>[A-Z]+\d?) (= (?<v>\w+) (?<vc>\(\))? )?$",
			RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace);

		private static readonly Regex multiRegRegex = new Regex(
			@"^MULTI(?<k>SOURCE|DEST|SOURCEDEST)(?<i>[0-9])$");

		public static XedOperand Parse(string str, XedInstructionStringResolvers resolvers)
		{
			str = Regex.Replace(str, @"[\w_]+", match => resolvers.State(match.Value) ?? match.Value);

			var strParts = new List<string>(str.Split(':'));

			// Type field, e.g. "REG1=XMM_N()"
			if (strParts.Count == 0) throw new FormatException();
			var typeMatch = typeRegex.Match(strParts[0]);
			strParts.RemoveAt(0);
			if (!typeMatch.Success) throw new FormatException();

			var field = resolvers.Field(typeMatch.Groups["n"].Value);
			XedBlotValue? value = null;
			if (typeMatch.Groups.TryGetValue("v", out var valueStr))
			{
				if (ushort.TryParse(valueStr, out ushort intValue))
				{
					value = XedBlotValue.MakeConstant(intValue);
				}
				else
				{
					var registerType = (XedRegisterFieldType)field.Type;
					value = typeMatch.Groups["vc"].Success
						? XedBlotValue.MakeCallResult(valueStr)
						: XedBlotValue.MakeConstant(registerType.GetValue(valueStr));
				}
			}

			// Access field (optional)
			XedOperandAccess access = default;
			if (strParts.Count > 0)
			{
				access = XedOperandAccess.Parse(strParts[0]);
				strParts.RemoveAt(0);
			}

			// TXT field (optional, trailing)
			string text = null;
			if (strParts.Count > 0 && strParts[strParts.Count - 1].StartsWith("TXT="))
			{
				text = strParts[strParts.Count - 1].Substring("TXT=".Length);
				strParts.RemoveAt(strParts.Count - 1);
			}

			// Multireg field (optional, trailing)
			XedOperandMultiReg? multiReg = null;
			if (strParts.Count > 0)
			{
				var multiRegMatch = multiRegRegex.Match(strParts[strParts.Count - 1]);
				if (multiRegMatch.Success)
				{
					multiReg = new XedOperandMultiReg(
						XedEnumNameAttribute.GetEnumerantOrNull<XedOperandMultiRegKind>(multiRegMatch.Groups["k"].Value).Value,
						byte.Parse(multiRegMatch.Groups["i"].Value, CultureInfo.InvariantCulture));
					strParts.RemoveAt(strParts.Count - 1);
				}
			}

			// Visibility field (optional)
			XedOperandVisibility? visibility = null;
			if (strParts.Count > 0)
			{
				visibility = TryParseAsVisibility(strParts, 0);
				if (!visibility.HasValue && strParts.Count > 1)
					visibility = TryParseAsVisibility(strParts, strParts.Count - 1);
			}

			if (!visibility.HasValue) visibility = field.DefaultOperandVisibility;

			// Width field (optional)
			XedOperandWidth? width = null;
			XedXType? xtype = null;
			if (strParts.Count > 0)
			{
				width = resolvers.OperandWidth(strParts[0]);
				strParts.RemoveAt(0);

				// XType override part (optional)
				if (strParts.Count > 0)
				{
					try
					{
						xtype = resolvers.XType(strParts[0]);
						strParts.RemoveAt(0);
					}
					catch (KeyNotFoundException) { }
				}
			}

			if (strParts.Count > 0) throw new FormatException();
			return new XedOperand(field, value, access, visibility.Value, width, xtype, multiReg, text);
		}

		private static XedOperandVisibility? TryParseAsVisibility(List<string> strParts, int index)
		{
			var visibility = XedEnumNameAttribute.GetEnumerantOrNull<XedOperandVisibility>(strParts[index]);
			if (!visibility.HasValue) return null;

			strParts.RemoveAt(index);
			return visibility;
		}

		private static (XedOperandKind, int) GetKindAndIndex(string fieldName)
		{
			string kindStr;
			int index;
			if (char.IsDigit(fieldName[fieldName.Length - 1])) // Traling index
			{
				kindStr = fieldName.Substring(0, fieldName.Length - 1);
				index = fieldName[fieldName.Length - 1] - '0';
			}
			else
			{
				kindStr = fieldName;
				index = -1;
			}

			var kind = XedEnumNameAttribute.GetEnumerantOrNull<XedOperandKind>(kindStr).Value;
			if (kind.IsIndexed() && index == -1) throw new FormatException();
			return (kind, Math.Max(0, index));
		}
	}
}
