using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.Debugger
{
	using System.Diagnostics.Contracts;
	using static Kernel32;

	public sealed class ProcessModule
	{
		private readonly LOAD_DLL_DEBUG_INFO debugInfo;

		internal ProcessModule(LOAD_DLL_DEBUG_INFO debugInfo)
		{
			Contract.Assert(debugInfo.hFile != IntPtr.Zero);
			this.debugInfo = debugInfo;
		}

		public string FilePath => GetFinalPathNameByHandle(debugInfo.hFile, 0);
		public ulong BaseAddress => unchecked((ulong)debugInfo.lpBaseOfDll);

		internal void Dispose()
		{
			CloseHandle(debugInfo.hFile);
		}
	}
}
