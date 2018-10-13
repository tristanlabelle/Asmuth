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
		public ImmutableArray<XedBlot> Lhs { get; }
		public ImmutableArray<XedBlot> Rhs { get; }
		public bool IsEncode { get; }
		public XedRulePatternControlFlow ControlFlow { get; }

		public XedRulePatternCase(ImmutableArray<XedBlot> lhs, ImmutableArray<XedBlot> rhs,
			bool isEncode, XedRulePatternControlFlow controlFlow)
		{
			this.Lhs = lhs;
			this.Rhs = rhs;
			this.IsEncode = isEncode;
			this.ControlFlow = controlFlow;
		}

		public XedBlot? OutRegBlot
		{
			get
			{
				foreach (var blot in (IsEncode ? Lhs : Rhs))
					if (blot.Type == XedBlotType.Equality && blot.Field.Name == "OUTREG")
						return blot;
				return null;
			}
		}

		public override string ToString()
		{
			var str = new StringBuilder();

			if (Lhs.Length == 0) str.Append("otherwise");
			else
			{
				for (int i = 0; i < Lhs.Length; ++i)
				{
					if (i > 0) str.Append(' ');
					str.Append(Lhs[i].ToString());
				}
			}

			str.Append(IsEncode ? " -> " : " | ");

			if (Rhs.Length == 0 && IsEncode) str.Append("nothing");
			else
			{
				for (int i = 0; i < Rhs.Length; ++i)
				{
					if (i > 0) str.Append(' ');
					str.Append(Rhs[i].ToString());
				}
			}

			return str.ToString();
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

			bool? isEncode = null;
			foreach (var @case in cases)
			{
				if (!isEncode.HasValue) isEncode = @case.IsEncode;
				else if (@case.IsEncode != isEncode) throw new ArgumentException();

				var outRegBlot = @case.OutRegBlot;
				if (outRegBlot.HasValue != ReturnsRegister) throw new ArgumentException();
				Cases.Add(@case);
			}
		}

		public bool IsEncode => Cases[0].IsEncode;
	}
}
