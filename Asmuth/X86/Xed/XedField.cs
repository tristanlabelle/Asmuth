using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Xed
{
	public enum XedFieldType : byte
	{
		Int,
		UInt,
		[XedEnumName("xed_bits_t")] Bits,
		[XedEnumName("xed_chip_enum_t")] ChipEnumerant,
		[XedEnumName("xed_reg_enum_t")] RegisterEnumerant,
		[XedEnumName("xed_error_enum_t")] ErrorEnumerant,
		[XedEnumName("xed_iclass_enum_t")] InstructionClassEnumerant
	}

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
		// MOD            SCALAR     xed_bits_t 2             SUPPRESSED  NOPRINT INTERNAL DO EO
		public string Name { get; }
		public XedFieldType Type { get; }
		private readonly byte sizeInBits;
		public int SizeInBits => sizeInBits;
		public XedFieldFlags Flags { get; }

		public XedField(string name, XedFieldType type, int sizeInBits, XedFieldFlags flags)
		{
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
			this.Type = type;
			this.sizeInBits = (byte)sizeInBits;
			this.Flags = flags;
		}

		public override string ToString() => Name;
	}
}
