using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Xed
{
	public abstract class XedPattern
	{
		public string Name { get; }

		internal XedPattern(string name)
		{
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
		}

		public override string ToString() => Name + "()";
	}
}
