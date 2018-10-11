using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Xed
{
	public sealed partial class XedDatabase
	{
		public XedRegisterTable RegisterTable { get; } = new XedRegisterTable();
		public EmbeddedKeyCollection<XedField, string> Fields { get; }
			= new EmbeddedKeyCollection<XedField, string>(f => f.Name);
		public EmbeddedKeyCollection<XedSequence, string> EncodeSequences { get; }
			= new EmbeddedKeyCollection<XedSequence, string>(p => p.Name);
		public EmbeddedKeyCollection<XedPattern, string> EncodePatterns { get; }
			= new EmbeddedKeyCollection<XedPattern, string>(p => p.Name);
		public EmbeddedKeyCollection<XedPattern, string> EncodeDecodePatterns { get; }
			= new EmbeddedKeyCollection<XedPattern, string>(p => p.Name);
		public EmbeddedKeyCollection<XedPattern, string> DecodePatterns { get; }
			= new EmbeddedKeyCollection<XedPattern, string>(p => p.Name);

		public XedPattern FindPattern(string name, bool encode)
		{
			XedPattern pattern;
			if (!(encode ? EncodePatterns : DecodePatterns).TryFind(name, out pattern))
				EncodeDecodePatterns.TryFind(name, out pattern);
			return pattern;
		}
	}
}
