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
		int ID { get; }
		SafeFileHandle ImageHandle { get; }
		ForeignPtr ImageBase { get; }
		void RequestBreak();
		void RequestContinue();
		void RequestSuspendThread(int id);
		void RequestResumeThread(int id);
		void RequestTerminate(int exitCode);
		void RequestTerminateThread(int id, int exitCode);
		void ReadMemory(ForeignPtr source, IntPtr dest, int length);
		void WriteMemory(IntPtr source, ForeignPtr dest, int length);
		void GetThreadContext(int id, uint flags, out CONTEXT_X86 context);
	}

	internal interface IDebugEventListener
	{
		void OnAttachFailed(Exception exception);
		DebugEventResponse OnAttachSucceeded(int threadID, IProcessDebuggerService service);

		DebugEventResponse OnChildProcessCreated(int threadID,
			IProcessDebuggerService child, out IDebugEventListener childListener);

		DebugEventResponse OnException(int threadID, ExceptionRecord record);
		DebugEventResponse OnModuleLoaded(int threadID, ForeignPtr @base, SafeFileHandle handle);
		DebugEventResponse OnModuleUnloaded(int threadID, ForeignPtr @base);
		DebugEventResponse OnProcessExited(int threadID, int exitCode);
		DebugEventResponse OnStringOutputted(int threadID, string str);
		DebugEventResponse OnThreadCreated(int threadID, ForeignPtr entryPoint);
		DebugEventResponse OnThreadExited(int threadID, int exitCode);
	}
}
