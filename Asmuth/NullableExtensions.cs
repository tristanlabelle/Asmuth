using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth
{
	public static class NullableExtensions
	{
		public static bool TryGetValue<T>(this T? nullable, out T value) where T : struct
		{
			value = nullable.GetValueOrDefault();
			return nullable.HasValue;
		}
	}
}
