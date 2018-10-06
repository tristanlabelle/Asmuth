using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Asmuth.X86.Xed
{
	public readonly struct XedRulePatternCase
	{
		public ImmutableArray<XedBlot> Conditions { get; }
		public ImmutableArray<XedBlot> Actions { get; }
		public bool Reset { get; }

		public XedRulePatternCase(ImmutableArray<XedBlot> conditions,
			ImmutableArray<XedBlot> actions, bool reset)
		{
			this.Conditions = conditions;
			this.Actions = actions;
			this.Reset = reset;
		}

		public XedBlot? OutRegBlot
		{
			get
			{
				foreach (var blot in Actions)
				{
					if (blot.Type != XedBlotType.Equality || blot.Field.Name != "OUTREG") continue;
					return blot;
				}
				return null;
			}
		}
	}

	// XED patterns aka rules
	public sealed class XedRulePattern : XedPattern
	{
		public bool ReturnsRegister { get; }
		public IList<XedRulePatternCase> Cases { get; } = new List<XedRulePatternCase>();

		public XedRulePattern(string name, bool returnsRegister, IEnumerable<XedRulePatternCase> cases)
			: base(name)
		{
			this.ReturnsRegister = returnsRegister;

			foreach (var @case in cases)
			{
				var outRegBlot = @case.OutRegBlot;
				if (outRegBlot.HasValue != ReturnsRegister) throw new ArgumentException();
				Cases.Add(@case);
			}
		}
	}

}
