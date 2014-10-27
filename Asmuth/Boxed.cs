using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth
{
	public sealed class Boxed<T> where T : struct
	{
		public readonly T Value;

		public Boxed() { }
		public Boxed(T value) { this.Value = value; }
	}
}
