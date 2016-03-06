using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.Debugger
{
	using static Kernel32;

	partial class ProcessDebugger
	{
		public sealed class Thread
		{
			private readonly ProcessDebugger process;
			private readonly int id;

			// Called on worker thread
			internal Thread(ProcessDebugger process, int id)
			{
				Contract.Requires(process != null);
				this.process = process;
				this.id = id;
			}

			public int ID => id;
			public ProcessDebugger Process => process;

			public void RequestResume() => process.service.RequestResumeThread(id);
			public void RequestSuspend() => process.service.RequestSuspendThread(id);

			internal void GetContext(uint flags, out CONTEXT_X86 context)
				=> process.service.GetThreadContext(id, flags, out context);

			internal CONTEXT_X86 GetContext(uint flags)
			{
				CONTEXT_X86 context;
				GetContext(flags, out context);
				return context;
			}
		}
	}
}
