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

		public XedAssignmentBlot? OutRegBlot
		{
			get
			{
				foreach (var blot in Actions)
				{
					if (blot.Type != XedBlotType.Assignment) continue;
					var assignment = blot.Assignment;
					if (assignment.Field != "OUTREG") continue;
					return assignment;
				}
				return null;
			}
		}
	}

	public sealed class XedRulePattern : XedPattern
	{
		public bool ReturnsRegister { get; }
		public ImmutableArray<XedRulePatternCase> Cases { get; }

		public XedRulePattern(string name, bool returnsRegister,
			ImmutableArray<XedRulePatternCase> cases) : base(name)
		{
			this.ReturnsRegister = returnsRegister;
			this.Cases = cases;

			foreach (var @case in cases)
			{
				var outRegBlot = @case.OutRegBlot;
				if (outRegBlot.HasValue != ReturnsRegister) throw new ArgumentException();
					
			}
		}
	}

}
