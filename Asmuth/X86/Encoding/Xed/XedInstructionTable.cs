using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Encoding.Xed
{
	public sealed class XedInstructionTable : XedPattern
	{
		public IList<XedInstruction> Instructions { get; } = new List<XedInstruction>();

		public XedInstructionTable(string name) : base(name) { }
	}
}
