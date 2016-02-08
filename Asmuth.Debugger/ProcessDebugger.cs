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

	public sealed class ProcessDebugger
	{
		private readonly Debugger debugger;
		private readonly CREATE_PROCESS_DEBUG_INFO debugInfo;
		private readonly ConcurrentDictionary<int, ThreadDebugger> threadsByID = new ConcurrentDictionary<int, ThreadDebugger>();
		private readonly BlockingCollection<ProcessModule> modules = new BlockingCollection<ProcessModule>();
		private readonly Synchronized<TaskCompletionSource<ThreadDebugger>> synchronizedBreakTaskCompletionSource
			= new Synchronized<TaskCompletionSource<ThreadDebugger>>();

		// Called on worker thread
		internal ProcessDebugger(Debugger debugger, CREATE_PROCESS_DEBUG_INFO debugInfo)
		{
			Contract.Requires(debugger != null);
			this.debugger = debugger;
			this.debugInfo = debugInfo;
		}

		// Called back from worker thread
		public event EventHandler<ProcessDebuggerEventArgs<ThreadDebugger>> ThreadCreated;
		public event EventHandler<ProcessDebuggerEventArgs<ProcessModule>> ModuleLoaded;
		public event EventHandler<ProcessDebuggerEventArgs<string>> StringOutputted;

		internal IntPtr Handle => debugInfo.hProcess;

		public string ImagePath
		{
			get
			{
				if (debugInfo.hFile != IntPtr.Zero) return GetFinalPathNameByHandle(debugInfo.hFile, 0);
				// Fallback to GetProcessImageFileName
				throw new NotImplementedException();
			}
		}
		
		public int ID => unchecked((int)GetProcessId(Handle));

		public ThreadDebugger[] GetThreads() => threadsByID.Values.ToArray();
		public ProcessModule[] GetModules() => modules.ToArray();

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
					debugger.EnqueueWorkerThreadRequest(() =>
					{
						if (!DebugBreakProcess(Handle))
							CompleteBreakTask(null, GetLastWin32Exception());
					});
				}

				return synchronizedBreakTaskCompletionSource.Value.Task;
			}
		}

		// May be called on either thread
		internal void Dispose()
		{
			CloseHandle(debugInfo.hFile);
			CloseHandle(debugInfo.hProcess);
			CloseHandle(debugInfo.hThread);
		}

		// Called on worker thread
		private void RaiseEvent<TData>(EventHandler<ProcessDebuggerEventArgs<TData>> handler, TData data, out bool @break)
		{
			if (handler == null)
			{
				@break = false;
			}
			else
			{
				var args = new ProcessDebuggerEventArgs<TData>(data);
				handler(this, args);
				@break = args.Break;
			}
		}

		// Called on worker thread
		internal ThreadDebugger OnThreadCreated(int id, CREATE_THREAD_DEBUG_INFO debugInfo, out bool @break)
		{
			var thread = new ThreadDebugger(this, debugInfo);
			threadsByID.TryAdd(id, thread);

			RaiseEvent(ThreadCreated, thread, out @break);
			return thread;
		}

		// Called on worker thread
		internal ProcessModule OnModuleLoaded(LOAD_DLL_DEBUG_INFO debugInfo, out bool @break)
		{
			var module = new ProcessModule(debugInfo);
			modules.Add(module);

			RaiseEvent(ModuleLoaded, module, out @break);
			return module;
		}

		// Called on worker thread
		internal void OnOutputString(OUTPUT_DEBUG_STRING_INFO debugString, out bool @break)
		{
			var handler = StringOutputted;
			if (handler == null)
			{
				@break = false;
			}
			else
			{
				var encoding = debugString.fUnicode == 0 ? Encoding.ASCII : Encoding.Unicode;
				var bytes = new byte[debugString.nDebugStringLength];
				Marshal.Copy(debugString.lpDebugStringData, bytes, 0, bytes.Length);
				var str = encoding.GetString(bytes);
				RaiseEvent(handler, str, out @break);
			}
		}

		// Called on worker thread
		internal void OnBrokenIntoThread(int id)
		{
			var thread = FindThread(id);
			Contract.Assert(thread != null);
			thread.OnBroken();

			CompleteBreakTask(thread);
		}

		private void CompleteBreakTask(ThreadDebugger thread, Exception exception = null)
		{
			TaskCompletionSource<ThreadDebugger> breakTaskCompletionSource = null;
			using (synchronizedBreakTaskCompletionSource.Enter())
			{
				if (synchronizedBreakTaskCompletionSource.Value != null)
				{
					breakTaskCompletionSource = synchronizedBreakTaskCompletionSource.Value;
					synchronizedBreakTaskCompletionSource.Value = null;
				}
			}

			if (breakTaskCompletionSource != null)
			{
				if (exception == null) breakTaskCompletionSource.SetResult(thread);
				else breakTaskCompletionSource.SetException(exception);
			}
		}
	}
}
