using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Xed
{
	[TestClass]
	public sealed class XedBlotTests
	{
		[TestMethod]
		public void TestParseBlot()
		{
			AssertParseEqual("0x42", XedBlot.MakeBits("0100_0010"));
			AssertParseEqual("0b0100", XedBlot.MakeBits("0100"));
			AssertParseEqual("wrxb", XedBlot.MakeBits("wrxb"));
			AssertParseEqual("1_ddd", XedBlot.MakeBits("1ddd"));
			AssertParseEqual("MOD[0b11]", XedBlot.MakeBits(XedTestData.ModField, "11"));
			AssertParseEqual("MOD[mm]", XedBlot.MakeBits(XedTestData.ModField, "mm"));
			AssertParseEqual("UIMM0[ssss_uuuu]", XedBlot.MakeBits(XedTestData.UImm0Field, "ssssuuuu"));
			AssertParseEqual("UIMM0[i/8]", XedBlot.MakeBits(XedTestData.UImm0Field, new string('i', 8)));
			// TODO: "REXW[w]=1", "BASE0=@", "SIBBASE[bbb]=*"

			AssertParseEqual("MOD=3", XedBlot.MakeEquality(XedTestData.ModField, 3));
			AssertParseEqual("BASE0=ArAX()", XedBlot.MakeEquality(XedTestData.Base0Field, XedBlotValue.MakeCallResult("ArAX")));
			AssertParseEqual("REXW=w", XedBlot.MakeEquality(XedTestData.RexWField, XedBlotValue.MakeBits("w")));
			AssertParseEqual("OUTREG=XED_REG_XMM0", XedBlot.MakeEquality(XedTestData.OutRegField, 1));
			
			AssertParseEqual("MOD!=3", XedBlot.MakeInequality(XedTestData.OutRegField, 3));

			AssertParseEqual("MODRM()", XedBlot.MakeCall("MODRM"));
		}
		
		private static void AssertParseEqual(string str, XedBlot blot)
			=> Assert.AreEqual(blot, XedBlot.Parse(str, XedTestData.ResolveField).Item1);
	}
}
