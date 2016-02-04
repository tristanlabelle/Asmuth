using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	public enum UDReason
	{
		VexPrefixIn8086Mode,
		LegacyPrefixAndVex, // Lock/66/F2/F3/REX before VEX
		VexAndMmx,
		LockPrefix,
		ReservedVexField,
		UnimplementedRegister,
		IllegalOpcode,
	}
}
