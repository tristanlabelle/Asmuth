using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86
{
	using Flags = OpcodeEncodingFlags;
	using Result = OpcodeEncodingComparisonResult;
	
	public enum OpcodeEncodingComparisonResult
	{
		Equal,
		Different,
		Ambiguous,
		LhsMoreGeneral,
		RhsMoreGeneral,
	}

	partial struct OpcodeEncoding
	{
		private struct Comparer
		{
			private Result result;

			private Comparer(Result result) { this.result = result; }

			public static Result Compare(OpcodeEncoding lhs, OpcodeEncoding rhs)
			{
				var comparer = new Comparer(Result.Equal);
				comparer.Encoding(lhs, rhs);
				return comparer.result;
			}

			private void Encoding(OpcodeEncoding lhs, OpcodeEncoding rhs)
			{
				if (!IgnorableField(lhs.Flags, rhs.Flags, Flags.CodeSegmentType_Mask)) return;
				if (!Field(lhs.Flags, rhs.Flags, Flags.XexType_Mask)) return;
				if (!IgnorableField(lhs.Flags, rhs.Flags, Flags.OperandSize_Mask)) return;
				if (!IgnorableField(lhs.Flags, rhs.Flags, Flags.VexL_Mask)) return;
				if (!IgnorableField(lhs.Flags, rhs.Flags, Flags.SimdPrefix_Mask)) return;
				if (!IgnorableField(lhs.Flags, rhs.Flags, Flags.RexW_Mask)) return;
				if (!Field(lhs.Flags, rhs.Flags, Flags.Map_Mask)) return;
				if (!MainByte(lhs.MainByte, lhs.MainByteMask, rhs.MainByte, rhs.MainByteMask)) return;
				if (!ModRM(lhs.Flags, lhs.ModRM, rhs.Flags, rhs.ModRM)) return;
				if (!Immediate(lhs.Flags, lhs.Imm8, rhs.Flags, rhs.Imm8)) return;
			}

			private bool Field(Flags lhs, Flags rhs, Flags mask)
			{
				lhs &= mask;
				rhs &= mask;
				return lhs == rhs ? AddEqual() : AddDifferent();
			}

			private bool IgnorableField(Flags lhs, Flags rhs, Flags mask)
			{
				lhs &= mask;
				rhs &= mask;
				if (lhs == rhs) return AddEqual();
				if (lhs == 0) return AddLhsMoreGeneral();
				if (rhs == 0) return AddRhsMoreGeneral();
				return AddDifferent();
			}

			private bool MainByte(byte lhs, byte lhsMask, byte rhs, byte rhsMask)
			{
				byte fullMask = (byte)(lhsMask & rhsMask);
				if ((lhs & fullMask) != (rhs & fullMask)) return AddDifferent();
				if (lhsMask == rhsMask) return AddEqual();
				if (fullMask == lhsMask) return AddLhsMoreGeneral();
				if (rhsMask == fullMask) return AddRhsMoreGeneral();
				throw new UnreachableException();
			}

			private bool ModRM(Flags lhsFlags, ModRM lhsModRM, Flags rhsFlags, ModRM rhsModRM)
			{
				// Compare ModRM presence
				if (lhsFlags.HasModRM() != rhsFlags.HasModRM()) return AddAmbiguous();
				if (!lhsFlags.HasModRM()) return AddEqual();

				// Compare reg
				bool lhsHasFixedReg = (lhsFlags & Flags.ModRM_FixedReg) != 0;
				bool rhsHasFixedReg = (rhsFlags & Flags.ModRM_FixedReg) != 0;
				if (lhsHasFixedReg && rhsHasFixedReg)
					if (lhsModRM.GetReg() != rhsModRM.GetReg())
						return AddDifferent();
				if (!lhsHasFixedReg && rhsHasFixedReg)
					if (!AddLhsMoreGeneral())
						return false;
				if (lhsHasFixedReg && !rhsHasFixedReg)
					if (!AddRhsMoreGeneral())
						return false;

				// Compare Mod and RM
				var lhsRMFlags = lhsFlags & Flags.ModRM_RM_Mask;
				var rhsRMFlags = rhsFlags & Flags.ModRM_RM_Mask;
				if ((lhsFlags.HasDirectModRM_Mod() && rhsRMFlags == Flags.ModRM_RM_Indirect)
					|| (rhsFlags.HasDirectModRM_Mod() && lhsRMFlags == Flags.ModRM_RM_Indirect))
				{
					// Cannot possibly collide
					return AddDifferent();
				}

				throw new NotImplementedException();
			}

			private bool Immediate(Flags lhsFlags, byte lhsImm8, Flags rhsFlags, byte rhsImm8)
			{
				int lhsSize = lhsFlags.GetImmediateSizeInBytes();
				int rhsSize = rhsFlags.GetImmediateSizeInBytes();
				if (lhsSize != rhsSize) return AddAmbiguous();

				if (lhsSize == 1)
				{
					if (!IgnorableField(lhsFlags, rhsFlags, Flags.Imm8Ext_Mask)) return false;

					bool lhsFixedImm8 = (lhsFlags & Flags.Imm8Ext_Mask) == Flags.Imm8Ext_Fixed;
					bool rhsFixedImm8 = (rhsFlags & Flags.Imm8Ext_Mask) == Flags.Imm8Ext_Fixed;
					if (lhsFixedImm8 && rhsFixedImm8)
						if (lhsImm8 != rhsImm8)
							return AddDifferent();
					if (!lhsFixedImm8 && rhsFixedImm8)
						return AddLhsMoreGeneral();
					if (lhsFixedImm8 && !rhsFixedImm8)
						return AddRhsMoreGeneral();
				}

				return AddEqual();
			}

			private bool AddLhsMoreGeneral()
			{
				if (result == Result.RhsMoreGeneral)
					return AddAmbiguous();

				result = Result.LhsMoreGeneral;
				return true;
			}

			private bool AddRhsMoreGeneral()
			{
				if (result == Result.LhsMoreGeneral)
					return AddAmbiguous();

				result = Result.RhsMoreGeneral;
				return true;
			}

			public bool AddEqual() => true;

			private bool AddAmbiguous()
			{
				result = Result.Ambiguous;
				return false;
			}

			private bool AddDifferent()
			{
				result = Result.Different;
				return false;
			}
		}

		public static Result Compare(OpcodeEncoding lhs, OpcodeEncoding rhs)
			=> Comparer.Compare(lhs, rhs);
	}
}
