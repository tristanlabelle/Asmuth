using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw.Nasm
{
	public enum NasmPrefix
	{
		ES,
		CS,
		SS,
		DS,
		FS,
		GS,
		A32,
		A64,
		ASP,
		LOCK,
		O16,
		O32,
		O64,
		OSP,
		REP,
		REPE,
		REPNE,
		REPNZ,
		REPZ,
		TIMES,
		WAIT,
		XACQUIRE,
		XRELEASE,
		BND,
		NOBND,
		EVEX,
		VEX3,
		VEX2,
	}
}
