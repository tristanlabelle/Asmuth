using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Asmuth.X86.Xed
{
	public enum XedRulePatternControlFlow : byte
	{
		Break, // Typical case, exit pattern after matching case
		Continue, // Keep matching more cases
		Reset, // Reset the decoder, preserving field states
	}

	public readonly struct XedRulePatternCase
	{
		public ImmutableArray<XedBlot> Conditions { get; }
		public ImmutableArray<XedBlot> Actions { get; }
		public XedRulePatternControlFlow ControlFlow { get; }

		public XedRulePatternCase(ImmutableArray<XedBlot> conditions,
			ImmutableArray<XedBlot> actions, XedRulePatternControlFlow controlFlow)
		{
			this.Conditions = conditions;
			this.Actions = actions;
			this.ControlFlow = controlFlow;
		}

		public XedBlot? TryGetOutRegBlot(bool isEncode)
		{
			foreach (var blot in (isEncode ? Conditions : Actions))
			{
				if (blot.Type != XedBlotType.Equality || blot.Field.Name != "OUTREG") continue;
				return blot;
			}
			return null;
		}

		public override string ToString()
		{
			if (Conditions.Length == 0) return "otherwise";

			var str = new StringBuilder();
			foreach (var blot in Conditions)
			{
				if (str.Length > 0) str.Append(' ');
				str.Append(blot.ToString());
			}
			return str.ToString();
		}
	}

	// XED patterns aka rules
	public sealed class XedRulePattern : XedPattern
	{
		public bool ReturnsRegister { get; }
		public bool IsEncode { get; }
		public IList<XedRulePatternCase> Cases { get; } = new List<XedRulePatternCase>();

		public XedRulePattern(string name, bool returnsRegister, bool isEncode, IEnumerable<XedRulePatternCase> cases)
			: base(name)
		{
			this.ReturnsRegister = returnsRegister;
			this.IsEncode = isEncode;

			foreach (var @case in cases)
			{
				var outRegBlot = @case.TryGetOutRegBlot(isEncode);
				if (outRegBlot.HasValue != ReturnsRegister) throw new ArgumentException();
				Cases.Add(@case);
			}
		}
	}

}
