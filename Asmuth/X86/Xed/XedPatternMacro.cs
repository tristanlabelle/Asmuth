using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Asmuth.X86.Xed
{
	public readonly struct XedPatternMacroCase
	{
		public ImmutableArray<XedPatternBlot> Matches { get; }
		public ImmutableArray<XedPatternBlot> Productions { get; }

		public XedPatternMacroCase(ImmutableArray<XedPatternBlot> matches,
			ImmutableArray<XedPatternBlot> productions)
		{
			this.Matches = matches;
			this.Productions = productions;
		}
	}

	public sealed class XedPatternMacro
	{
		public string Name { get; }
		public bool ReturnsRegister { get; }
		public ImmutableArray<XedPatternMacroCase> Cases { get; }

		public XedPatternMacro(string name, bool returnsRegister, ImmutableArray<XedPatternMacroCase> cases)
		{
			this.Name = name;
			this.ReturnsRegister = returnsRegister;
			this.Cases = cases;
		}
	}

}
