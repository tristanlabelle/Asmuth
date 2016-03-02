using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.Debugger
{
	using static NativeMethods;
	using static Kernel32;

	public sealed class ThreadDebugger
	{
		private readonly ProcessDebugger process;
		private readonly int id;
		private volatile bool isRunning = true;

		// Called on worker thread
		internal ThreadDebugger(ProcessDebugger process, int id)
		{
			Contract.Requires(process != null);
			this.process = process;
			this.id = id;
		}
		
		public int ID => id;
		public ProcessDebugger Process => process;
		public bool IsRunning => isRunning;

		public void Continue(bool handled = true)
		{
			Contract.Requires(!IsRunning);
			isRunning = true;

			// TODO: Move to worker thread
			CheckWin32(ContinueDebugEvent(
				unchecked((uint)process.ID),
				unchecked((uint)ID),
				handled ? DBG_CONTINUE : DBG_EXCEPTION_NOT_HANDLED));
		}

		internal CONTEXT_X86 GetContext(uint flags)
		{
			var context = new CONTEXT_X86();
			context.ContextFlags = flags;
			throw new NotImplementedException();
		}
		
		// Called on worker thread
		internal void OnBroken()
		{
			isRunning = false;
		}

		// Called on worker thread
		internal void OnContinued()
		{
			isRunning = true;
		}
	}
}
