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
			Assert.AreEqual("cl", new Gpr(GprCode.C, GprPart.Byte).Name);
			Assert.AreEqual("ch", new Gpr(GprCode.C, GprPart.HighByte).Name);
			Assert.AreEqual("cx", new Gpr(GprCode.C, GprPart.Word).Name);
			Assert.AreEqual("ecx", new Gpr(GprCode.C, GprPart.Dword).Name);
			Assert.AreEqual("rcx", new Gpr(GprCode.C, GprPart.Qword).Name);

			Assert.AreEqual("bpl", new Gpr(GprCode.BP, GprPart.Byte).Name);
			Assert.AreEqual("bp", new Gpr(GprCode.BP, GprPart.Word).Name);
			Assert.AreEqual("ebp", new Gpr(GprCode.BP, GprPart.Dword).Name);
			Assert.AreEqual("rbp", new Gpr(GprCode.BP, GprPart.Qword).Name);

			Assert.AreEqual("r8b", new Gpr(GprCode.R8, GprPart.Byte).Name);
			Assert.AreEqual("r8w", new Gpr(GprCode.R8, GprPart.Word).Name);
			Assert.AreEqual("r8d", new Gpr(GprCode.R8, GprPart.Dword).Name);
			Assert.AreEqual("r8", new Gpr(GprCode.R8, GprPart.Qword).Name);
		}
	}
}
