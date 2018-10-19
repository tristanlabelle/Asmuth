using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Encoding.Xed
{
	public abstract class XedSymbol
	{
		public string Name { get; }

		internal XedSymbol(string name)
		{
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
		}

		public override string ToString() => Name;
	}

	/// <summary>
	/// Base class for rule and instruction patterns.
	/// </summary>
	public abstract class XedPattern : XedSymbol
	{
		internal XedPattern(string name) : base(name) { }
	}
}
