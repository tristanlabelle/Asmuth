using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Xed
{
	public sealed class XedDatabase
	{
		public XedRegisterTable RegisterTable { get; } = new XedRegisterTable();
		public IEmbeddedKeyCollection<XedInstructionsPattern, string> InstructionsPatterns { get; }
			= new EmbeddedKeyCollection<XedInstructionsPattern, string>(p => p.Name);
		public IEmbeddedKeyCollection<XedRulePattern, string> RulePatterns { get; }
			= new EmbeddedKeyCollection<XedRulePattern, string>(p => p.Name);

		public XedPattern FindPattern(string name)
		{
			InstructionsPatterns.TryFind(name, out var i);
			RulePatterns.TryFind(name, out var r);
			if (i == null && r == null) return null;
			if (i != null && r != null) throw new ArgumentException();
			return (XedPattern)i ?? r;
		}
	}
}
