using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Encoding.Xed
{
	[TestClass]
	public sealed class XedBlotTests
	{
		[TestMethod]
		public void TestParseBitsBlots()
		{
			AssertParseEqual("0x42", XedBlot.MakeBits("0100_0010"));
			AssertParseEqual("0b0100", XedBlot.MakeBits("0100"));
			AssertParseEqual("wrxb", XedBlot.MakeBits("wrxb"));
			AssertParseEqual("1_ddd", XedBlot.MakeBits("1ddd"));
			AssertParseEqual("0b0100_0", XedBlot.MakeBits("01000"));
			AssertParseEqual("MOD[0b11]", XedBlot.MakeBits(XedTestData.ModField, "11"));
			AssertParseEqual("MOD[mm]", XedBlot.MakeBits(XedTestData.ModField, "mm"));
			AssertParseEqual("UIMM0[ssss_uuuu]", XedBlot.MakeBits(XedTestData.UImm0Field, "ssssuuuu"));
			AssertParseEqual("UIMM0[i/8]", XedBlot.MakeBits(XedTestData.UImm0Field, new string('i', 8)));
			// TODO: "REXW[w]=1", "SIBBASE[bbb]=*"
		}

		[TestMethod]
		public void TestParseEqualityBlots()
		{
			AssertParseEqual("MOD=3", XedBlot.MakeEquality(XedTestData.ModField, 3));
			AssertParseEqual("BASE0=ArAX()", XedBlot.MakeEquality(XedTestData.Base0Field, XedBlotValue.MakeCallResult("ArAX")));
			AssertParseEqual("REXW=w", XedBlot.MakeEquality(XedTestData.RexWField, XedBlotValue.MakeBits("w")));
			AssertParseEqual("OUTREG=XED_REG_XMM0", XedBlot.MakeEquality(XedTestData.OutRegField, 1));
			AssertParseEqual("OUTREG=@", XedBlot.MakeEquality(XedTestData.OutRegField, 0));

			AssertParseEqual("MOD!=3", XedBlot.MakeInequality(XedTestData.ModField, 3));
		}

		[TestMethod]
		public void TestParseCallBlot()
		{
			AssertParseEqual("MODRM()", XedBlot.MakeCall("MODRM"));
		}

		private static void AssertParseEqual(string str, XedBlot blot)
			=> Assert.AreEqual(blot, XedBlot.Parse(str, XedTestData.ResolveField).Item1);
	}
}
