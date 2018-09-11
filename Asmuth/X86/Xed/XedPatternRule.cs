using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Asmuth.X86.Xed
{
	public readonly struct XedPatternRuleCase
	{
		public ImmutableArray<XedBlot> Conditions { get; }
		public ImmutableArray<XedBlot> Actions { get; }

		public XedPatternRuleCase(ImmutableArray<XedBlot> conditions,
			ImmutableArray<XedBlot> actions)
		{
			this.Conditions = conditions;
			this.Actions = actions;
		}
	}

	public sealed class XedPatternRule
	{
		public string Name { get; }
		public bool ReturnsRegister { get; }
		public ImmutableArray<XedPatternRuleCase> Cases { get; }

		public XedPatternRule(string name, bool returnsRegister, ImmutableArray<XedPatternRuleCase> cases)
		{
			this.Name = name;
			this.ReturnsRegister = returnsRegister;
			this.Cases = cases;
		}
	}

}
