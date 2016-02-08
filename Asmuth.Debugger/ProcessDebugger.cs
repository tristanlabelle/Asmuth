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
		public event EventHandler<ProcessDebuggerEventArgs<ExceptionRecord>> ExceptionRaised;
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
							TryCompleteBreakTask(null, GetLastWin32Exception());
					});
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
				buffer = (UIntPtr)((ulong)buffer + (ulong)writtenCount);
				length = (UIntPtr)((ulong)length - (ulong)writtenCount);
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
		internal void OnException(ThreadDebugger thread, ExceptionRecord record, out bool @break)
		{
			thread.OnBroken(record.Address);

			if (record.Code == EXCEPTION_BREAKPOINT && TryCompleteBreakTask(thread))
				@break = true;
			else
				RaiseEvent(ExceptionRaised, record, out @break);

			if (@break) thread.OnContinued();
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
