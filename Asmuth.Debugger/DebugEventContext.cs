using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.Debugger
{
	[DebuggerDisplay("process {ProcessID}, thread {ThreadID}")]
	public struct DebugEventContext
	{
		public int ProcessID { get; }
		public int ThreadID { get; }

		public DebugEventContext(int processID, int threadID)
		{
			this.ProcessID = processID;
			this.ThreadID = threadID;
		}
	}
}
