using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Asmuth.X86.Xed
{
	public sealed class XedSequence : XedPattern
	{
		public ImmutableArray<XedSequenceEntry> Entries { get; }

		public XedSequence(string name, ImmutableArray<XedSequenceEntry> entries)
			: base(name)
		{
			this.Entries = entries;
		}

		public override string ToString() => "SEQUENCE " + Name;
	}

	public enum XedSequenceEntryType : byte
	{
		Sequence,
		RulePattern
	}

	public readonly struct XedSequenceEntry
	{
		public string TargetName { get; }
		public XedSequenceEntryType Type { get; }

		public XedSequenceEntry(string targetName, XedSequenceEntryType type)
		{
			this.TargetName = targetName;
			this.Type = type;
		}

		public override string ToString() => TargetName;
	}
}
