using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth
{
	public static class ListExtensions
	{
		public static bool SwapRemove<T>(this IList<T> list, T item)
		{
			int index = list.IndexOf(item);
			if (index < 0) return false;
			SwapRemoveAt(list, index);
			return true;
		}

		public static void SwapRemoveAt<T>(this IList<T> list, int index)
		{
			if (index < list.Count - 1) list[index] = list[list.Count - 1];
			list.RemoveAt(list.Count - 1);
		}
	}
}
