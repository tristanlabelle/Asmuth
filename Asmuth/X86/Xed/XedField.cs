using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Xed
{
	public enum XedFieldFlags : byte
	{
		None = 0,
		[XedEnumName("SUPPRESSED", manyToOne: true)] SuppressedVisibility = 0,
		[XedEnumName("EXPLICIT")] ExplicitVisibility = 1 << 0,
		[XedEnumName("NOPRINT", manyToOne: true)] NoPrint = 0,
		[XedEnumName("PRINT")] Print = 1 << 1,
		[XedEnumName("INTERNAL", manyToOne: true)] Internal = 0,
		[XedEnumName("PUBLIC")] Public = 1 << 2,
		[XedEnumName("DS", manyToOne: true)] DecoderSkip = 0,
		[XedEnumName("DI")] DecoderInput = 1 << 3,
		[XedEnumName("DO")] DecoderOutput = 1 << 4,
		[XedEnumName("EI", manyToOne: true)] EncoderInput = 0,
		[XedEnumName("EO")] EncoderOutput = 1 << 5
	}

	public sealed class XedField
	{
		public string Name { get; }
		public XedFieldType Type { get; }
		public XedFieldFlags Flags { get; }

		public XedField(string name, XedFieldType type, XedFieldFlags flags)
		{
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
			this.Type = type ?? throw new ArgumentNullException(nameof(type));
			this.Flags = flags;
		}

		public int SizeInBits => Type.SizeInBits;

		public override string ToString() => Name;
	}
}
