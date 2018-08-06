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
				if (process == null) throw new ArgumentNullException(nameof(process));
				this.process = process;
				this.id = id;
			}

			public int ID => id;
			public ProcessDebugger Process => process;

			public void RequestResume() => process.service.RequestResumeThread(id);
			public void RequestSuspend() => process.service.RequestSuspendThread(id);

			internal void GetContext(uint flags, out X86.CONTEXT context)
				=> process.service.GetThreadContext(id, flags, out context);

			internal X86.CONTEXT GetContext(uint flags)
			{
				X86.CONTEXT context;
				GetContext(flags, out context);
				return context;
			}
		}
	}
}
