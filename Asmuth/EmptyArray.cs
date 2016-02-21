using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth
{
	public static class EmptyArray<T>
	{
		public static T[] Rank1 { get; } = new T[0];
		public static T[,] Rank2 { get; } = new T[0, 0];
	}
}
