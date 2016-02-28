using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace Asmuth.Debugger
{
	using static Kernel32;
	
	internal interface IDebuggerService : IDisposable
	{
		void AttachToProcess(int id, IDebugEventListener listener);
	}

	internal interface IProcessDebuggerService : IDisposable
	{
		CREATE_PROCESS_DEBUG_INFO ProcessDebugInfo { get; }
		void RequestBreak();
		void RequestContinue();
		void RequestSuspendThread(int id);
		void RequestResumeThread(int id);
		void RequestTerminate(int exitCode);
		void RequestTerminateThread(int id, int exitCode);
		void ReadMemory(ulong address, int count, IntPtr buffer);
		void WriteMemory(ulong address, int count, IntPtr buffer);
	}

	internal interface IDebugEventListener
	{
		void OnAttachFailed(Exception exception);
		DebugEventResponse OnAttachSucceeded(int threadID, IProcessDebuggerService service);

		DebugEventResponse OnChildProcessCreated(int threadID,
			IProcessDebuggerService child, out IDebugEventListener childListener);

		DebugEventResponse OnException(int threadID, ExceptionRecord record);
		DebugEventResponse OnModuleLoaded(int threadID, ulong baseAddres, SafeFileHandle handle);
		DebugEventResponse OnModuleUnloaded(int threadID, ulong baseAddress);
		DebugEventResponse OnProcessExited(int threadID, int exitCode);
		DebugEventResponse OnStringOutputted(int threadID, string str);
		DebugEventResponse OnThreadCreated(int threadID, CREATE_THREAD_DEBUG_INFO info);
		DebugEventResponse OnThreadExited(int threadID, int exitCode);
	}
}
