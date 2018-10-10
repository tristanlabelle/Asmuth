using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Asmuth
{
	public static class RegexExtensions
	{
		public static bool TryGetValue(this Group group, out string value)
		{
			if (group.Success)
			{
				value = group.Value;
				return true;
			}
			else
			{
				value = null;
				return false;
			}
		}

		public static bool TryGetValue(this GroupCollection collection, string name, out string value)
			=> TryGetValue(collection[name], out value);
		public static bool TryGetValue(this GroupCollection collection, int i, out string value)
			=> TryGetValue(collection[i], out value);
	}
}
