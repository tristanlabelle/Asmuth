using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Asmuth.X86.Xed
{
	public enum XedFlag : byte
	{
		[XedEnumName("cf")] Carry,
		[XedEnumName("pf")] Parity,
		[XedEnumName("af")] af,
		[XedEnumName("zf")] Zero,
		[XedEnumName("sf")] Sign,
		[XedEnumName("tf")] tf,
		[XedEnumName("_if")] @if,
		[XedEnumName("df")] df,
		[XedEnumName("of")] Overflow,
		[XedEnumName("iopl")] iopl, // 2 bits
		[XedEnumName("nt")] nt,
		[XedEnumName("rf")] rf,
		[XedEnumName("vm")] vm,
		[XedEnumName("ac")] ac,
		[XedEnumName("vif")] vif,
		[XedEnumName("vip")] vip,
		[XedEnumName("id")] id,
		[XedEnumName("fc0")] fc0,
		[XedEnumName("fc1")] fc1,
		[XedEnumName("fc2")] fc2,
		[XedEnumName("fc3")] fc3,
		[XedEnumName("must_be_1")] MustBe1,
		[XedEnumName("must_be_0a")] MustBe0A,
		[XedEnumName("must_be_0b")] MustBe0B,
		[XedEnumName("must_be_0c")] MustBe0C,
		[XedEnumName("must_be_0d")] MustBe0D, // 2 bits
		[XedEnumName("must_be_0e")] MustBe0E, // 4 bits
	}

	public enum XedFlagAction : byte
	{
		[XedEnumName("u")] Undefined, // Treated as a write
		[XedEnumName("tst")] Test, // Read
		[XedEnumName("mod")] Modify, // Write
		[XedEnumName("0")] Zero, // Write
		[XedEnumName("1")] One, // Write
		[XedEnumName("pop")] Pop, // Write
		[XedEnumName("ah")] AH, // Write
	}

	public enum XedFlagsRecordSemantic : byte
	{
		Readonly, Must, May
	}

	public enum XedFlagsRecordQualifier : byte
	{
		Rep, NoRep, Imm0, Imm1, ImmX
	}

	public readonly struct XedFlagsRecord
	{
		public XedFlagsRecordQualifier? Qualifier { get; }
		public XedFlagsRecordSemantic Semantic { get; }
		public IReadOnlyDictionary<XedFlag, XedFlagAction> Flags { get; }
	}
}
