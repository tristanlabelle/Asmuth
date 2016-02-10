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
		private readonly CREATE_THREAD_DEBUG_INFO debugInfo;
		private volatile bool isRunning = true;
		private volatile object exitCode;

		// Called on worker thread
		internal ThreadDebugger(ProcessDebugger process, CREATE_THREAD_DEBUG_INFO debugInfo)
		{
			Contract.Requires(process != null);
			Contract.Requires(debugInfo.hThread != IntPtr.Zero);
			this.process = process;
			this.debugInfo = debugInfo;
		}
		
		public int ID => unchecked((int)GetThreadId(debugInfo.hThread));
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
			CheckWin32(GetThreadContext(debugInfo.hThread, ref context));
			return context;
		}

		// Called on either thread
		internal void Dispose()
		{
			CloseHandle(debugInfo.hThread);
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

		internal void OnExited(uint exitCode)
		{
			isRunning = false;
			this.exitCode = (object)exitCode;
		}
	}
}
