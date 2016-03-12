using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace Asmuth.Debugger
{
	using BOOL = Boolean;
	using BYTE = Byte;
	using DWORD = UInt32;
	using DWORD64 = UInt64;
	using HANDLE = IntPtr;
	using HMODULE = IntPtr;
	using LONG = Int32;
	using LONGLONG = Int64;
	using LPCONTEXT = IntPtr;
	using LPCTSTR = String;
	using LPCVOID = IntPtr;
	using LPTSTR = StringBuilder;
	using LPVOID = IntPtr;
	using LPSTR = IntPtr;
	using LPTHREAD_START_ROUTINE = IntPtr;
	using PVOID = IntPtr;
	using SIZE_T = UIntPtr;
	using UINT = UInt32;
	using ULONG_PTR = UIntPtr;
	using ULONGLONG = UInt64;
	using WORD = UInt16;
	using Ptr32 = System.UInt32;

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

		public const DWORD CONTEXT_X86 = 0x00010000; // this assumes that i386 and
		public const DWORD CONTEXT_i386 = CONTEXT_X86; // this assumes that i386 and
		public const DWORD CONTEXT_i486 = CONTEXT_X86; // i486 have identical context records

		public const DWORD MAXIMUM_SUPPORTED_EXTENSION = 512;

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

		public const DWORD EXCEPTION_NONCONTINUABLE = 0x1;
		public const DWORD STATUS_GUARD_PAGE_VIOLATION = 0x80000001;
		public const DWORD STATUS_DATATYPE_MISALIGNMENT = 0x80000002;
		public const DWORD STATUS_BREAKPOINT = 0x80000003;
		public const DWORD STATUS_SINGLE_STEP = 0x80000004;
		public const DWORD STATUS_ACCESS_VIOLATION = 0xC0000005;
		public const DWORD STATUS_IN_PAGE_ERROR = 0xC0000006;
		public const DWORD STATUS_INVALID_HANDLE = 0xC0000008;
		public const DWORD STATUS_NO_MEMORY = 0xC0000017;
		public const DWORD STATUS_ILLEGAL_INSTRUCTION = 0xC000001D;
		public const DWORD STATUS_NONCONTINUABLE_EXCEPTION = 0xC0000025;
		public const DWORD STATUS_INVALID_DISPOSITION = 0xC0000026;
		public const DWORD STATUS_ARRAY_BOUNDS_EXCEEDED = 0xC000008C;
		public const DWORD STATUS_FLOAT_DENORMAL_OPERAND = 0xC000008D;
		public const DWORD STATUS_FLOAT_DIVIDE_BY_ZERO = 0xC000008E;
		public const DWORD STATUS_FLOAT_INEXACT_RESULT = 0xC000008F;
		public const DWORD STATUS_FLOAT_INVALID_OPERATION = 0xC0000090;
		public const DWORD STATUS_FLOAT_OVERFLOW = 0xC0000091;
		public const DWORD STATUS_FLOAT_STACK_CHECK = 0xC0000092;
		public const DWORD STATUS_FLOAT_UNDERFLOW = 0xC0000093;
		public const DWORD STATUS_INTEGER_DIVIDE_BY_ZERO = 0xC0000094;
		public const DWORD STATUS_INTEGER_OVERFLOW = 0xC0000095;
		public const DWORD STATUS_PRIVILEGED_INSTRUCTION = 0xC0000096;
		public const DWORD STATUS_STACK_OVERFLOW = 0xC00000FD;
		public const DWORD STATUS_CONTROL_C_EXIT = 0xC000013A;

		// from winbase.h
		public const DWORD EXCEPTION_ACCESS_VIOLATION = STATUS_ACCESS_VIOLATION;
		public const DWORD EXCEPTION_DATATYPE_MISALIGNMENT = STATUS_DATATYPE_MISALIGNMENT;
		public const DWORD EXCEPTION_BREAKPOINT = STATUS_BREAKPOINT;
		public const DWORD EXCEPTION_SINGLE_STEP = STATUS_SINGLE_STEP;
		public const DWORD EXCEPTION_ARRAY_BOUNDS_EXCEEDED = STATUS_ARRAY_BOUNDS_EXCEEDED;
		public const DWORD EXCEPTION_FLT_DENORMAL_OPERAND = STATUS_FLOAT_DENORMAL_OPERAND;
		public const DWORD EXCEPTION_FLT_DIVIDE_BY_ZERO = STATUS_FLOAT_DIVIDE_BY_ZERO;
		public const DWORD EXCEPTION_FLT_INEXACT_RESULT = STATUS_FLOAT_INEXACT_RESULT;
		public const DWORD EXCEPTION_FLT_INVALID_OPERATION = STATUS_FLOAT_INVALID_OPERATION;
		public const DWORD EXCEPTION_FLT_OVERFLOW = STATUS_FLOAT_OVERFLOW;
		public const DWORD EXCEPTION_FLT_STACK_CHECK = STATUS_FLOAT_STACK_CHECK;
		public const DWORD EXCEPTION_FLT_UNDERFLOW = STATUS_FLOAT_UNDERFLOW;
		public const DWORD EXCEPTION_INT_DIVIDE_BY_ZERO = STATUS_INTEGER_DIVIDE_BY_ZERO;
		public const DWORD EXCEPTION_INT_OVERFLOW = STATUS_INTEGER_OVERFLOW;
		public const DWORD EXCEPTION_PRIV_INSTRUCTION = STATUS_PRIVILEGED_INSTRUCTION;
		public const DWORD EXCEPTION_IN_PAGE_ERROR = STATUS_IN_PAGE_ERROR;
		public const DWORD EXCEPTION_ILLEGAL_INSTRUCTION = STATUS_ILLEGAL_INSTRUCTION;
		public const DWORD EXCEPTION_NONCONTINUABLE_EXCEPTION = STATUS_NONCONTINUABLE_EXCEPTION;
		public const DWORD EXCEPTION_STACK_OVERFLOW = STATUS_STACK_OVERFLOW;
		public const DWORD EXCEPTION_INVALID_DISPOSITION = STATUS_INVALID_DISPOSITION;
		public const DWORD EXCEPTION_GUARD_PAGE = STATUS_GUARD_PAGE_VIOLATION;
		public const DWORD EXCEPTION_INVALID_HANDLE = STATUS_INVALID_HANDLE;

		public const DWORD INFINITE = 0xFFFFFFFF;
		
		public static class X86
		{
			public const DWORD CONTEXT_CONTROL = CONTEXT_i386 | 0x00000001; // SS:SP, CS:IP, FLAGS, BP
			public const DWORD CONTEXT_INTEGER = CONTEXT_i386 | 0x00000002; // AX, BX, CX, DX, SI, DI
			public const DWORD CONTEXT_SEGMENTS = CONTEXT_i386 | 0x00000004; // DS, ES, FS, GS
			public const DWORD CONTEXT_FLOATING_POINT = CONTEXT_i386 | 0x00000008; // 387 state
			public const DWORD CONTEXT_DEBUG_REGISTERS = CONTEXT_i386 | 0x00000010; // DB 0-3,6,7
			public const DWORD CONTEXT_EXTENDED_REGISTERS = CONTEXT_i386 | 0x00000020; // cpu specific extensions

			public const DWORD CONTEXT_FULL = CONTEXT_CONTROL | CONTEXT_INTEGER | CONTEXT_SEGMENTS;
			public const DWORD CONTEXT_ALL = CONTEXT_CONTROL | CONTEXT_INTEGER | CONTEXT_SEGMENTS
				| CONTEXT_FLOATING_POINT | CONTEXT_DEBUG_REGISTERS | CONTEXT_EXTENDED_REGISTERS;

			public const DWORD CONTEXT_XSTATE = CONTEXT_i386 | 0x00000040;

			public const DWORD SIZE_OF_80387_REGISTERS = 80;

			[StructLayout(LayoutKind.Sequential)]
			public struct CONTEXT
			{
				[StructLayout(LayoutKind.Sequential, Size = (int)MAXIMUM_SUPPORTED_EXTENSION)]
				public struct EXTENDED_REGISTERS { }

				public DWORD ContextFlags;
				public DWORD Dr0, Dr1, Dr2, Dr3, Dr6, Dr7; // if CONTEXT_DEBUG_REGISTERS
				public FLOATING_SAVE_AREA FloatSave; // if CONTEXT_FLOATING_POINT
				public DWORD SegGs, SegFs, SegEs, SegDs; // if CONTEXT_SEGMENTS
				public DWORD Edi, Esi, Ebx, Edx, Ecx, Eax; // if CONTEXT_INTEGER
				public DWORD Ebp, Eip, SegCs, EFlags, Esp, SegSs; // if CONTEXT_CONTROL
				public EXTENDED_REGISTERS ExtendedRegisters; // if CONTEXT_EXTENDED_REGISTERS
			}

			[StructLayout(LayoutKind.Sequential)]
			public struct FLOATING_SAVE_AREA
			{
				[StructLayout(LayoutKind.Sequential, Size = (int)SIZE_OF_80387_REGISTERS)]
				public struct REGISTER_AREA { }

				DWORD ControlWord;
				DWORD StatusWord;
				DWORD TagWord;
				DWORD ErrorOffset;
				DWORD ErrorSelector;
				DWORD DataOffset;
				DWORD DataSelector;
				REGISTER_AREA RegisterArea;
				DWORD Cr0NpxState;
			}

			[StructLayout(LayoutKind.Sequential)]
			public struct CONTEXT_EX
			{
				// The total length of the structure starting from the chunk with
				// the smallest offset. N.B. that the offset may be negative.
				public CONTEXT_CHUNK All;

				// Wrapper for the traditional CONTEXT structure. N.B. the size of
				// the chunk may be less than sizeof(CONTEXT) is some cases (when
				// CONTEXT_EXTENDED_REGISTERS is not set on x86 for instance).
				public CONTEXT_CHUNK Legacy;

				// CONTEXT_XSTATE: Extended processor state chunk. The state is
				// stored in the same format XSAVE operation strores it with
				// exception of the first 512 bytes, i.e. staring from
				// XSAVE_AREA_HEADER. The lower two bits corresponding FP and
				// SSE state must be zero.
				public CONTEXT_CHUNK XState;
			}

			[StructLayout(LayoutKind.Sequential)]
			public struct CONTEXT_CHUNK
			{
				public LONG Offset;
				public DWORD Length;
			}

			[StructLayout(LayoutKind.Sequential)]
			public struct XSTATE_CONTEXT
			{
				public DWORD64 Mask;
				public DWORD Length;
				public DWORD Reserved1;
				public Ptr32 Area; // Pointer to XSAVE_AREA
				public DWORD Reserved2;
				public Ptr32 Buffer;
				public DWORD Reserved3;
			}

			[StructLayout(LayoutKind.Sequential)]
			public struct M128A
			{
				public ULONGLONG Low;
				public LONGLONG High;
			}

			[StructLayout(LayoutKind.Sequential)]
			public struct XSAVE_AREA
			{
				public XSAVE_FORMAT LegacyState;
				public XSAVE_AREA_HEADER Header;
			}

			[StructLayout(LayoutKind.Sequential)]
			public struct XSAVE_FORMAT
			{
				[StructLayout(LayoutKind.Sequential)]
				public struct FLOAT_REGISTERS
				{
					public M128A _0, _1, _2, _3, _4, _5, _6, _7;
				}

				[StructLayout(LayoutKind.Sequential, Size = 192)]
				public struct RESERVED4 { }

				[StructLayout(LayoutKind.Sequential, Size = sizeof(DWORD) * 7)]
				public struct STACK_CONTROL { }

				public WORD ControlWord;
				public WORD StatusWord;
				public BYTE TagWord;
				public BYTE Reserved1;
				public WORD ErrorOpcode;
				public DWORD ErrorOffset;
				public WORD ErrorSelector;
				public WORD Reserved2;
				public DWORD DataOffset;
				public WORD DataSelector;
				public WORD Reserved3;
				public DWORD MxCsr;
				public DWORD MxCsr_Mask;
				public FLOAT_REGISTERS FloatRegisters;

				public FLOAT_REGISTERS XmmRegisters;
				public RESERVED4 Reserved4;

				//
				// The fields below are not part of XSAVE/XRSTOR format.
				// They are written by the OS which is relying on a fact that
				// neither (FX)SAVE nor (F)XSTOR used this area.
				//

				public STACK_CONTROL StackControl; // KERNEL_STACK_CONTROL structure actualy
				public DWORD Cr0NpxState;
			}

			[StructLayout(LayoutKind.Sequential)]
			public struct XSAVE_AREA_HEADER
			{
				[StructLayout(LayoutKind.Sequential)]
				public struct RESERVED
				{
					public DWORD64 _0, _1, _2, _3, _4, _5, _6;
				}

				public DWORD64 Mask;
				public RESERVED Reserved;
			}
		}

		public static class WOW64
		{
			public const DWORD CONTEXT_i386 = 0x00010000;
			public const DWORD CONTEXT_i486 = 0x00010000;

			public const DWORD CONTEXT_CONTROL = CONTEXT_i386 | 0x00000001; // SS:SP, CS:IP, FLAGS, BP
			public const DWORD CONTEXT_INTEGER = CONTEXT_i386 | 0x00000002; // AX, BX, CX, DX, SI, DI
			public const DWORD CONTEXT_SEGMENTS = CONTEXT_i386 | 0x00000004; // DS, ES, FS, GS
			public const DWORD CONTEXT_FLOATING_POINT = CONTEXT_i386 | 0x00000008; // 387 state
			public const DWORD CONTEXT_DEBUG_REGISTERS = CONTEXT_i386 | 0x00000010; // DB 0-3,6,7
			public const DWORD CONTEXT_EXTENDED_REGISTERS = CONTEXT_i386 | 0x00000020; // cpu specific extensions

			public const DWORD CONTEXT_FULL = CONTEXT_CONTROL | CONTEXT_INTEGER | CONTEXT_SEGMENTS;

			public const DWORD CONTEXT_ALL = CONTEXT_CONTROL | CONTEXT_INTEGER | CONTEXT_SEGMENTS
				| CONTEXT_FLOATING_POINT | CONTEXT_DEBUG_REGISTERS | CONTEXT_EXTENDED_REGISTERS;

			public const DWORD CONTEXT_XSTATE = CONTEXT_i386 | 0x00000040;

			public const DWORD SIZE_OF_80387_REGISTERS = 80;

			public const DWORD MAXIMUM_SUPPORTED_EXTENSION = 512;

			[StructLayout(LayoutKind.Sequential)]
			public struct CONTEXT
			{
				[StructLayout(LayoutKind.Sequential, Size = (int)MAXIMUM_SUPPORTED_EXTENSION)]
				public struct EXTENDED_REGISTERS { }

				// The flags values within this flag control the contents of
				// a CONTEXT record.
				//
				// If the context record is used as an input parameter, then
				// for each portion of the context record controlled by a flag
				// whose value is set, it is assumed that that portion of the
				// context record contains valid context. If the context record
				// is being used to modify a threads context, then only that
				// portion of the threads context will be modified.
				//
				// If the context record is used as an IN OUT parameter to capture
				// the context of a thread, then only those portions of the thread's
				// context corresponding to set flags will be returned.
				//
				// The context record is never used as an OUT only parameter.
				public DWORD ContextFlags;
				
				// This section is specified/returned if CONTEXT_DEBUG_REGISTERS is
				// set in ContextFlags.  Note that CONTEXT_DEBUG_REGISTERS is NOT
				// included in CONTEXT_FULL.
				public DWORD Dr0;
				public DWORD Dr1;
				public DWORD Dr2;
				public DWORD Dr3;
				public DWORD Dr6;
				public DWORD Dr7;

				// This section is specified/returned if the
				// ContextFlags word contians the flag CONTEXT_FLOATING_POINT.
				public FLOATING_SAVE_AREA FloatSave;
				
				// This section is specified/returned if the
				// ContextFlags word contians the flag CONTEXT_SEGMENTS.
				public DWORD SegGs;
				public DWORD SegFs;
				public DWORD SegEs;
				public DWORD SegDs;
				
				// This section is specified/returned if the
				// ContextFlags word contians the flag CONTEXT_INTEGER.
				public DWORD Edi;
				public DWORD Esi;
				public DWORD Ebx;
				public DWORD Edx;
				public DWORD Ecx;
				public DWORD Eax;
				
				// This section is specified/returned if the
				// ContextFlags word contains the flag CONTEXT_CONTROL.
				public DWORD Ebp;
				public DWORD Eip;
				public DWORD SegCs; // MUST BE SANITIZED
				public DWORD EFlags; // MUST BE SANITIZED
				public DWORD Esp;
				public DWORD SegSs;

				// This section is specified/returned if the ContextFlags word
				// contains the flag CONTEXT_EXTENDED_REGISTERS.
				// The format and contexts are processor specific
				public EXTENDED_REGISTERS ExtendedRegisters;
			}

			[StructLayout(LayoutKind.Sequential)]
			public struct FLOATING_SAVE_AREA
			{
				[StructLayout(LayoutKind.Sequential, Size = (int)SIZE_OF_80387_REGISTERS)]
				public struct REGISTER_AREA { }

				public DWORD ControlWord;
				public DWORD StatusWord;
				public DWORD TagWord;
				public DWORD ErrorOffset;
				public DWORD ErrorSelector;
				public DWORD DataOffset;
				public DWORD DataSelector;
				public REGISTER_AREA RegisterArea;
				public DWORD Cr0NpxState;
			}
		}

		public static class AMD64
		{
			[StructLayout(LayoutKind.Sequential)]
			public struct CONTEXT
			{
				// Register parameter home addresses.
				//
				// N.B. These fields are for convience - they could be used to extend the
				//      context record in the future.
				public DWORD64 P1Home;
				public DWORD64 P2Home;
				public DWORD64 P3Home;
				public DWORD64 P4Home;
				public DWORD64 P5Home;
				public DWORD64 P6Home;

				// Control flags.
				public DWORD ContextFlags;
				public DWORD MxCsr;

				// Segment Registers and processor flags.
				public WORD SegCs;
				public WORD SegDs;
				public WORD SegEs;
				public WORD SegFs;
				public WORD SegGs;
				public WORD SegSs;
				public DWORD EFlags;

				// Debug registers
				public DWORD64 Dr0;
				public DWORD64 Dr1;
				public DWORD64 Dr2;
				public DWORD64 Dr3;
				public DWORD64 Dr6;
				public DWORD64 Dr7;

				// Integer registers.
				public DWORD64 Rax;
				public DWORD64 Rcx;
				public DWORD64 Rdx;
				public DWORD64 Rbx;
				public DWORD64 Rsp;
				public DWORD64 Rbp;
				public DWORD64 Rsi;
				public DWORD64 Rdi;
				public DWORD64 R8;
				public DWORD64 R9;
				public DWORD64 R10;
				public DWORD64 R11;
				public DWORD64 R12;
				public DWORD64 R13;
				public DWORD64 R14;
				public DWORD64 R15;

				// Program counter.
				public DWORD64 Rip;

				// Floating point state.
				//union {
				//XMM_SAVE_AREA32 FltSave;
				//struct {

				//			M128A Header[2];
				//		M128A Legacy[8];
				//		M128A Xmm0;
				//		M128A Xmm1;
				//		M128A Xmm2;
				//		M128A Xmm3;
				//		M128A Xmm4;
				//		M128A Xmm5;
				//		M128A Xmm6;
				//		M128A Xmm7;
				//		M128A Xmm8;
				//		M128A Xmm9;
				//		M128A Xmm10;
				//		M128A Xmm11;
				//		M128A Xmm12;
				//		M128A Xmm13;
				//		M128A Xmm14;
				//		M128A Xmm15;
				//	}
				//	DUMMYSTRUCTNAME;
				//	}
				//DUMMYUNIONNAME;

				// Vector registers.
				//public M128A VectorRegister[26];
				public DWORD64 VectorControl;

				// Special debug control registers.
				public DWORD64 DebugControl;
				public DWORD64 LastBranchToRip;
				public DWORD64 LastBranchFromRip;
				public DWORD64 LastExceptionToRip;
				public DWORD64 LastExceptionFromRip;
			}
		}
		
		#region DEBUG_EVENT
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

		[StructLayout(LayoutKind.Sequential)]
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

		[StructLayout(LayoutKind.Sequential)]
		public struct CREATE_THREAD_DEBUG_INFO
		{
			public HANDLE hThread;
			public LPVOID lpThreadLocalBase;
			public LPTHREAD_START_ROUTINE lpStartAddress;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct EXCEPTION_DEBUG_INFO32
		{
			public EXCEPTION_RECORD32 ExceptionRecord;
			public DWORD dwFirstChance;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct EXCEPTION_DEBUG_INFO64
		{
			public EXCEPTION_RECORD64 ExceptionRecord;
			public DWORD dwFirstChance;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct EXCEPTION_RECORD32
		{
			[StructLayout(LayoutKind.Sequential, Size = sizeof(DWORD) * 15)]
			public struct EXCEPTION_INFORMATION { }

			public DWORD ExceptionCode;
			public DWORD ExceptionFlags;
			public DWORD ExceptionRecord;
			public DWORD ExceptionAddress;
			public DWORD NumberParameters;
			public EXCEPTION_INFORMATION ExceptionInformation;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct EXCEPTION_RECORD64
		{
			[StructLayout(LayoutKind.Sequential, Size = sizeof(DWORD64) * 15)]
			public struct EXCEPTION_INFORMATION { }

			public DWORD ExceptionCode;
			public DWORD ExceptionFlags;
			public DWORD64 ExceptionRecord;
			public DWORD64 ExceptionAddress;
			public DWORD NumberParameters;
			public DWORD __unusedAlignment;
			public EXCEPTION_INFORMATION ExceptionInformation;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct EXIT_PROCESS_DEBUG_INFO
		{
			public DWORD dwExitCode;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct EXIT_THREAD_DEBUG_INFO
		{
			public DWORD dwExitCode;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct LOAD_DLL_DEBUG_INFO
		{
			public HANDLE hFile;
			public LPVOID lpBaseOfDll;
			public DWORD dwDebugInfoFileOffset;
			public DWORD nDebugInfoSize;
			public LPVOID lpImageName;
			public WORD fUnicode;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct UNLOAD_DLL_DEBUG_INFO
		{
			public LPVOID lpBaseOfDll;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct OUTPUT_DEBUG_STRING_INFO
		{
			public LPSTR lpDebugStringData;
			public WORD fUnicode;
			public WORD nDebugStringLength;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct RIP_INFO
		{
			public DWORD dwError;
			public DWORD dwType;
		} 
		#endregion

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
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern BOOL GetThreadContext(HANDLE hThread, ref X86.CONTEXT lpContext);

		[DllImport(DllName, SetLastError = true)]
		public static extern DWORD GetThreadId(HANDLE Thread);

		[DllImport(DllName, SetLastError = true)]
		public static extern HANDLE OpenProcess(DWORD dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] BOOL bInheritHandle, DWORD dwProcessId);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern BOOL ReadProcessMemory(HANDLE hProcess, LPCVOID lpBaseAddress, LPVOID lpBuffer, SIZE_T nSize, out SIZE_T lpNumberOfBytesRead);

		[DllImport(DllName, SetLastError = true)]
		public static extern DWORD ResumeThread(HANDLE hThread);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern BOOL SetThreadContext(HANDLE hThread, LPCONTEXT lpContext);

		[DllImport(DllName, SetLastError = true)]
		public static extern DWORD SuspendThread(HANDLE hThread);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern BOOL TerminateProcess(HANDLE hProcess, UINT uExitCode);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern BOOL TerminateThread(HANDLE hThread, DWORD dwExitCode);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern BOOL WaitForDebugEventEx(out DEBUG_EVENT @event, DWORD milliseconds);

		[DllImport(DllName, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern BOOL WriteProcessMemory(HANDLE hProcess, LPVOID lpBaseAddress, LPVOID lpBuffer, SIZE_T nSize, out SIZE_T lpNumberOfBytesWritten);

		public static string GetFinalPathNameByHandle(HANDLE handle, DWORD dwFlags)
		{
			var pathLength = GetFinalPathNameByHandle(handle, null, 0, dwFlags);
			NativeMethods.CheckWin32(pathLength > 0);
			var pathBuilder = new StringBuilder((int)pathLength);
			pathBuilder.Length = (int)pathLength;
			NativeMethods.CheckWin32(GetFinalPathNameByHandle(handle, pathBuilder, pathLength, dwFlags) > 0);
			return pathBuilder.ToString();
		}

		public static string GetFinalPathNameByHandle(SafeFileHandle safeHandle, DWORD dwFlags)
		{
			Contract.Requires(safeHandle != null);

			var rawHandle = safeHandle.DangerousGetHandle();
			if (safeHandle.IsClosed || safeHandle.IsInvalid) throw new ArgumentException();

			var pathLength = GetFinalPathNameByHandle(rawHandle, null, 0, dwFlags);
			NativeMethods.CheckWin32(pathLength > 0);
			var pathBuilder = new StringBuilder((int)pathLength);
			pathBuilder.Length = (int)pathLength;
			NativeMethods.CheckWin32(GetFinalPathNameByHandle(rawHandle, pathBuilder, pathLength, dwFlags) > 0);
			return pathBuilder.ToString();
		}
	}

	public static class NativeMethods
	{
		public static void CheckWin32(bool result)
		{
			if (!result) throw GetLastWin32Exception();
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
