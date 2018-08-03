using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86
{
    public enum SimdPrefix : byte
	{
		None = 0,
		_66 = 1,
		_F2 = 2,
		_F3 = 3,
	}
}
