using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	/// <summary>
	/// Provides constants for common opcodes that deserve hard-coding.
	/// </summary>
	public static class KnownOpcodes
	{
		public const byte Lea = 0x8D;
		public const byte Nop = 0x90;
		public const byte RetNearAndPop = 0xC2;
		public const byte RetNear = 0xC3;
		public const byte Int3 = 0xCC;
		public const byte Int = 0xCD;
		public const byte Jmp = 0xE9;
		public const byte Jmp8 = 0xEB;
		public const byte JmpI = 0xFF; // Indirect jump
	}
}
