using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	public struct CpuidResult
	{
		public uint Eax, Ebx, Ecx, Edx;

		public uint Get(GprCode code)
		{
			switch (code)
			{
				case GprCode.Eax: return Eax;
				case GprCode.Ebx: return Ebx;
				case GprCode.Ecx: return Ecx;
				case GprCode.Edx: return Edx;
				default: throw new ArgumentOutOfRangeException(nameof(code));
			}
		}

		public override string ToString()
		{
			return $"(0x{Eax:X8}, 0x{Ebx:X8}, 0x{Ecx:X8}, 0x{Edx:X8})";
		}
	}
}
