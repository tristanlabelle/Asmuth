using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.Debugger
{
	partial class ProcessDebugger
	{
		internal sealed class EventListener : IDebugEventListener
		{
			private readonly ProcessDebugger owner;

			public EventListener(ProcessDebugger owner)
			{
				this.owner = owner;
			}
			
			public void OnAttachFailed(Exception exception)
			{
				owner.attachTaskCompletionSource.SetException(exception);
			}

			public DebugEventResponse OnAttachSucceeded(int threadID, IProcessDebuggerService service)
			{
				owner.attachTaskCompletionSource.SetResult(owner);
				return owner.initialBreak ? DebugEventResponse.Break : DebugEventResponse.ContinueUnhandled;
			}

			public DebugEventResponse OnChildProcessCreated(int threadID,
				IProcessDebuggerService child, out IDebugEventListener childListener)
			{
				var childProcess = new ProcessDebugger(child);
				childListener = childProcess.eventListener;
				throw new NotImplementedException();
			}

			public DebugEventResponse OnException(int threadID, ExceptionRecord record)
			{
				var thread = owner.FindThread(threadID);
				if (thread == null)
				{
					Contract.Assert(false, "Received exception from unknown thread.");
					return DebugEventResponse.ContinueUnhandled;
				}

				thread.OnBroken();

				if (record.Code == Kernel32.EXCEPTION_BREAKPOINT && owner.TryCompleteBreakTask(thread))
				{
					return DebugEventResponse.Break;
				}
				else
				{
					var eventArgs = new ExceptionDebugEventArgs(thread, record);
					var response = owner.RaiseEvent(owner.ExceptionRaised, eventArgs);
					if (response != DebugEventResponse.Break)
						thread.OnContinued();
					return response;
				}
			}
			
			public DebugEventResponse OnModuleLoaded(int threadID, ulong baseAddres, SafeFileHandle handle)
			{
				var module = new ProcessModule(baseAddres, handle);
				owner.modulesByBaseAddress.TryAdd(module.BaseAddress, module);
				return owner.RaiseEvent(owner.ModuleLoaded, new ModuleDebugEventArgs(module));
			}

			public DebugEventResponse OnModuleUnloaded(int threadID, ulong baseAddress)
			{
				ProcessModule module;
				if (owner.modulesByBaseAddress.TryRemove(baseAddress, out module))
				{
					return owner.RaiseEvent(owner.ModuleUnloaded, new ModuleDebugEventArgs(module));
				}
				else
				{
					Contract.Assert(false, "Mismatched unload dll message.");
					return DebugEventResponse.ContinueUnhandled;
				}
			}

			public DebugEventResponse OnProcessExited(int threadID, int exitCode)
			{
				throw new NotImplementedException();
			}

			public DebugEventResponse OnStringOutputted(int threadID, string str)
			{
				var handler = owner.StringOutputted;
				if (handler == null)
					return DebugEventResponse.ContinueUnhandled;
				else
					return owner.RaiseEvent(handler, new DebugStringOutputEventArgs(str));
			}

			public DebugEventResponse OnThreadCreated(int threadID, ulong entryPoint)
			{
				var thread = new ThreadDebugger(owner, threadID);
				bool added = owner.threadsByID.TryAdd(threadID, thread);
				Contract.Assert(added);
				return owner.RaiseEvent(owner.ThreadCreated, new ThreadCreatedDebugEventArgs(thread, entryPoint));
			}

			public DebugEventResponse OnThreadExited(int threadID, int exitCode)
			{
				ThreadDebugger thread;
				bool removed = owner.threadsByID.TryRemove(threadID, out thread);
				Contract.Assert(removed);
				return owner.RaiseEvent(owner.ThreadExited, new ThreadExitedDebugEventArgs(thread, exitCode));
			}
		}
	}
}
