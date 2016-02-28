using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Diagnostics.Contracts;

namespace Asmuth.Debugger
{
	using static Kernel32;

	class Program
	{
		static void Main(string[] args)
		{
			Run().Wait();
		}

		private static async Task Run()
		{
			var notepadProcess = Process.Start(@"C:\Windows\SysWow64\notepad.exe");
			var notepadDebugger = await ProcessDebugger.AttachAsync(notepadProcess.Id, initialBreak: false);
			var brokenThread = await notepadDebugger.BreakAsync();

			var context = brokenThread.GetContext(CONTEXT_FULL);
			Contract.Assert(!brokenThread.IsRunning);
		}
	}
}
