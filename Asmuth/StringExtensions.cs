using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth
{
	public static class StringExtensions
	{
		public static string FormatInvariant(this string format, params object[] args)
		{
			return string.Format(CultureInfo.InvariantCulture, format, args);
		}
	}
}
