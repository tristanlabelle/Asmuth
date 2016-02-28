using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Asmuth.Debugger
{
	using static Advapi32;
	using static Kernel32;
	using static NativeMethods;

	internal sealed partial class Win32DebuggerService : IDebuggerService
	{
		public Win32DebuggerService()
		{
			AcquireDebuggingPrivilege();
		}

		public void AttachToProcess(int id, IDebugEventListener listener)
		{
			if (listener == null) throw new ArgumentNullException(nameof(listener));

			var workerThread = new Thread(param =>
			{
				var @this = (Win32DebuggerService)param;
				@this.WorkerThreadMain(unchecked((uint)id), listener);
			});

			workerThread.Name = "Debugger message pump thread";
			workerThread.Start(this);
		}

		public void Dispose() { }

		private static void AcquireDebuggingPrivilege()
		{
			IntPtr tokenHandle;
			CheckWin32(OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out tokenHandle));
			try
			{
				LUID valueLuid;
				CheckWin32(LookupPrivilegeValue(null, SE_DEBUG_NAME, out valueLuid));

				TOKEN_PRIVILEGES tokenPriviledges;
				tokenPriviledges.PrivilegeCount = 1;
				tokenPriviledges.Privilege.Luid = valueLuid;
				tokenPriviledges.Privilege.Attributes = SE_PRIVILEGE_ENABLED;
				CheckWin32(AdjustTokenPrivileges(tokenHandle, false, ref tokenPriviledges, 0));
			}
			finally
			{
				CloseHandle(tokenHandle);
			}
		}
	}
}
