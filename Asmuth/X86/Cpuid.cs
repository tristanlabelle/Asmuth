using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	public static partial class Cpuid
	{
		public static CpuidResult Invoke(uint function, byte subfunction = 0)
			=> InvokeImpl.Invoke(function, subfunction);

		public static CpuidFeatureFlags QueryFeatureFlags()
		{
			var result = Invoke(1);
			return (CpuidFeatureFlags)(((ulong)result.Edx << 32) | result.Ecx);
		}

		public static CpuidExtendedFeatureFlags QueryExtendedFeatureFlags()
		{
			var result = Invoke(1);
			return (CpuidExtendedFeatureFlags)(((ulong)result.Edx << 32) | result.Ecx);
		}
	}
}
