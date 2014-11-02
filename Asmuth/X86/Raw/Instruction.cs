using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	public struct Instruction
	{
		private enum Flags : byte
		{
			HasModRM,
			HasSib,
			Displacement_None,
			Displacement_8,
			Displacement_16,
			Displacement_32,
		}

		#region Fields
		public const int MaxLength = 15; // See 2.3.11

		private readonly ulong immediate;
		private readonly LegacyPrefixList legacyPrefixes;
		private readonly int displacement;
		private readonly Xex xex;
		private readonly byte mainByte;
		private readonly byte modRM;
		private readonly byte sib;
		private readonly Flags flags;
		#endregion
	}
}
