using System;
using System.Collections.Generic;
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
}
