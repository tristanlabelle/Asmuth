using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Asmuth.Debugger
{
	using static NativeMethods;
	using static Kernel32;

	public sealed partial class ProcessDebugger
	{
		private readonly bool initialBreak;
		private readonly EventListener eventListener;
		private readonly TaskCompletionSource<ProcessDebugger> attachTaskCompletionSource
			= new TaskCompletionSource<ProcessDebugger>();
		private IProcessDebuggerService processDebuggerService;

		private readonly ConcurrentDictionary<int, ThreadDebugger> threadsByID = new ConcurrentDictionary<int, ThreadDebugger>();
		private readonly ConcurrentDictionary<ulong, ProcessModule> modulesByBaseAddress = new ConcurrentDictionary<ulong, ProcessModule>();
		private readonly Synchronized<TaskCompletionSource<ThreadDebugger>> synchronizedBreakTaskCompletionSource
			= new Synchronized<TaskCompletionSource<ThreadDebugger>>();

		// Called on worker thread
		private ProcessDebugger(bool initialBreak)
		{
			this.initialBreak = initialBreak;
			this.eventListener = new EventListener(this);
		}

		private ProcessDebugger(IProcessDebuggerService service)
		{
			Contract.Requires(service != null);
			this.eventListener = new EventListener(this);
			this.processDebuggerService = service;
		}

		public static Task<ProcessDebugger> AttachAsync(int id, bool initialBreak = true)
		{
			var debuggerService = new Win32DebuggerService();
			var process = new ProcessDebugger(initialBreak);
			debuggerService.AttachToProcess(id, process.eventListener);
			return process.attachTaskCompletionSource.Task;
		}

		// Called back from worker thread
		public event EventHandler<ThreadDebugEventArgs> ThreadCreated;
		public event EventHandler<ThreadDebugEventArgs> ThreadExited;
		public event EventHandler<ModuleDebugEventArgs> ModuleLoaded;
		public event EventHandler<ModuleDebugEventArgs> ModuleUnloaded;
		public event EventHandler<ExceptionDebugEventArgs> ExceptionRaised;
		public event EventHandler<DebugStringOutputEventArgs> StringOutputted;

		private CREATE_PROCESS_DEBUG_INFO DebugInfo => processDebuggerService.ProcessDebugInfo;
		private IntPtr Handle => DebugInfo.hProcess;

		public string ImagePath
		{
			get
			{
				var hFile = DebugInfo.hFile;
				if (hFile != IntPtr.Zero) return GetFinalPathNameByHandle(hFile, 0);
				// Fallback to GetProcessImageFileName
				throw new NotImplementedException();
			}
		}
		
		public int ID => unchecked((int)GetProcessId(Handle));

		public ThreadDebugger[] GetThreads() => threadsByID.Values.ToArray();
		public ProcessModule[] GetModules() => modulesByBaseAddress.Values.ToArray();

		public ThreadDebugger FindThread(int id)
		{
			ThreadDebugger thread;
			threadsByID.TryGetValue(id, out thread);
			return thread;
		}

		public Task<ThreadDebugger> BreakAsync()
		{
			using (synchronizedBreakTaskCompletionSource.Enter())
			{
				if (synchronizedBreakTaskCompletionSource.Value == null)
				{
					synchronizedBreakTaskCompletionSource.Value = new TaskCompletionSource<ThreadDebugger>();
					processDebuggerService.RequestBreak();
				}

				return synchronizedBreakTaskCompletionSource.Value.Task;
			}
		}

		public void ReadMemory(ulong sourceAddress, UIntPtr buffer, UIntPtr length)
		{
			Contract.Requires((ulong)(UIntPtr)sourceAddress == sourceAddress);
			while ((ulong)length > 0)
			{
				UIntPtr readCount;
				CheckWin32(ReadProcessMemory(Handle, (IntPtr)sourceAddress, (IntPtr)(ulong)buffer, length, out readCount));
				sourceAddress += (ulong)readCount;
				buffer = (UIntPtr)((ulong)buffer + (ulong)readCount);
				length = (UIntPtr)((ulong)length - (ulong)readCount);
			}
		}

		public void WriteMemory(UIntPtr buffer, ulong destinationAddress, UIntPtr length)
		{
			Contract.Requires((ulong)(UIntPtr)destinationAddress == destinationAddress);
			while ((ulong)length > 0)
			{
				UIntPtr writtenCount;
				CheckWin32(WriteProcessMemory(Handle, (IntPtr)destinationAddress, (IntPtr)(ulong)buffer, length, out writtenCount));
				destinationAddress += (ulong)writtenCount;
				buffer = (UIntPtr)((ulong)buffer + (ulong)writtenCount);
				length = (UIntPtr)((ulong)length - (ulong)writtenCount);
			}
		}

		// May be called on either thread
		internal void Dispose()
		{
			processDebuggerService.Dispose();
		}

		// Called on worker thread
		private DebugEventResponse RaiseEvent<TArgs>(EventHandler<TArgs> handler, TArgs args)
			where TArgs : DebugEventArgs
		{
			if (handler == null)
			{
				return DebugEventResponse.ContinueUnhandled;
			}
			else
			{
				handler(this, args);
				return args.Response;
			}
		}

		// Called on worker thread
		private bool TryCompleteBreakTask(ThreadDebugger thread, Exception exception = null)
		{
			TaskCompletionSource<ThreadDebugger> breakTaskCompletionSource = null;
			using (synchronizedBreakTaskCompletionSource.Enter())
			{
				if (synchronizedBreakTaskCompletionSource.Value == null) return false;
				
				breakTaskCompletionSource = synchronizedBreakTaskCompletionSource.Value;
				synchronizedBreakTaskCompletionSource.Value = null;
			}
			
			if (exception == null) breakTaskCompletionSource.SetResult(thread);
			else breakTaskCompletionSource.SetException(exception);
			return true;
		}
	}
}
