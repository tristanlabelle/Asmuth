using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.Debugger
{
	public sealed class ProcessDebuggerEventArgs<TData> : EventArgs
	{
		private readonly TData data;
		private bool @break;

		public ProcessDebuggerEventArgs(TData data)
		{
			this.data = data;
		}

		public TData Data => data;
		public bool Break => @break;
	}
}
