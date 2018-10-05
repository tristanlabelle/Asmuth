using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Xed
{
	public sealed class XedInstructionTable : XedCallable
	{
		public IList<XedInstruction> Instructions { get; } = new List<XedInstruction>();

		public XedInstructionTable(string name) : base(name) { }
	}
}
