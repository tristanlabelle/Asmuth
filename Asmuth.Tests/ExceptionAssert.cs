using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth
{
	public static class ExceptionAssert
	{
		public static void Throws<TException>(Action action)
			where TException : Exception
		{
			try
			{
				action();
				Assert.Fail($"Expected exception {typeof(TException).Name}");
			}
			catch (TException) { }
			catch (Exception e)
			{
				Assert.Fail($"Expected exception {typeof(TException).Name} but got {e.GetType().Name}");
			}
		}
	}
}
