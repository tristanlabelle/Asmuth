using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	[TestClass]
	public sealed class CpuidTests
	{
		[TestMethod]
		public void TestHasFpu()
		{
			PortableExecutableKinds peKind;
			ImageFileMachine machine;
			typeof(object).GetTypeInfo().Module.GetPEKind(out peKind, out machine);

			if (machine != ImageFileMachine.I386 && machine != ImageFileMachine.AMD64)
				Assert.Inconclusive("Test can only run on X86 or AMD64.");

			var featureFlags = Cpuid.QueryFeatureFlags();
			Assert.AreEqual(CpuidFeatureFlags.Fpu, featureFlags & CpuidFeatureFlags.Fpu);
		}
	}
}
