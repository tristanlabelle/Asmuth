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
		public void TestParseField()
		{
			var field = XedDataFiles.ParseField("MOD SCALAR xed_bits_t 2 SUPPRESSED NOPRINT INTERNAL DO EO",
				shortEnumName => throw new KeyNotFoundException());
			Assert.AreEqual("MOD", field.Name);
			Assert.AreEqual(XedBitsFieldType._2, field.Type);
			Assert.AreEqual(XedOperandVisibility.Suppressed, field.DefaultOperandVisibility);
			Assert.AreEqual(false, field.IsPrintable);
			Assert.AreEqual(false, field.IsPublic);
			Assert.AreEqual(XedFieldUsage.Output, field.DecoderUsage);
			Assert.AreEqual(XedFieldUsage.Output, field.EncoderUsage);
		}

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

			var symbols = XedDataFiles.ParsePatterns(new StringReader(str),
				s => s == "norexr" ? "REXR=0" : null, XedTestData.ResolveField).ToArray();
			Assert.AreEqual(2, symbols.Length);

			{
				var prefixesPattern = (XedRulePattern)symbols[0];
				Assert.AreEqual("PREFIXES", prefixesPattern.Name);
				Assert.IsFalse(prefixesPattern.ReturnsRegister);
				Assert.AreEqual(3, prefixesPattern.Cases.Count);

				var rexCase = prefixesPattern.Cases[0];
				Assert.AreEqual(2, rexCase.Conditions.Length);
				Assert.AreEqual(5, rexCase.Actions.Length);
				Assert.AreEqual(XedRulePatternControlFlow.Reset, rexCase.ControlFlow);

				var otherwiseCase = prefixesPattern.Cases[2];
				Assert.AreEqual(0, otherwiseCase.Conditions.Length);
				Assert.AreEqual(0, otherwiseCase.Actions.Length);
			}

			{
				var xmmPattern = (XedRulePattern)symbols[1];
				Assert.AreEqual("XMM_R_64", xmmPattern.Name);
				Assert.IsTrue(xmmPattern.ReturnsRegister);
				Assert.AreEqual(1, xmmPattern.Cases.Count);
				Assert.AreEqual(2, xmmPattern.Cases[0].Conditions.Length);
				Assert.AreEqual(1, xmmPattern.Cases[0].Actions.Length);
				Assert.AreEqual(1, xmmPattern.Cases[0].TryGetOutRegBlot(xmmPattern.IsEncode).Value.Value.Constant);
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
			var resolvers = new XedInstructionStringResolvers
			{
				State = s => s,
				Field = XedTestData.ResolveField,
				OperandWidth = s => default,
				XType = s => default
			};
			var entry = XedDataFiles.ParseInstructions(new StringReader(str), resolvers).Single();
			var instruction = entry.Instruction;
			Assert.AreEqual("INSTRUCTIONS", entry.PatternName);
			Assert.AreEqual("INC", instruction.Class);
			Assert.AreEqual(3, instruction.PrivilegeLevel);
			Assert.AreEqual("BINARY", instruction.Category);
			Assert.AreEqual("BASE", instruction.Extension);
			Assert.AreEqual("I86", instruction.IsaSet);
			Assert.AreEqual("BYTEOP", instruction.Attributes.Single());

			Assert.AreEqual(1, instruction.Flags.Count);
			Assert.AreEqual(5, instruction.Flags[0].FlagActions.Length);

			Assert.AreEqual(1, instruction.Forms.Count);
			var form = instruction.Forms[0];
			Assert.AreEqual(5, form.Pattern.Length);
			Assert.AreEqual(1, form.Operands.Length);
		}
	}
}
