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
			Assert.AreEqual("cl", Gpr.Byte(GprCode.C, hasRex: true).Name);
			Assert.AreEqual("ch", Gpr.Byte(GprCode.BplOrCH, hasRex: false).Name);
			Assert.AreEqual("cx", Gpr.Word(GprCode.C).Name);
			Assert.AreEqual("ecx", Gpr.Dword(GprCode.C).Name);
			Assert.AreEqual("rcx", Gpr.Qword(GprCode.C).Name);

			Assert.AreEqual("bpl", Gpr.Byte(GprCode.BP, hasRex: true).Name);
			Assert.AreEqual("bp", Gpr.Word(GprCode.BP).Name);
			Assert.AreEqual("ebp", Gpr.Dword(GprCode.BP).Name);
			Assert.AreEqual("rbp", Gpr.Qword(GprCode.BP).Name);

			Assert.AreEqual("r8b", Gpr.Byte(GprCode.R8, hasRex: true).Name);
			Assert.AreEqual("r8w", Gpr.Word(GprCode.R8).Name);
			Assert.AreEqual("r8d", Gpr.Dword(GprCode.R8).Name);
			Assert.AreEqual("r8", Gpr.Qword(GprCode.R8).Name);
		}
	}
}
