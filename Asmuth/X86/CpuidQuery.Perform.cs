using System;
using System.Collections.Generic;
using System.IO;
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
				// Generate the code
				int ptrSize = IntPtr.Size;
				var fullRegPart = ptrSize == 4 ? GprPart.Dword : GprPart.Qword;
				var xsp = new Gpr(GprCode.SP, fullRegPart);
				var xbx = new Gpr(GprCode.BX, fullRegPart);
				var xdi = new Gpr(GprCode.DI, fullRegPart);
				var derefXdi = EffectiveAddress.Indirect(xdi);
				Func<int, EffectiveAddress> stack = (int displacement) => EffectiveAddress.Indirect(xsp, displacement);

				var codeStream = new MemoryStream();
				var codeWriter = new CodeWriter(codeStream,
					ptrSize == 4 ? CodeContext.Protected_Default32 : CodeContext.SixtyFourBit);

				// Save xBX and xDI
				codeWriter.Mov(stack(ptrSize * -1), xbx);
				codeWriter.Mov(stack(ptrSize * -2), xdi);

				// Call CPUID
				codeWriter.Mov(Gpr.Eax, stack(ptrSize));
				codeWriter.Mov(Gpr.Ecx, stack(ptrSize + 4));
				codeWriter.Cpuid();

				// Store results
				codeWriter.Mov(xdi, stack(ptrSize + 8));
				codeWriter.Mov(derefXdi, Gpr.Eax);
				codeWriter.Mov(xdi, stack(ptrSize + 8 + ptrSize));
				codeWriter.Mov(derefXdi, Gpr.Ebx);
				codeWriter.Mov(xdi, stack(ptrSize + 8 + ptrSize * 2));
				codeWriter.Mov(derefXdi, Gpr.Ecx);
				codeWriter.Mov(xdi, stack(ptrSize + 8 + ptrSize * 3));
				codeWriter.Mov(derefXdi, Gpr.Edx);

				// Restore xBX and xDI
				codeWriter.Mov(xbx, stack(ptrSize * -1));
				codeWriter.Mov(xdi, stack(ptrSize * -2));

				codeWriter.Ret();

				// Make a function pointer out of it
				var codeBytes = codeStream.ToArray();
				var codePtr = Marshal.AllocHGlobal(codeBytes.Length);
				Marshal.Copy(codeBytes, 0, codePtr, codeBytes.Length);
				Cpuid = (Cpuid)Marshal.GetDelegateForFunctionPointer(codePtr, typeof(Cpuid));
			}
		}

		public static void Perform(uint function, byte subfunction,
			out uint eax, out uint ebx, out uint ecx, out uint edx)
		{
			Lazy.Cpuid(function, subfunction, out eax, out ebx, out ecx, out edx);
		}

		public static void Perform(uint function, out uint eax, out uint ebx, out uint ecx, out uint edx)
			=> Perform(function, 0, out eax, out ebx, out ecx, out edx);

		public static uint Perform(CpuidQuery query)
		{
			uint eax, ebx, ecx, edx;
			Perform(query.Function, query.InputEcx.GetValueOrDefault(),
				out eax, out ebx, out ecx, out edx);

			uint output;
			switch (query.OutputGpr)
			{
				case GprCode.Eax: output = eax; break;
				case GprCode.Ebx: output = ebx; break;
				case GprCode.Ecx: output = ecx; break;
				case GprCode.Edx: output = edx; break;
				default: throw new UnreachableException();
			}

			return Bits.MaskAndShiftRight(output, query.OutputMask, query.bitShift);
		}
	}
}
