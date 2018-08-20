using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Asmuth
{
	public enum SetComparisonResult
	{
		Disjoint, // LHS & RHS = 0
		Equal, // LHS = RHS
		Overlapping, // LHS & RHS != 0
		SupersetSubset, // LHS > RHS
		SubsetSuperset // RHS < LHS
	}

	public static class SetComparisonResultEnum
	{
		public static SetComparisonResult Combine(
			this SetComparisonResult result, SetComparisonResult other)
		{
			if (result == other) return result;
			if (result == SetComparisonResult.Disjoint || other == SetComparisonResult.Disjoint)
				return SetComparisonResult.Disjoint;
			if ((result == SetComparisonResult.Overlapping || other == SetComparisonResult.Overlapping)
				|| (result == SetComparisonResult.SubsetSuperset && other == SetComparisonResult.SupersetSubset)
				|| (result == SetComparisonResult.SupersetSubset && other == SetComparisonResult.SubsetSuperset))
				return SetComparisonResult.Overlapping;
			if (result == SetComparisonResult.Equal) return other;
			Debug.Assert(other == SetComparisonResult.Equal);
			return result;
		}
	}
}
