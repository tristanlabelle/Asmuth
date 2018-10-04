using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Xed
{
	public enum XedBlotKind : byte
	{
		None,
		Assignment,
		Equal,
		NotEqual
	}

	public readonly struct XedBlot2
	{
		public XedField Field { get; }
		public string BitsPattern { get; }
		private readonly string callee;
		private readonly byte value;
		public XedBlotKind Kind { get; }
	}
}
