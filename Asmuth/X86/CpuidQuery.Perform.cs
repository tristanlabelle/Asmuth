using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	partial struct CpuidQuery
	{
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		private delegate void Cpuid(uint function, uint subfunction,
			out uint eax, out uint ebx, out uint ecx, out uint edx);

		private sealed class Lazy
		{
			public static Cpuid Cpuid { get; }

			static Lazy()
			{
				// TODO: Detect x86/amd64 processor!
				byte[] codeBytes =
				{

				};

				int ptrSize = IntPtr.Size;

				// MOV [esp-4/8], r/ebx # Rbx is not caller-saved
				// MOV [esp-8/16], r/edi # We'll use rdi as a temporary
				// MOV eax, [esp+4/8]
				// MOV ecx, [esp+8/12]
				// CPUID
				// MOV r/edi, [esp+12/16]
				// MOV [r/edi], eax
				// MOV r/edi, [esp+16/24]
				// MOV [r/edi], ebx
				// MOV r/edi, [esp+20/32]
				// MOV [r/edi], ecx
				// MOV r/edi, [esp+24/40]
				// MOV [r/edi], edx
				// MOV r/ebx, [esp-4/8] # Restore rbx
				// MOV r/edi, [esp-8/16] # Restore rdi
				// RET

				var codePtr = Marshal.AllocHGlobal(codeBytes.Length);
				Marshal.Copy(codeBytes, 0, codePtr, codeBytes.Length);
				Cpuid = (Cpuid)Marshal.GetDelegateForFunctionPointer(codePtr, typeof(Cpuid));

				throw new NotImplementedException();
			}
		}

		public static void Perform(uint function, byte subfunction,
			out uint eax, out uint ebx, out uint ecx, out uint edx)
		{
			Lazy.Cpuid(function, subfunction, out eax, out ebx, out ecx, out edx);
		}

		public static void Perform(uint function, out uint eax, out uint ebx, out uint ecx, out uint edx)
			=> Perform(function, 0, out eax, out ebx, out ecx, out edx);
	}
}
