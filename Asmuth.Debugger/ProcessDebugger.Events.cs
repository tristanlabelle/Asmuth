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
				if (str == null) throw new ArgumentNullException(nameof(str));
				this.String = str;
			}
		}

		public sealed class ThreadCreatedEventArgs : DebugEventArgs
		{
			public Thread Thread { get; }
			public ForeignPtr EntryPoint { get; }

			public ThreadCreatedEventArgs(Thread thread, ForeignPtr entryPoint)
			{
				if (thread == null) throw new ArgumentNullException(nameof(thread));
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
				if (thread == null) throw new ArgumentNullException(nameof(thread));
				this.Thread = thread;
				this.ExitCode = exitCode;
			}
		}

		public sealed class ModuleEventArgs : DebugEventArgs
		{
			public Module Module { get; }

			public ModuleEventArgs(Module module)
			{
				if (module == null) throw new ArgumentNullException(nameof(module));
				this.Module = module;
			}
		}

		public sealed class ExceptionEventArgs : DebugEventArgs
		{
			public Thread Thread { get; }
			public ExceptionRecord Record { get; }

			public ExceptionEventArgs(Thread thread, ExceptionRecord record)
			{
				if (thread == null) throw new ArgumentNullException(nameof(thread));
				if (record == null) throw new ArgumentNullException(nameof(record));
				this.Thread = thread;
				this.Record = record;
			}
		}
	}
}
