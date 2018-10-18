using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Asmuth.X86
{
	partial struct OpcodeEncoding
	{
		private struct Comparer
		{
			private SetComparisonResult result;

			private Comparer(SetComparisonResult result) { this.result = result; }

			public static SetComparisonResult Compare(OpcodeEncoding lhs, OpcodeEncoding rhs)
			{
				var comparer = new Comparer(SetComparisonResult.Equal);
				comparer.Encoding(lhs, rhs);
				return comparer.result;
			}

			private void Encoding(OpcodeEncoding lhs, OpcodeEncoding rhs)
			{
				if (!Compare(lhs.X64, rhs.X64)) return;
				if (!Compare((int?)lhs.AddressSize, (int?)rhs.AddressSize)) return;
				if (!Compare(lhs.OperandSize, rhs.OperandSize)) return;
				if (lhs.VexType != rhs.VexType)
				{
					AddDisjoint();
					return;
				}

				if (!Compare((int?)lhs.VectorSize, (int?)rhs.VectorSize)) return;
				if (!Compare((int?)lhs.SimdPrefix, (int?)rhs.SimdPrefix)) return;
				if (!Compare((int)lhs.Map, (int)rhs.Map)) return;

				if (!MainByte(lhs.MainByte, lhs.MainByteMask, rhs.MainByte, rhs.MainByteMask)) return;

				result = result.Combine(AddressingFormEncoding.Compare(lhs.AddressingForm, rhs.AddressingForm));
				if (result == SetComparisonResult.Disjoint || result == SetComparisonResult.Overlapping) return;

				if (!Immediate(lhs.ImmediateSize, lhs.Imm8Ext, rhs.ImmediateSize, rhs.Imm8Ext)) return;
			}

			private bool Compare(OperandSizeEncoding lhs, OperandSizeEncoding rhs)
			{
				if (lhs == rhs) return AddEqual();
				if (lhs == OperandSizeEncoding.Any) return AddSupersetSubset();
				if (rhs == OperandSizeEncoding.Any) return AddSubsetSuperset();

				if (lhs == OperandSizeEncoding.NoPromotion
					&& (rhs == OperandSizeEncoding.Word || rhs == OperandSizeEncoding.Dword))
					return AddOverlapping();
				if ((lhs == OperandSizeEncoding.Word || lhs == OperandSizeEncoding.Dword)
					&& rhs == OperandSizeEncoding.NoPromotion)
					return AddOverlapping();
				return AddDisjoint();
			}

			private bool Compare(bool? lhs, bool? rhs)
			{
				if (lhs == rhs) return AddEqual();
				if (!lhs.HasValue) return AddSupersetSubset();
				if (!rhs.HasValue) return AddSubsetSuperset();
				return AddDisjoint();
			}

			private bool Compare(int lhs, int rhs)
			{
				return lhs == rhs ? AddEqual() : AddDisjoint();
			}

			private bool Compare(int? lhs, int? rhs)
			{
				if (lhs == rhs) return AddEqual();
				if (!lhs.HasValue) return AddSupersetSubset();
				if (!rhs.HasValue) return AddSubsetSuperset();
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

			private bool Immediate(ImmediateSizeEncoding lhsImmSize, byte? lhsImm8Ext,
				ImmediateSizeEncoding rhsImmSize, byte? rhsImm8Ext)
			{
				if (lhsImmSize != rhsImmSize) return AddOverlapping();
				return lhsImmSize.FixedInBytes == 1
					? Compare((int?)lhsImm8Ext, (int?)rhsImm8Ext)
					: AddEqual();
			}

			private bool AddSupersetSubset()
			{
				if (result == SetComparisonResult.Overlapping) return true;
				if (result == SetComparisonResult.SubsetSuperset) return AddOverlapping();
				result = SetComparisonResult.SupersetSubset;
				return true;
			}

			private bool AddSubsetSuperset()
			{
				if (result == SetComparisonResult.Overlapping) return true;
				if (result == SetComparisonResult.SupersetSubset) return AddOverlapping();
				result = SetComparisonResult.SubsetSuperset;
				return true;
			}

			public bool AddEqual() => true;

			private bool AddOverlapping()
			{
				result = SetComparisonResult.Overlapping;
				// We must still continue in case a later field makes them disjoint.
				return true;
			}

			private bool AddDisjoint()
			{
				result = SetComparisonResult.Disjoint;
				return false;
			}
		}

		public static SetComparisonResult Compare(OpcodeEncoding lhs, OpcodeEncoding rhs)
			=> Comparer.Compare(lhs, rhs);
	}
}
