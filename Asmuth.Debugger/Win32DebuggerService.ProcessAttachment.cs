using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.Debugger
{
	using System.Collections.Concurrent;
	using static Kernel32;
	using static NativeMethods;

	partial class Win32DebuggerService
	{
		private sealed class ProcessAttachment : IProcessDebuggerService
		{
			private readonly IDebugEventListener listener;
			private readonly ConcurrentDictionary<int, IntPtr> threadIDsToHandles
				= new ConcurrentDictionary<int, IntPtr>();
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

			public void RequestSuspendThread(int id)
			{
				IntPtr handle;
				if (threadIDsToHandles.TryGetValue(id, out handle))
					CheckWin32(SuspendThread(handle) < uint.MaxValue);
			}

			public void RequestResumeThread(int id)
			{
				IntPtr handle;
				if (threadIDsToHandles.TryGetValue(id, out handle))
					CheckWin32(ResumeThread(handle) < uint.MaxValue);
			}

			public void RequestTerminate(int exitCode)
			{
				CheckWin32(TerminateProcess(debugInfo.hProcess, unchecked((uint)exitCode)));
			}

			public void RequestTerminateThread(int id, int exitCode)
			{
				IntPtr handle;
				if (threadIDsToHandles.TryGetValue(id, out handle))
					CheckWin32(TerminateThread(handle, unchecked((uint)exitCode)));
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

			internal DebugEventResponse OnThreadCreated(int threadID, CREATE_THREAD_DEBUG_INFO debugInfo)
			{
				bool added = threadIDsToHandles.TryAdd(threadID, debugInfo.hThread);
				Contract.Assert(added);
				return listener.OnThreadCreated(threadID, unchecked((ulong)debugInfo.lpStartAddress));
			}

			internal DebugEventResponse OnThreadExited(int threadID, EXIT_THREAD_DEBUG_INFO debugInfo)
			{
				IntPtr handle;
				bool removed = threadIDsToHandles.TryRemove(threadID, out handle);
				Contract.Assert(removed);

				return listener.OnThreadExited(threadID, unchecked((int)debugInfo.dwExitCode));
			}

			internal DebugEventResponse OnAttachSucceeded(int threadID, CREATE_PROCESS_DEBUG_INFO debugInfo)
			{
				this.debugInfo = debugInfo;
				return listener.OnAttachSucceeded(threadID, this);
			}
		}
	}
}
