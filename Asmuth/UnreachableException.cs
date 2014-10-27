using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth
{
	internal sealed class UnreachableException : Exception
	{
		public UnreachableException() : base("Code expected to be unreachable was reached.") { }
		public UnreachableException(string message) : base(message) { }
	}
}
