using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.Debugger
{
	partial class Debugger
	{
		private sealed class ProcessAttachment
		{
			private readonly TaskCompletionSource<ProcessDebugger> taskCompletionSource
				= new TaskCompletionSource<ProcessDebugger>();
			private readonly bool initialBreak;

			public ProcessAttachment(bool initialBreak)
			{
				this.initialBreak = initialBreak;
			}

			public bool InitialBreak => initialBreak;

			public Task<ProcessDebugger> Task => taskCompletionSource.Task;

			public ProcessDebugger Process
			{
				get
				{
					Contract.Assert(taskCompletionSource.Task.IsCompleted);
					return taskCompletionSource.Task.Result;
				}
			}

			public void Complete(ProcessDebugger process) => taskCompletionSource.SetResult(process);
			public void Fail(Exception exception) => taskCompletionSource.SetException(exception);
			public void Dispose()
			{
				if (Task.IsCompleted) Task.Result.Dispose();
				else taskCompletionSource.SetCanceled();
			}
		}
	}
}
