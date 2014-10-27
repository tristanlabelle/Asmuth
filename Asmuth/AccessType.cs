using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth
{
	[Flags]
	public enum AccessType : byte
	{
		None = 0,
		Read = 1,
		Write = 2,
		ReadWrite = 3
	}
}
