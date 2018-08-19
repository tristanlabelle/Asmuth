using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Asmuth.X86
{
	using Flags = OpcodeEncodingFlags;
	using Result = SetComparisonResult;
	
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
				if (!IgnorableField(lhs.Flags, rhs.Flags, Flags.LongMode_Mask)) return;
				if (!IgnorableField(lhs.Flags, rhs.Flags, Flags.AddressSize_Mask)) return;
				if (!Field(lhs.Flags, rhs.Flags, Flags.VexType_Mask)) return;
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
				return lhs == rhs ? AddEqual() : AddDisjoint();
			}

			private bool IgnorableField(Flags lhs, Flags rhs, Flags mask)
			{
				lhs &= mask;
				rhs &= mask;
				if (lhs == rhs) return AddEqual();
				if (lhs == 0) return AddSupersetSubset();
				if (rhs == 0) return AddSubsetSuperset();
				return AddDisjoint();
			}

			private bool MainByte(byte lhs, byte lhsMask, byte rhs, byte rhsMask)
			{
				byte fullMask = (byte)(lhsMask & rhsMask);
				if ((lhs & fullMask) != (rhs & fullMask)) return AddDisjoint();
				if (lhsMask == rhsMask) return AddEqual();
				if (fullMask == lhsMask) return AddSupersetSubset();
				if (rhsMask == fullMask) return AddSubsetSuperset();
				throw new UnreachableException();
			}

			private bool ModRM(Flags lhsFlags, ModRM lhsModRM, Flags rhsFlags, ModRM rhsModRM)
			{
				// Compare ModRM presence
				if (lhsFlags.HasModRM() != rhsFlags.HasModRM()) return AddOverlapping();
				if (!lhsFlags.HasModRM()) return AddEqual();

				// Compare reg
				bool lhsHasFixedReg = (lhsFlags & Flags.ModRM_FixedReg) != 0;
				bool rhsHasFixedReg = (rhsFlags & Flags.ModRM_FixedReg) != 0;
				if (lhsHasFixedReg && rhsHasFixedReg)
					if (lhsModRM.GetReg() != rhsModRM.GetReg())
						return AddDisjoint();
				if (!lhsHasFixedReg && rhsHasFixedReg)
					if (!AddSupersetSubset())
						return false;
				if (lhsHasFixedReg && !rhsHasFixedReg)
					if (!AddSubsetSuperset())
						return false;

				// Compare Mod and RM
				var lhsRMFlags = lhsFlags & Flags.ModRM_RM_Mask;
				var rhsRMFlags = rhsFlags & Flags.ModRM_RM_Mask;

				// Handle 'any' ModRMs
				bool lhsIsAnyRM = lhsRMFlags == Flags.ModRM_RM_Any;
				bool rhsIsAnyRM = rhsRMFlags == Flags.ModRM_RM_Any;
				if (lhsIsAnyRM && rhsIsAnyRM)
					return AddEqual();
				if (lhsIsAnyRM && !rhsIsAnyRM)
					if (!AddSupersetSubset())
						return false;
				if (!lhsIsAnyRM && rhsHasFixedReg)
					if (!AddSubsetSuperset())
						return false;

				// Handle indirect ModRMs
				bool lhsIsMemMod = lhsRMFlags == Flags.ModRM_RM_Indirect;
				bool rhsIsMemMod = rhsRMFlags == Flags.ModRM_RM_Indirect;
				if (lhsIsMemMod != rhsIsMemMod) return AddDisjoint();
				if (lhsIsMemMod) return AddEqual();

				// Handle fixed vs direct
				bool lhsFixedRM = lhsRMFlags == Flags.ModRM_RM_Fixed;
				bool rhsFixedRM = rhsRMFlags == Flags.ModRM_RM_Fixed;
				if (!lhsFixedRM && rhsFixedRM) return AddSupersetSubset();
				if (lhsFixedRM && !rhsFixedRM) return AddSubsetSuperset();

				Debug.Assert(lhsFixedRM == rhsFixedRM);
				return !lhsFixedRM || lhsModRM.GetRM() == rhsModRM.GetRM()
					? AddEqual() : AddDisjoint();
			}

			private bool Immediate(Flags lhsFlags, byte lhsImm8, Flags rhsFlags, byte rhsImm8)
			{
				int lhsSize = lhsFlags.GetImmediateSizeInBytes();
				int rhsSize = rhsFlags.GetImmediateSizeInBytes();
				if (lhsSize != rhsSize) return AddOverlapping();

				if (lhsSize == 1)
				{
					if (!IgnorableField(lhsFlags, rhsFlags, Flags.Imm8Ext_Mask)) return false;

					bool lhsFixedImm8 = (lhsFlags & Flags.Imm8Ext_Mask) == Flags.Imm8Ext_Fixed;
					bool rhsFixedImm8 = (rhsFlags & Flags.Imm8Ext_Mask) == Flags.Imm8Ext_Fixed;
					if (lhsFixedImm8 && rhsFixedImm8)
						if (lhsImm8 != rhsImm8)
							return AddDisjoint();
					if (!lhsFixedImm8 && rhsFixedImm8)
						return AddSupersetSubset();
					if (lhsFixedImm8 && !rhsFixedImm8)
						return AddSubsetSuperset();
				}

				return AddEqual();
			}

			private bool AddSupersetSubset()
			{
				if (result == Result.Overlapping) return true;
				if (result == Result.SubsetSuperset) return AddOverlapping();
				result = Result.SupersetSubset;
				return true;
			}

			private bool AddSubsetSuperset()
			{
				if (result == Result.Overlapping) return true;
				if (result == Result.SupersetSubset) return AddOverlapping();
				result = Result.SubsetSuperset;
				return true;
			}

			public bool AddEqual() => true;

			private bool AddOverlapping()
			{
				result = Result.Overlapping;
				// We must still continue in case a later field makes them disjoint.
				return true;
			}

			private bool AddDisjoint()
			{
				result = Result.Disjoint;
				return false;
			}
		}

		public static Result Compare(OpcodeEncoding lhs, OpcodeEncoding rhs)
			=> Comparer.Compare(lhs, rhs);
	}
}
