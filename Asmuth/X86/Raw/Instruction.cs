using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	[StructLayout(LayoutKind.Auto)]
	public sealed class Instruction
	{
		private enum Flags : byte
		{
			HasModRM = 1 << 0,

			DisplacementSize_Shift = 1,
			DisplacementSize_Mask = 7 << DisplacementSize_Shift,

			ImmediateSize_Shift = DisplacementSize_Shift + 3,
			ImmediateSize_Mask = 0xF << ImmediateSize_Shift
		}

		#region Fields
		public const int MaxLength = 15; // See 2.3.11

		private IList<LegacyPrefix> legacyPrefixes;
		private Xex xex;
		private byte mainByte;
		private ModRM modRM;
		private Sib sib;
		private int displacement;
		private ulong immediate;
		private Flags flags;
		#endregion

		#region Constructors
		private Instruction() { }
		#endregion

		#region Properties
		public IReadOnlyList<LegacyPrefix> LegacyPrefixes => (IReadOnlyList<LegacyPrefix>)legacyPrefixes;
		public Xex Xex => xex;
		public byte MainByte => mainByte;
		public ModRM? ModRM => (flags & Flags.HasModRM) == Flags.HasModRM ? modRM : (ModRM?)null;
		public Sib? Sib => (flags & Flags.HasModRM) == Flags.HasModRM && modRM.ImpliesSib() ? sib : (Sib?)null;
		public int Displacement => displacement;
		// TODO: immediate
		// TODO: byte count
		#endregion

		#region Builder nested class
		public enum BuildingState
		{
			Initial,
			Prefixes,
			PostXex,
			PostMainByte,
			PostModRM,
			PostSib,
			PostDisplacement,
			End
		}

		public sealed class Builder
		{
			private Instruction instruction = CreateEmptyInstruction();
			private BuildingState state;

			#region Properties
			public IList<LegacyPrefix> LegacyPrefixes => instruction.legacyPrefixes;

			public Xex Xex
			{
				get { return instruction.xex; }
				set { instruction.xex = value; }
			}

			public byte MainByte
			{
				get { return instruction.mainByte; }
				set { instruction.mainByte = value; }
			}
			#endregion

			private static Instruction CreateEmptyInstruction()
			{
				return new Instruction
				{
					legacyPrefixes = new List<LegacyPrefix>()
				};
			}
		}
		#endregion
	}
}
