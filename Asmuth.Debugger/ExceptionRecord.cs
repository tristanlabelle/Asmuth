using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.Debugger
{
	using System.Diagnostics.Contracts;
	using System.Runtime.InteropServices;
	using static Kernel32;

	public sealed class ExceptionRecord
	{
		private readonly uint code;
		private readonly ulong address;
		private readonly uint flags;
		private readonly ulong[] arguments;
		private readonly ExceptionRecord child;

		internal ExceptionRecord(uint code, ulong address, uint flags, ulong[] arguments, ExceptionRecord child)
		{
			this.code = code;
			this.address = address;
			this.flags = flags;
			this.arguments = arguments;
		}

		public uint Code => code;
		public ulong Address => address;
		public bool IsContinuable => (flags & EXCEPTION_NONCONTINUABLE) == 0;
		public ExceptionRecord Child => child;

		internal static ExceptionRecord FromStruct(ref EXCEPTION_RECORD32 record)
		{
			Contract.Requires(IntPtr.Size == sizeof(int));

			ExceptionRecord child = null;
			if (record.ExceptionRecord != 0)
			{
				var childStruct = (EXCEPTION_RECORD32)Marshal.PtrToStructure(
					unchecked((IntPtr)record.ExceptionRecord), typeof(EXCEPTION_RECORD32));
				child = FromStruct(ref childStruct);
			}
			
			return new ExceptionRecord(record.ExceptionCode, record.ExceptionAddress, record.ExceptionFlags,
				GetArguments(record.NumberParameters, record.ExceptionInformation), child);
		}

		internal static ExceptionRecord FromStruct(ref EXCEPTION_RECORD64 record)
		{
			Contract.Requires(IntPtr.Size == sizeof(long));

			ExceptionRecord child = null;
			if (record.ExceptionRecord != 0)
			{
				var childStruct = (EXCEPTION_RECORD64)Marshal.PtrToStructure(
					unchecked((IntPtr)record.ExceptionRecord), typeof(EXCEPTION_RECORD64));
				child = FromStruct(ref childStruct);
			}

			return new ExceptionRecord(record.ExceptionCode, record.ExceptionAddress, record.ExceptionFlags,
				GetArguments(record.NumberParameters, record.ExceptionInformation), child);
		}

		private static ulong[] GetArguments<T>(ulong number, T information)
		{
			ulong[] arguments = new ulong[number];
			if (arguments.Length > 0)
			{
				var gcHandle = GCHandle.Alloc(information, GCHandleType.Pinned);
				try
				{
					IntPtr baseAddress = gcHandle.AddrOfPinnedObject();
					for (int i = 0; i < arguments.Length; ++i)
						arguments[i] = unchecked((ulong)Marshal.ReadIntPtr(baseAddress + i * IntPtr.Size));
				}
				finally
				{
					gcHandle.Free();
				}
			}

			return arguments;
		}
	}
}
