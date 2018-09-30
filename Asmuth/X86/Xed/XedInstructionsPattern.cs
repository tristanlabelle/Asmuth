using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Xed
{
	public sealed class XedInstructionsPattern : XedPattern
	{
		public IList<XedInstruction> Instructions { get; } = new List<XedInstruction>();

		public XedInstructionsPattern(string name) : base(name) { }
	}
}
