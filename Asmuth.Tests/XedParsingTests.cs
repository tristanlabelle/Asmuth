using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Xed
{
	[TestClass]
	public sealed class XedParsingTests
	{
		[TestMethod]
		public void TestParseXType()
		{
			AssertParseXType("f32 SINGLE 32", "f32", XedBaseType.Single, 32);
			AssertParseXType("i1 INT 1", "i1", XedBaseType.Int, 1);
			AssertParseXType("int INT 0", "int", XedBaseType.Int, 0);
			AssertParseXType("struct STRUCT 0", "struct", XedBaseType.Struct, 0);
			AssertParseXType("var VARIABLE 0", "var", XedBaseType.Variable, 0);
		}

		private void AssertParseXType(string str, string name, XedBaseType type, int elementSizeInBits)
		{
			var result = XedDataFiles.ParseXType(str);
			Assert.AreEqual(name, result.Key);
			Assert.AreEqual(type, result.Value.BaseType);
			Assert.AreEqual(elementSizeInBits, result.Value.BitsPerElement);
		}

		[TestMethod]
		public void TestParseStateMacro()
		{
			AssertParseStateMacro("no_rex REX=0", "no_rex", "REX=0");
			AssertParseStateMacro("reset_rex  REX=0 REXW=0 REXB=0 REXR=0 REXX=0 ",
				"reset_rex", "REX=0 REXW=0 REXB=0 REXR=0 REXX=0");
		}

		private void AssertParseStateMacro(string str, string key, string value)
		{
			var result = XedDataFiles.ParseStateMacro(str);
			Assert.AreEqual(key, result.Key);
			Assert.AreEqual(value, result.Value);
		}

		[TestMethod]
		public void TestParseRegister()
		{
			var result = XedDataFiles.ParseRegister("AH  gpr  8   RAX/EAX 4  h");
			Assert.AreEqual("AH", result.Name);
			Assert.AreEqual("gpr", result.Class);
			Assert.AreEqual(8, result.WidthInBits_IA32);
			Assert.AreEqual("RAX", result.MaxEnclosingRegName_X64);
			Assert.AreEqual("EAX", result.MaxEnclosingRegName_IA32);
			Assert.AreEqual(4, result.ID);
			Assert.IsTrue(result.IsHighByte);

			Assert.AreEqual(null, XedDataFiles.ParseRegister("FLAGS   flags 16 RFLAGS/EFLAGS").ID);

			XedDataFiles.ParseRegister("ST0   x87 80  ST0  0 - st(0)");

			result = XedDataFiles.ParseRegister("CR0 cr 32/64  CR0  0");
			Assert.AreEqual(32, result.WidthInBits_IA32);
			Assert.AreEqual(64, result.WidthInBits_X64);

			result = XedDataFiles.ParseRegister("X87PUSH     pseudox87 NA");
			Assert.AreEqual("X87PUSH", result.MaxEnclosingRegName_IA32);
			Assert.AreEqual(0, result.WidthInBits_IA32);
		}

		[TestMethod]
		public void TestParseRegisterTable_MaxEnclosingRegisterYmmToZmm()
		{
			var str = @"XMM0  xmm  128 YMM0  0
				XMM0  xmm  128 ZMM0  0";
			var table = XedDataFiles.ParseRegisterTable(new StringReader(str));
			Assert.AreEqual(1, table.Count);
			Assert.AreEqual("XMM0", table.ByIndex[0].Name);
			Assert.AreEqual("ZMM0", table.ByIndex[0].MaxEnclosingRegName_IA32);
		}

		[TestMethod]
		public void TestParseOperandWidth()
		{
			var xtypeDict = new Dictionary<string, XedXType>
			{
				{ "int", new XedXType(XedBaseType.Int, 0) },
				{ "i32", new XedXType(XedBaseType.Int, 32) },
				{ "f64", new XedXType(XedBaseType.Double, 64) },
			};
			XedXType xtypeLookup(string name) => xtypeDict[name];

			AssertParseOperandWidth("i3 int 3bits", xtypeLookup, "i3", XedBaseType.Int, 3);
			AssertParseOperandWidth("asz int 2 4 8", xtypeLookup, "asz", XedBaseType.Int, 16, 32, 64);
			AssertParseOperandWidth("pd f64 16", xtypeLookup, "pd", XedBaseType.Double, 128);
		}

		private void AssertParseOperandWidth(string str, Func<string, XedXType> xtypeLookup,
			string name, XedBaseType type, int widthInBits)
			=> AssertParseOperandWidth(str, xtypeLookup, name, type, widthInBits, widthInBits, widthInBits);

		private void AssertParseOperandWidth(string str, Func<string, XedXType> xtypeLookup,
			string name, XedBaseType type, int widthInBits_16, int widthInBits_32, int widthInBits_64)
		{
			var result = XedDataFiles.ParseOperandWidth(str, xtypeLookup);
			Assert.AreEqual(name, result.Key);
			Assert.AreEqual(type, result.Value.BaseType);
			Assert.AreEqual(widthInBits_16, result.Value.WidthInBits_16);
			Assert.AreEqual(widthInBits_32, result.Value.WidthInBits_32);
			Assert.AreEqual(widthInBits_64, result.Value.WidthInBits_64);
		}

		[TestMethod]
		public void TestParseBlot()
		{
			AssertParseEqual("0x42", new XedBitsBlot(0x42, 8));
			AssertParseEqual("0b0100", new XedBitsBlot(0b0100, 4));
			AssertParseEqual("wrxb", new XedBitsBlot("wrxb"));
			AssertParseEqual("1_ddd", new XedBitsBlot("1ddd"));
			AssertParseEqual("MOD[0b11]", new XedBitsBlot("MOD", 0b11, 2));
			AssertParseEqual("MOD[mm]", new XedBitsBlot("MOD", "mm"));
			AssertParseEqual("UIMM0[ssss_uuuu]", new XedBitsBlot("UIMM0", "ssssuuuu"));
			AssertParseEqual("UIMM0[i/16]", new XedBitsBlot("UIMM0", new string('i', 16)));
			// TODO: "REXW[w]=1"

			AssertParseEqual("MOD=3", predicate: false, new XedAssignmentBlot("MOD", 3));
			AssertParseEqual("BASE0=ArAX()", XedAssignmentBlot.Call("BASE0", "ArAX"));
			AssertParseEqual("REXW=w", XedAssignmentBlot.BitPattern("REXW", "w"));
			AssertParseEqual("OUTREG=XED_REG_XMM0",
				XedAssignmentBlot.NamedConstant("OUTREG", "XED_REG_XMM0"));

			AssertParseEqual("MOD=3", predicate: true, XedPredicateBlot.Equal("MOD", 3));
			AssertParseEqual("MOD!=3", XedPredicateBlot.NotEqual("MOD", 3));

			AssertParseEqual("MODRM()", XedBlot.Call("MODRM"));
		}

		private static void AssertParseEqual(string str, bool predicate, XedBlot blot)
			=> Assert.AreEqual(blot, XedBlot.Parse(str, predicate));

		private static void AssertParseEqual(string str, XedBlot blot)
			=> AssertParseEqual(str, predicate: false, blot);

		[TestMethod]
		public void TestParsePatternRules()
		{
			var str = @"
				# Hey, nice rule
				PREFIXES()::
				0b0100 wrxb | XED_RESET REX=1 REXW=w REXR=r REXX=x REXB=b
				0x66 | XED_RESET PREFIX66=1
				otherwise | nothing

				xed_reg_enum_t XMM_R_64()::
				norexr REG=0x0 | OUTREG=XED_REG_XMM0";

			var symbols = XedDataFiles.ParsePatternsFile(new StringReader(str),
				s => s == "norexr" ? "REXR=0" : null).ToArray();
			Assert.AreEqual(2, symbols.Length);

			{
				var prefixesPattern = (XedPattern)symbols[0];
				Assert.AreEqual("PREFIXES", prefixesPattern.Name);
				Assert.IsFalse(prefixesPattern.ReturnsRegister);
				Assert.AreEqual(3, prefixesPattern.Cases.Length);

				var rexCase = prefixesPattern.Cases[0];
				Assert.AreEqual(2, rexCase.Conditions.Length);
				Assert.AreEqual(5, rexCase.Actions.Length);
				Assert.IsTrue(rexCase.Reset);

				var otherwiseCase = prefixesPattern.Cases[2];
				Assert.AreEqual(0, otherwiseCase.Conditions.Length);
				Assert.AreEqual(0, otherwiseCase.Actions.Length);
			}

			{
				var xmmPattern = (XedPattern)symbols[1];
				Assert.AreEqual("XMM_R_64", xmmPattern.Name);
				Assert.IsTrue(xmmPattern.ReturnsRegister);
				Assert.AreEqual(1, xmmPattern.Cases.Length);
				Assert.AreEqual(2, xmmPattern.Cases[0].Conditions.Length);
				Assert.AreEqual(1, xmmPattern.Cases[0].Actions.Length);
				Assert.AreEqual("XED_REG_XMM0", xmmPattern.Cases[0].OutRegBlot.Value.ConstantName);
			}
		}

		[TestMethod]
		public void TestParseInstructions()
		{
			var str = @"
				INSTRUCTIONS()::
				{
					ICLASS    : INC
					CPL       : 3
					CATEGORY  : BINARY
					EXTENSION : BASE
					ISA_SET   : I86
					ATTRIBUTES : BYTEOP
					FLAGS     : MUST [ of-mod sf-mod zf-mod af-mod pf-mod ]
					PATTERN   : 0xFE MOD[0b11] MOD=3 REG[0b000] RM[nnn]
					OPERANDS  : REG0=GPR8_B():rw
				}";
			var entry = XedDataFiles.ParseInstructions(new StringReader(str),
				s => null, s => default).Single();
			var instruction = entry.Value;
			Assert.AreEqual("INSTRUCTIONS", entry.Key);
			Assert.AreEqual("INC", instruction.Class);
			Assert.AreEqual(3, instruction.PrivilegeLevel);
			Assert.AreEqual("BINARY", instruction.Category);
			Assert.AreEqual("BASE", instruction.Extension);
			Assert.AreEqual("I86", instruction.IsaSet);
			Assert.AreEqual("BYTEOP", instruction.Attributes.Single());

			Assert.AreEqual(1, instruction.FlagsRecords.Count);
			Assert.AreEqual(5, instruction.FlagsRecords[0].FlagActions.Count);

			Assert.AreEqual(1, instruction.Forms.Count);
			var form = instruction.Forms[0];
			Assert.AreEqual(5, form.Pattern.Length);
			Assert.AreEqual(1, form.Operands.Length);
		}
	}
}
