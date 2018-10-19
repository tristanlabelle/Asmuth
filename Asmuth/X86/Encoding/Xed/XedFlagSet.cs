using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;

namespace Asmuth.X86.Encoding.Xed
{
	public enum XedFlag : byte
	{
		[XedEnumName("cf")] Carry,
		[XedEnumName("pf")] Parity,
		[XedEnumName("af")] af,
		[XedEnumName("zf")] Zero,
		[XedEnumName("sf")] Sign,
		[XedEnumName("tf")] tf,
		[XedEnumName("if")] @if,
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
		public ImmutableArray<KeyValuePair<XedFlag, XedFlagAction>> FlagActions { get; }
		
		internal XedFlagsRecord(XedFlagsRecordQualifier? qualifier, XedFlagsRecordSemantic semantic,
			ImmutableArray<KeyValuePair<XedFlag, XedFlagAction>> flagActions)
		{
			this.Qualifier = qualifier;
			this.Semantic = semantic;
			this.FlagActions = flagActions;
		}

		private static readonly Regex parseRegex = new Regex(
			@"^\s* ((?<q>\w+)\s+)? (?<s>\w+) \s* \[ \s* ((?<f>\w+)-(?<a>\w+)\s*)* \] \s*$",
			RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace);

		public static XedFlagsRecord Parse(string str)
		{
			// IMMx MUST [ of-u sf-mod zf-mod af-u pf-mod cf-mod ]
			if (str == null) throw new ArgumentNullException(nameof(str));

			var match = parseRegex.Match(str);
			if (match == null) throw new FormatException();

			XedFlagsRecordQualifier? qualifier = null;
			if (match.Groups.TryGetValue("q", out var qualifierStr))
			{
				if (!Enum.TryParse(qualifierStr, ignoreCase: true, out XedFlagsRecordQualifier parsedQualifier))
					throw new FormatException();
				qualifier = parsedQualifier;
			}

			if (!Enum.TryParse(match.Groups["s"].Value, ignoreCase: true, out XedFlagsRecordSemantic semantic))
				throw new FormatException();

			var flagCaptures = match.Groups["f"].Captures;
			var actionCaptures = match.Groups["a"].Captures;
			var flagActionsBuilder = ImmutableArray.CreateBuilder<KeyValuePair<XedFlag, XedFlagAction>>(flagCaptures.Count);
			for (int i = 0; i < flagCaptures.Count; ++i)
			{
				var flag = XedEnumNameAttribute.GetEnumerantOrNull<XedFlag>(flagCaptures[i].Value).Value;
				var action = XedEnumNameAttribute.GetEnumerantOrNull<XedFlagAction>(actionCaptures[i].Value).Value;
				flagActionsBuilder.Add(new KeyValuePair<XedFlag, XedFlagAction>(flag, action));
			}

			return new XedFlagsRecord(qualifier, semantic, flagActionsBuilder.MoveToImmutable());
		}
	}
}
