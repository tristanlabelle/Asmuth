using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Xed
{
	public abstract class XedCallable
	{
		public string Name { get; }

		internal XedCallable(string name)
		{
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
		}

		public override string ToString() => Name;
	}
}
