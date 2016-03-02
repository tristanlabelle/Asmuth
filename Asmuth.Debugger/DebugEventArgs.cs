using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.Debugger
{
	public class DebugEventArgs : EventArgs
	{
		public DebugEventResponse Response { get; set; }
	}

	public sealed class DebugStringOutputEventArgs : DebugEventArgs
	{
		public string String { get; }

		public DebugStringOutputEventArgs(string str)
		{
			Contract.Requires(str != null);
			this.String = str;
		}
	}

	public sealed class ThreadCreatedDebugEventArgs : DebugEventArgs
	{
		public ThreadDebugger Thread { get; }
		public ulong EntryPoint { get; }

		public ThreadCreatedDebugEventArgs(ThreadDebugger thread, ulong entryPoint)
		{
			Contract.Requires(thread != null);
			this.Thread = thread;
			this.EntryPoint = entryPoint;
		}
	}

	public sealed class ThreadExitedDebugEventArgs : DebugEventArgs
	{
		public ThreadDebugger Thread { get; }
		public int ExitCode { get; }

		public ThreadExitedDebugEventArgs(ThreadDebugger thread, int exitCode)
		{
			Contract.Requires(thread != null);
			this.Thread = thread;
			this.ExitCode = exitCode;
		}
	}

	public sealed class ModuleDebugEventArgs : DebugEventArgs
	{
		private readonly ProcessModule module;

		public ModuleDebugEventArgs(ProcessModule module)
		{
			Contract.Requires(module != null);
			this.module = module;
		}

		public ProcessModule Module => module;
	}

	public sealed class ExceptionDebugEventArgs : DebugEventArgs
	{
		private readonly ThreadDebugger thread;
		private readonly ExceptionRecord record;

		public ExceptionDebugEventArgs(ThreadDebugger thread, ExceptionRecord record)
		{
			Contract.Requires(thread != null);
			Contract.Requires(record != null);
			this.thread = thread;
			this.record = record;
		}

		public ThreadDebugger Thread => thread;
		public ExceptionRecord Record => record;
	}
}
