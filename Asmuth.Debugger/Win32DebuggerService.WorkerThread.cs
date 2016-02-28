using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.Debugger
{
	using static Kernel32;
	using static NativeMethods;

	partial class Win32DebuggerService
	{
		private void WorkerThreadMain(uint rootProcessID, IDebugEventListener rootListener)
		{
			if (!DebugActiveProcess(rootProcessID))
			{
				rootListener.OnAttachFailed(GetLastWin32Exception());
				return;
			}

			var processAttachments = new Dictionary<uint, ProcessAttachment>();
			var rootProcessAttachment = new ProcessAttachment(rootListener);
			processAttachments.Add(rootProcessID, rootProcessAttachment);

			while (processAttachments.Count > 0)
			{
				DEBUG_EVENT debugEvent;
				CheckWin32(WaitForDebugEventEx(out debugEvent, INFINITE));

				ProcessAttachment processAttachment;
				if (!processAttachments.TryGetValue(debugEvent.dwProcessId, out processAttachment))
				{
					// Unexpected, we should have been asked to attach to the process
					throw new InvalidOperationException();
				}

				// If we've just attached to a new process, complete the attach event.
				var listener = processAttachment.Listener;
				int threadID = unchecked((int)debugEvent.dwThreadId);
				DebugEventResponse response;
				switch (debugEvent.dwDebugEventCode)
				{
					case EXCEPTION_DEBUG_EVENT:
						{
							var record = IntPtr.Size == sizeof(int)
								? ExceptionRecord.FromStruct(ref debugEvent.Exception32.ExceptionRecord)
								: ExceptionRecord.FromStruct(ref debugEvent.Exception64.ExceptionRecord);
							response = listener.OnException(threadID, record);
						}
						break;

					case CREATE_PROCESS_DEBUG_EVENT:
						response = processAttachment.OnAttachSucceeded(threadID, debugEvent.CreateProcessInfo);
						break;

					case CREATE_THREAD_DEBUG_EVENT:
						response = listener.OnThreadCreated(threadID, debugEvent.CreateThread);
						break;

					case EXIT_THREAD_DEBUG_EVENT:
						response = listener.OnThreadExited(threadID, unchecked((int)debugEvent.ExitThread.dwExitCode));
						break;

					case LOAD_DLL_DEBUG_EVENT:
						// TODO: Additional parameters
						response = listener.OnModuleLoaded(threadID, unchecked((ulong)debugEvent.LoadDll.lpBaseOfDll), null);
						break;

					case OUTPUT_DEBUG_STRING_EVENT:
						{
							var str = debugEvent.DebugString.fUnicode == 0
								? Marshal.PtrToStringAnsi(debugEvent.DebugString.lpDebugStringData, debugEvent.DebugString.nDebugStringLength)
								: Marshal.PtrToStringUni(debugEvent.DebugString.lpDebugStringData, debugEvent.DebugString.nDebugStringLength);
							response = listener.OnStringOutputted(threadID, str);
						}
						break;

					case UNLOAD_DLL_DEBUG_EVENT:
						response = listener.OnModuleUnloaded(threadID, (ulong)debugEvent.UnloadDll.lpBaseOfDll);
						break;

					default:
						Debug.Fail("Unknown debug code.");
						response = DebugEventResponse.ContinueUnhandled;
						break;
				}

				if (response != DebugEventResponse.Break)
				{
					bool unhandledException = response == DebugEventResponse.ContinueUnhandled
						&& debugEvent.dwDebugEventCode == EXCEPTION_DEBUG_EVENT;
					CheckWin32(ContinueDebugEvent(debugEvent.dwProcessId, debugEvent.dwThreadId,
						unhandledException ? DBG_EXCEPTION_NOT_HANDLED : DBG_CONTINUE));
				}
			}
		}
	}
}
