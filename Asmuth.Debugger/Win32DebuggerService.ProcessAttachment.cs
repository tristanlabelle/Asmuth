using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.Debugger
{
	using static Kernel32;
	using static NativeMethods;

	partial class Win32DebuggerService
	{
		private sealed class ProcessAttachment : IProcessDebuggerService
		{
			private readonly IDebugEventListener listener;
			private CREATE_PROCESS_DEBUG_INFO debugInfo;

			public ProcessAttachment(IDebugEventListener listener)
			{
				this.listener = listener;
			}

			public IDebugEventListener Listener => listener;
			public CREATE_PROCESS_DEBUG_INFO ProcessDebugInfo => debugInfo;

			public void RequestBreak()
			{
				CheckWin32(DebugBreakProcess(debugInfo.hProcess));
			}

			public void RequestContinue()
			{
				throw new NotImplementedException();
			}

			public void RequestSuspendThread(int threadID)
			{
				throw new NotImplementedException();
			}

			public void RequestResumeThread(int id)
			{
				throw new NotImplementedException();
			}

			public void RequestTerminate(int exitCode)
			{
				CheckWin32(TerminateProcess(debugInfo.hProcess, unchecked((uint)exitCode)));
			}

			public void RequestTerminateThread(int id, int exitCode)
			{
				throw new NotImplementedException();
			}

			public void ReadMemory(ulong address, int count, IntPtr buffer)
			{
				throw new NotImplementedException();
			}

			public void WriteMemory(ulong address, int count, IntPtr buffer)
			{
				throw new NotImplementedException();
			}

			public void Dispose()
			{
				var processID = GetProcessId(debugInfo.hProcess);
				CheckWin32(DebugActiveProcessStop(processID));
				// TODO: will the worker thread get a callback?
			}

			internal DebugEventResponse OnAttachSucceeded(int threadID, CREATE_PROCESS_DEBUG_INFO debugInfo)
			{
				this.debugInfo = debugInfo;
				return listener.OnAttachSucceeded(threadID, this);
			}
		}
	}
}
