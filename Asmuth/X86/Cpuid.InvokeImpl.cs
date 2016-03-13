using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	using System.IO;
	using static Gpr;

	partial class Cpuid
	{
		private sealed class InvokeImpl
		{
			[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
			private delegate CpuidResult CpuidFunc(uint function, uint subfunction);

			private static readonly CpuidFunc func;

			static InvokeImpl()
			{
				// Generate the code
				int ptrSize = IntPtr.Size;
				var fullRegPart = ptrSize == 4 ? GprPart.Dword : GprPart.Qword;
				var xsp = new Gpr(GprCode.SP, fullRegPart);
				var xbx = new Gpr(GprCode.BX, fullRegPart);
				var xdi = new Gpr(GprCode.DI, fullRegPart);

				var codeStream = new MemoryStream();
				var codeWriter = new CodeWriter(codeStream,
					ptrSize == 4 ? CodeContext.Protected_Default32 : CodeContext.SixtyFourBit);

				// Save xBX and xDI
				codeWriter.Mov(xsp[ptrSize * -1], xbx);
				codeWriter.Mov(xsp[ptrSize * -2], xdi);

				// Call CPUID
				codeWriter.Mov(Eax, xsp[ptrSize + ptrSize]);
				codeWriter.Mov(Ecx, xsp[ptrSize + ptrSize + 4]);
				codeWriter.Cpuid();

				// Store results
				codeWriter.Mov(xdi, xsp[ptrSize]); // load retval*
				codeWriter.Mov(xdi[0], Eax);
				codeWriter.Mov(xdi[4], Ebx);
				codeWriter.Mov(xdi[8], Ecx);
				codeWriter.Mov(xdi[12], Edx);

				// Restore xBX and xDI
				codeWriter.Mov(xbx, xsp[ptrSize * -1]);
				codeWriter.Mov(xdi, xsp[ptrSize * -2]);

				codeWriter.Ret();

				// Make a function pointer out of it
				var codeBytes = codeStream.ToArray();
				var codePtr = Marshal.AllocHGlobal(codeBytes.Length);
				try
				{
					Marshal.Copy(codeBytes, 0, codePtr, codeBytes.Length);
					func = (CpuidFunc)Marshal.GetDelegateForFunctionPointer(codePtr, typeof(Cpuid));
				}
				catch
				{
					Marshal.FreeBSTR(codePtr);
					throw;
				}
			}

			public static CpuidResult Invoke(uint function, byte subfunction)
				=> func(function, subfunction);
		}
	}
}
