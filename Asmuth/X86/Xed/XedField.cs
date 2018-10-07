using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Asmuth.X86.Xed
{
	public enum XedFieldUsage : byte
	{
		Input,
		Output,
		Skip
	}

	public sealed class XedField
	{
		public string Name { get; }
		public XedFieldType Type { get; }
		public XedOperandVisibility DefaultOperandVisibility { get; }
		public bool IsPrintable { get; }
		public bool IsPublic { get; }
		public XedFieldUsage DecoderUsage { get; }
		public XedFieldUsage EncoderUsage { get; }

		public XedField(string name, XedFieldType type, XedOperandVisibility defaultOperandVisibility,
			bool isPrintable, bool isPublic, XedFieldUsage decoderUsage, XedFieldUsage encoderUsage)
		{
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
			this.Type = type ?? throw new ArgumentNullException(nameof(type));
			this.DefaultOperandVisibility = defaultOperandVisibility;
			this.IsPrintable = IsPrintable;
			this.IsPublic = isPublic;
			this.DecoderUsage = decoderUsage;
			this.EncoderUsage = encoderUsage;
		}
		
		public int SizeInBits => Type.SizeInBits;

		public override string ToString() => Name;

		private static readonly string[] encoderFieldUsages = { "EI", "EO" };
		private static readonly string[] decoderFieldUsages = { "DI", "DO", "DS" };

		public static XedOperandVisibility ParseDefaultOperandVisilibity(string str)
		{
			if (str == "SUPPRESSED") return XedOperandVisibility.Suppressed;
			if (str == "EXPLICIT") return XedOperandVisibility.Explicit;
			throw new FormatException();
		}

		public static XedFieldUsage ParseDecoderUsage(string str) => ParseUsage(str, decoderFieldUsages);
		public static XedFieldUsage ParseEncoderUsage(string str) => ParseUsage(str, encoderFieldUsages);

		private static XedFieldUsage ParseUsage(string str, string[] usages)
		{
			for (int i = 0; i < usages.Length; ++i)
				if (str == usages[i])
					return (XedFieldUsage)i;
			throw new FormatException();
		}
	}
}
