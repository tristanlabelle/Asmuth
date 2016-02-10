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
		/// <summary>
		/// If set to <c>true</c>, the process' execution will remain paused after this event is handled.
		/// </summary>
		public bool Break { get; set; }
	}

	public sealed class DebugStringOutputEventArgs : DebugEventArgs
	{
		private readonly string str;

		public DebugStringOutputEventArgs(string str)
		{
			Contract.Requires(str != null);
			this.str = str;
		}

		public string String => str;
	}

	public sealed class ThreadDebugEventArgs : DebugEventArgs
	{
		private readonly ThreadDebugger thread;

		public ThreadDebugEventArgs(ThreadDebugger thread)
		{
			Contract.Requires(thread != null);
			this.thread = thread;
		}

		public ThreadDebugger Thread => thread;
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
