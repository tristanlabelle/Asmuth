using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.Debugger
{
	partial class ProcessDebugger
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

		public sealed class ThreadCreatedEventArgs : DebugEventArgs
		{
			public Thread Thread { get; }
			public ForeignPtr EntryPoint { get; }

			public ThreadCreatedEventArgs(Thread thread, ForeignPtr entryPoint)
			{
				Contract.Requires(thread != null);
				this.Thread = thread;
				this.EntryPoint = entryPoint;
			}
		}

		public sealed class ThreadExitedEventArgs : DebugEventArgs
		{
			public Thread Thread { get; }
			public int ExitCode { get; }

			public ThreadExitedEventArgs(Thread thread, int exitCode)
			{
				Contract.Requires(thread != null);
				this.Thread = thread;
				this.ExitCode = exitCode;
			}
		}

		public sealed class ModuleEventArgs : DebugEventArgs
		{
			public Module Module { get; }

			public ModuleEventArgs(Module module)
			{
				Contract.Requires(module != null);
				this.Module = module;
			}
		}

		public sealed class ExceptionEventArgs : DebugEventArgs
		{
			public Thread Thread { get; }
			public ExceptionRecord Record { get; }

			public ExceptionEventArgs(Thread thread, ExceptionRecord record)
			{
				Contract.Requires(thread != null);
				Contract.Requires(record != null);
				this.Thread = thread;
				this.Record = record;
			}
		}
	}
}
