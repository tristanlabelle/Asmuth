using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.Debugger
{
	using BOOL = Boolean;
	using DWORD = UInt32;
	using DWORD64 = UInt64;
	using HANDLE = IntPtr;
	using HMODULE = IntPtr;
	using LONG = Int32;
	using LPCONTEXT = IntPtr;
	using LPCTSTR = String;
	using LPCVOID = IntPtr;
	using LPTSTR = StringBuilder;
	using LPVOID = IntPtr;
	using LPSTR = IntPtr;
	using LPTHREAD_START_ROUTINE = IntPtr;
	using PVOID = IntPtr;
	using SIZE_T = UIntPtr;
	using ULONG_PTR = UIntPtr;
	using WORD = UInt16;
	using System.Diagnostics.Contracts;
	internal static class Advapi32
	{
		private const string DllName = "advapi32.dll";

		public const DWORD SE_PRIVILEGE_ENABLED_BY_DEFAULT = 0x00000001;
		public const DWORD SE_PRIVILEGE_ENABLED = 0x00000002;
		public const DWORD SE_PRIVILEGE_REMOVED = 0x00000004;
		public const DWORD SE_PRIVILEGE_USED_FOR_ACCESS = 0x80000000;

		public const DWORD STANDARD_RIGHTS_REQUIRED = 0x000F0000;
		public const DWORD STANDARD_RIGHTS_READ = 0x00020000;
		public const DWORD TOKEN_ASSIGN_PRIMARY = 0x0001;
		public const DWORD TOKEN_DUPLICATE = 0x0002;
		public const DWORD TOKEN_IMPERSONATE = 0x0004;
		public const DWORD TOKEN_QUERY = 0x0008;
		public const DWORD TOKEN_QUERY_SOURCE = 0x0010;
		public const DWORD TOKEN_ADJUST_PRIVILEGES = 0x0020;
		public const DWORD TOKEN_ADJUST_GROUPS = 0x0040;
		public const DWORD TOKEN_ADJUST_DEFAULT = 0x0080;
		public const DWORD TOKEN_ADJUST_SESSIONID = 0x0100;
		public const DWORD TOKEN_READ = (STANDARD_RIGHTS_READ | TOKEN_QUERY);
		public const DWORD TOKEN_ALL_ACCESS = (STANDARD_RIGHTS_REQUIRED | TOKEN_ASSIGN_PRIMARY |
			TOKEN_DUPLICATE | TOKEN_IMPERSONATE | TOKEN_QUERY | TOKEN_QUERY_SOURCE |
			TOKEN_ADJUST_PRIVILEGES | TOKEN_ADJUST_GROUPS | TOKEN_ADJUST_DEFAULT |
			TOKEN_ADJUST_SESSIONID);

		public const string SE_DEBUG_NAME = "SeDebugPrivilege";

		public struct LUID
		{
			public DWORD LowPart;
			public LONG HighPart;
		}

		public struct TOKEN_PRIVILEGES
		{
			public DWORD PrivilegeCount;
			public LUID_AND_ATTRIBUTES Privilege;
		}

		public struct LUID_AND_ATTRIBUTES
		{
			public LUID Luid;
			public DWORD Attributes;
		}

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern BOOL AdjustTokenPrivileges(HANDLE TokenHandle,
			[MarshalAs(UnmanagedType.Bool)] BOOL DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, DWORD BufferLength,
			out TOKEN_PRIVILEGES PreviousState, out DWORD ReturnLength);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern BOOL AdjustTokenPrivileges(HANDLE TokenHandle,
			[MarshalAs(UnmanagedType.Bool)] BOOL DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, DWORD BufferLength,
			IntPtr PreviousState = default(IntPtr), IntPtr ReturnLength = default(IntPtr));

		[DllImport(DllName, SetLastError = true, CharSet = CharSet.Ansi)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern BOOL LookupPrivilegeValue(LPCTSTR lpSystemName, LPCTSTR lpName, out LUID lpLuid);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern BOOL OpenProcessToken(HANDLE ProcessHandle, DWORD DesiredAccess, [Out] out HANDLE TokenHandle);
	}
	
	internal static class Kernel32
	{
		private const string DllName = "kernel32.dll";

		public const DWORD EXCEPTION_DEBUG_EVENT = 1;
		public const DWORD CREATE_THREAD_DEBUG_EVENT = 2;
		public const DWORD CREATE_PROCESS_DEBUG_EVENT = 3;
		public const DWORD EXIT_THREAD_DEBUG_EVENT = 4;
		public const DWORD EXIT_PROCESS_DEBUG_EVENT = 5;
		public const DWORD LOAD_DLL_DEBUG_EVENT = 6;
		public const DWORD UNLOAD_DLL_DEBUG_EVENT = 7;
		public const DWORD OUTPUT_DEBUG_STRING_EVENT = 8;
		public const DWORD RIP_EVENT = 9;

		public const DWORD SLE_ERROR = 1;
		public const DWORD SLE_MINORERROR = 2;
		public const DWORD SLE_WARNING = 3;

		public const DWORD DBG_EXCEPTION_NOT_HANDLED = 0x80010001;
		public const DWORD DBG_CONTINUE = 0x00010002;

		public const DWORD INFINITE = 0xFFFFFFFF;

		public struct CREATE_PROCESS_DEBUG_INFO
		{
			public HANDLE hFile;
			public HANDLE hProcess;
			public HANDLE hThread;
			public LPVOID lpBaseOfImage;
			public DWORD dwDebugInfoFileOffset;
			public DWORD nDebugInfoSize;
			public LPVOID lpThreadLocalBase;
			public LPTHREAD_START_ROUTINE lpStartAddress;
			public LPVOID lpImageName;
			public WORD fUnicode;
		}

		public struct CREATE_THREAD_DEBUG_INFO
		{
			public HANDLE hThread;
			public LPVOID lpThreadLocalBase;
			public LPTHREAD_START_ROUTINE lpStartAddress;
		}

		public struct EXCEPTION_DEBUG_INFO32
		{
			public EXCEPTION_RECORD32 ExceptionRecord;
			public DWORD dwFirstChance;
		}

		public struct EXCEPTION_DEBUG_INFO64
		{
			public EXCEPTION_RECORD64 ExceptionRecord;
			public DWORD dwFirstChance;
		}

		public struct EXCEPTION_RECORD32
		{
			public DWORD ExceptionCode;
			public DWORD ExceptionFlags;
			public DWORD ExceptionRecord;
			public DWORD ExceptionAddress;
			public DWORD NumberParameters;
			public DWORD ExceptionInformation0, ExceptionInformation1, ExceptionInformation2, ExceptionInformation3,
				ExceptionInformation4, ExceptionInformation5, ExceptionInformation6, ExceptionInformation7,
				ExceptionInformation8, ExceptionInformation9, ExceptionInformation10, ExceptionInformation11,
				ExceptionInformation12, ExceptionInformation13, ExceptionInformation14, ExceptionInformation15;
		}

		public struct EXCEPTION_RECORD64
		{
			public DWORD ExceptionCode;
			public DWORD ExceptionFlags;
			public DWORD64 ExceptionRecord;
			public DWORD64 ExceptionAddress;
			public DWORD NumberParameters;
			public DWORD __unusedAlignment;
			public DWORD64 ExceptionInformation0, ExceptionInformation1, ExceptionInformation2, ExceptionInformation3,
				ExceptionInformation4, ExceptionInformation5, ExceptionInformation6, ExceptionInformation7,
				ExceptionInformation8, ExceptionInformation9, ExceptionInformation10, ExceptionInformation11,
				ExceptionInformation12, ExceptionInformation13, ExceptionInformation14, ExceptionInformation15;
		}

		public struct EXIT_PROCESS_DEBUG_INFO
		{
			public DWORD dwExitCode;
		}

		public struct EXIT_THREAD_DEBUG_INFO
		{
			public DWORD dwExitCode;
		}

		public struct LOAD_DLL_DEBUG_INFO
		{
			public HANDLE hFile;
			public LPVOID lpBaseOfDll;
			public DWORD dwDebugInfoFileOffset;
			public DWORD nDebugInfoSize;
			public LPVOID lpImageName;
			public WORD fUnicode;
		}

		public struct UNLOAD_DLL_DEBUG_INFO
		{
			public LPVOID lpBaseOfDll;
		}

		public struct OUTPUT_DEBUG_STRING_INFO
		{
			public LPSTR lpDebugStringData;
			public WORD fUnicode;
			public WORD nDebugStringLength;
		}

		public struct RIP_INFO
		{
			public DWORD dwError;
			public DWORD dwType;
		}

		[StructLayout(LayoutKind.Explicit)]
		public struct DEBUG_EVENT
		{
			[FieldOffset(0)]
			public DWORD dwDebugEventCode;
			[FieldOffset(4)]
			public DWORD dwProcessId;
			[FieldOffset(8)]
			public DWORD dwThreadId;
			[FieldOffset(12)]
			public EXCEPTION_DEBUG_INFO32 Exception32;
			[FieldOffset(12)]
			public EXCEPTION_DEBUG_INFO64 Exception64;
			[FieldOffset(12)]
			public CREATE_THREAD_DEBUG_INFO CreateThread;
			[FieldOffset(12)]
			public CREATE_PROCESS_DEBUG_INFO CreateProcessInfo;
			[FieldOffset(12)]
			public EXIT_THREAD_DEBUG_INFO ExitThread;
			[FieldOffset(12)]
			public EXIT_PROCESS_DEBUG_INFO ExitProcess;
			[FieldOffset(12)]
			public LOAD_DLL_DEBUG_INFO LoadDll;
			[FieldOffset(12)]
			public UNLOAD_DLL_DEBUG_INFO UnloadDll;
			[FieldOffset(12)]
			public OUTPUT_DEBUG_STRING_INFO DebugString;
			[FieldOffset(12)]
			public RIP_INFO RipInfo;
		}

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern BOOL CheckRemoteDebuggerPresent(HANDLE hProcess, [MarshalAs(UnmanagedType.Bool)] out BOOL pbDebuggerPresent);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern BOOL CloseHandle(HANDLE hObject);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern BOOL DebugActiveProcess(DWORD processID);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern BOOL DebugActiveProcessStop(DWORD processID);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern BOOL DebugBreakProcess(HANDLE Process);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern BOOL ContinueDebugEvent(DWORD dwProcessId, DWORD dwThreadId, DWORD dwContinueStatus);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern BOOL FlushInstructionCache(HANDLE hProcess, LPCVOID lpBaseAddress, SIZE_T dwSize);
		
		[DllImport(DllName, SetLastError = true)]
		public static extern HANDLE GetCurrentProcess();

		[DllImport(DllName, SetLastError = true, CharSet = CharSet.Unicode)]
		public static extern DWORD GetFinalPathNameByHandle(HANDLE hFile, LPTSTR lpszFilePath, DWORD cchFilePath, DWORD dwFlags);

		[DllImport(DllName, SetLastError = true, CharSet = CharSet.Unicode)]
		public static extern DWORD GetModuleFileNameEx(HANDLE hProcess, HMODULE hModule, LPTSTR lpFilename, DWORD nSize);

		[DllImport(DllName, SetLastError = true)]
		public static extern DWORD GetProcessId(HANDLE Process);

		[DllImport(DllName, SetLastError = true)]
		public static extern DWORD GetProcessImageFileName(HANDLE hProcess, LPTSTR lpImageFileName, DWORD nSize);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern BOOL GetThreadContext(HANDLE hThread, LPCONTEXT lpContext);

		[DllImport(DllName, SetLastError = true)]
		public static extern DWORD GetThreadId(HANDLE Thread);

		[DllImport(DllName, SetLastError = true)]
		public static extern HANDLE OpenProcess(DWORD dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] BOOL bInheritHandle, DWORD dwProcessId);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern BOOL ReadProcessMemory(HANDLE hProcess, LPCVOID lpBaseAddress, LPVOID lpBuffer, SIZE_T nSize, out SIZE_T lpNumberOfBytesRead);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern BOOL SetThreadContext(HANDLE hThread, LPCONTEXT lpContext);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern BOOL WaitForDebugEventEx(out DEBUG_EVENT @event, DWORD milliseconds);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern BOOL WriteProcessMemory(HANDLE hProcess, LPVOID lpBaseAddress, LPVOID lpBuffer, SIZE_T nSize, out SIZE_T lpNumberOfBytesWritten);
		
		public static string GetFinalPathNameByHandle(IntPtr handle, DWORD dwFlags)
		{
			var pathLength = GetFinalPathNameByHandle(handle, null, 0, dwFlags);
			NativeMethods.CheckWin32(pathLength > 0);
			var pathBuilder = new StringBuilder((int)pathLength);
			pathBuilder.Length = (int)pathLength;
			NativeMethods.CheckWin32(GetFinalPathNameByHandle(handle, pathBuilder, pathLength, dwFlags) > 0);
			return pathBuilder.ToString();
		}
	}

	public static class NativeMethods
	{
		public static void CheckWin32(bool result)
		{
			if (result) return;
			throw GetLastWin32Exception();
		}

		public static Exception GetLastWin32Exception()
			=> GetWin32Exception(Marshal.GetLastWin32Error());

		public static Exception GetWin32Exception(int errorCode)
		{
			Contract.Requires(errorCode >= 0 && errorCode < 0x10000);
			if (errorCode == 0) return null;

			var hr = 0x80070000 | (uint)errorCode;
			return Marshal.GetExceptionForHR(unchecked((int)hr));
		}
	}
}
