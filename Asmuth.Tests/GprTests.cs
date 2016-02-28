using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	[TestClass]
	public sealed class GprTests
	{
		[TestMethod]
		public void TestName()
		{
			Assert.AreEqual("CL", new Gpr(GprCode.C, GprPart.Byte).Name);
			Assert.AreEqual("CH", new Gpr(GprCode.C, GprPart.HighByte).Name);
			Assert.AreEqual("CX", new Gpr(GprCode.C, GprPart.Word).Name);
			Assert.AreEqual("ECX", new Gpr(GprCode.C, GprPart.Dword).Name);
			Assert.AreEqual("RCX", new Gpr(GprCode.C, GprPart.Qword).Name);

			Assert.AreEqual("BPL", new Gpr(GprCode.BP, GprPart.Byte).Name);
			Assert.AreEqual("BP", new Gpr(GprCode.BP, GprPart.Word).Name);
			Assert.AreEqual("EBP", new Gpr(GprCode.BP, GprPart.Dword).Name);
			Assert.AreEqual("RBP", new Gpr(GprCode.BP, GprPart.Qword).Name);

			Assert.AreEqual("R8B", new Gpr(GprCode.R8, GprPart.Byte).Name);
			Assert.AreEqual("R8W", new Gpr(GprCode.R8, GprPart.Word).Name);
			Assert.AreEqual("R8D", new Gpr(GprCode.R8, GprPart.Dword).Name);
			Assert.AreEqual("R8", new Gpr(GprCode.R8, GprPart.Qword).Name);
		}
	}
}
