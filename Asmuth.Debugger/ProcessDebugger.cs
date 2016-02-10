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
		private readonly ConcurrentDictionary<ulong, ProcessModule> modulesByBaseAddress = new ConcurrentDictionary<ulong, ProcessModule>();
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
		public event EventHandler<ThreadDebugEventArgs> ThreadCreated;
		public event EventHandler<ThreadDebugEventArgs> ThreadExited;
		public event EventHandler<ModuleDebugEventArgs> ModuleLoaded;
		public event EventHandler<ModuleDebugEventArgs> ModuleUnloaded;
		public event EventHandler<ExceptionDebugEventArgs> ExceptionRaised;
		public event EventHandler<DebugStringOutputEventArgs> StringOutputted;

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
			CloseHandle(debugInfo.hFile);
			CloseHandle(debugInfo.hProcess);
			CloseHandle(debugInfo.hThread);
		}

		// Called on worker thread
		private void RaiseEvent<TArgs>(EventHandler<TArgs> handler, TArgs args, out bool @break)
			where TArgs : DebugEventArgs
		{
			if (handler == null)
			{
				@break = false;
			}
			else
			{
				handler(this, args);
				@break = args.Break;
			}
		}

		#region Callbacks from worker thread
		internal void OnThreadCreated(int id, CREATE_THREAD_DEBUG_INFO debugInfo, out bool @break)
		{
			var thread = new ThreadDebugger(this, debugInfo);
			threadsByID.TryAdd(id, thread);
			RaiseEvent(ThreadCreated, new ThreadDebugEventArgs(thread), out @break);
		}

		internal void OnThreadExited(uint id, uint exitCode, out bool @break)
		{
			var thread = FindThread(unchecked((int)id));
			thread.OnExited(exitCode);
			RaiseEvent(ThreadExited, new ThreadDebugEventArgs(thread), out @break);
		}
		
		internal void OnModuleLoaded(LOAD_DLL_DEBUG_INFO debugInfo, out bool @break)
		{
			var module = new ProcessModule(debugInfo);
			modulesByBaseAddress.TryAdd(module.BaseAddress, module);
			RaiseEvent(ModuleLoaded, new ModuleDebugEventArgs(module), out @break);
		}
		
		internal void OnModuleUnloaded(ulong baseAddress, out bool @break)
		{
			ProcessModule module;
			if (modulesByBaseAddress.TryRemove(baseAddress, out module))
			{
				RaiseEvent(ModuleUnloaded, new ModuleDebugEventArgs(module), out @break);
			}
			else
			{
				Contract.Assert(false, "Mismatched unload dll message.");
				@break = false;
			}
		}
		
		internal void OnException(uint id, ExceptionRecord record, out bool @break)
		{
			var thread = FindThread(unchecked((int)id));
			if (thread == null)
			{
				Contract.Assert(false, "Received exception from unknown thread.");
				@break = false;
				return;
			}

			thread.OnBroken();
			
			if (record.Code == EXCEPTION_BREAKPOINT && TryCompleteBreakTask(thread))
			{
				@break = true;
			}
			else
			{
				var eventArgs = new ExceptionDebugEventArgs(thread, record);
				RaiseEvent(ExceptionRaised, eventArgs, out @break);
			}

			if (@break) thread.OnContinued();
		}
		
		internal void OnOutputString(string str, out bool @break)
		{
			var handler = StringOutputted;
			if (handler == null)
			{
				@break = false;
			}
			else
			{
				RaiseEvent(handler, new DebugStringOutputEventArgs(str), out @break);
			}
		}
		#endregion

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
