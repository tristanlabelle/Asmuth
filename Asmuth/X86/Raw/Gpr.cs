using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	public enum Gpr32
	{
		Eax,
		Ebx,
		Ecx,
		Edx,
		Ebp,
		Esp,
		Esi,
		Edi,
	}

    public enum Gpr64
	{
		Rax,
		Rbx,
		Rcx,
		Rdx,
		Rbp,
		Rsp,
		Rsi,
		Rdi,

		R8,
		R9,
		R10,
		R11,
		R12,
		R13,
		R14,
		R15
	}
}
