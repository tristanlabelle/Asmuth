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
	using static Kernel32;

	public sealed partial class ProcessDebugger
	{
		private readonly bool initialBreak;
		private readonly EventListener eventListener;
		private readonly TaskCompletionSource<ProcessDebugger> attachTaskCompletionSource
			= new TaskCompletionSource<ProcessDebugger>();
		private IProcessDebuggerService service;

		private readonly ConcurrentDictionary<int, Thread> threadsByID = new ConcurrentDictionary<int, Thread>();
		private readonly ConcurrentDictionary<ForeignPtr, Module> modulesByBaseAddress
			= new ConcurrentDictionary<ForeignPtr, Module>();
		private readonly Synchronized<TaskCompletionSource<Thread>> synchronizedBreakTaskCompletionSource
			= new Synchronized<TaskCompletionSource<Thread>>();

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
			this.service = service;
		}

		public static Task<ProcessDebugger> AttachAsync(int id, bool initialBreak = true)
		{
			var debuggerService = new Win32DebuggerService();
			var process = new ProcessDebugger(initialBreak);
			debuggerService.AttachToProcess(id, process.eventListener);
			return process.attachTaskCompletionSource.Task;
		}

		// Called back from worker thread
		public event EventHandler<ThreadCreatedEventArgs> ThreadCreated;
		public event EventHandler<ThreadExitedEventArgs> ThreadExited;
		public event EventHandler<ModuleEventArgs> ModuleLoaded;
		public event EventHandler<ModuleEventArgs> ModuleUnloaded;
		public event EventHandler<ExceptionEventArgs> ExceptionRaised;
		public event EventHandler<DebugStringOutputEventArgs> StringOutputted;

		public int ID => service.ID;
		private SafeHandle ImageHandle => service.ImageHandle;
		public ForeignPtr ImageBase => service.ImageBase;
		public string ImagePath => GetFinalPathNameByHandle(service.ImageHandle.DangerousGetHandle(), 0);

		public Thread[] GetThreads() => threadsByID.Values.ToArray();
		public Module[] GetModules() => modulesByBaseAddress.Values.ToArray();

		public Thread FindThread(int id)
		{
			Thread thread;
			threadsByID.TryGetValue(id, out thread);
			return thread;
		}

		public MemoryStream OpenMemory() => new MemoryStream(this);
		public MemoryStream OpenMemory(ForeignPtr ptr) => new MemoryStream(this) { Ptr = ptr };

		public Task<Thread> BreakAsync()
		{
			using (synchronizedBreakTaskCompletionSource.Enter())
			{
				if (synchronizedBreakTaskCompletionSource.Value == null)
				{
					synchronizedBreakTaskCompletionSource.Value = new TaskCompletionSource<Thread>();
					service.RequestBreak();
				}

				return synchronizedBreakTaskCompletionSource.Value.Task;
			}
		}

		// May be called on either thread
		internal void Dispose()
		{
			service.Dispose();
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
		private bool TryCompleteBreakTask(Thread thread, Exception exception = null)
		{
			TaskCompletionSource<Thread> breakTaskCompletionSource = null;
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
