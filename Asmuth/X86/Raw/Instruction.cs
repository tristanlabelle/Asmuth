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
		// Only 3 instructions have imm64/moffset64 operands,
		// and they do not allow ModRM bytes, so they cannot have displacements.
		// For those, consider "displacement" as the most significant 4 bytes
		private uint immediate;
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
		// public Sib? Sib
		public int Displacement => displacement;
		// TODO: immediate
		// TODO: byte count
		#endregion

		#region Builder nested class
		public sealed class Builder
		{
			private Instruction instruction = CreateEmptyInstruction();

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

			#region Methods
			public void SetModRM(ModRM modRM, Sib sib = default(Sib), int displacement = 0)
			{
				instruction.flags |= Flags.HasModRM;
				instruction.modRM = modRM;
				instruction.sib = sib;
				instruction.displacement = displacement;
				throw new NotImplementedException();
			}

			public void ClearModRM()
			{
				instruction.flags &= ~Flags.HasModRM;
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
