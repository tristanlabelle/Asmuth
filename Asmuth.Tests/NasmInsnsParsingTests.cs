using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Nasm
{
	[TestClass]
	public sealed class NasmInsnsParsingTests
	{
		[TestMethod]
		public void TestPseudo()
		{
			var entry = NasmInsns.ParseLine("DB		ignore				ignore						ignore");

			Assert.AreEqual("DB", entry.Mnemonic);
			Assert.AreEqual(0, entry.Operands.Count);
			Assert.AreEqual(0, entry.EncodingTokens.Count);
			Assert.AreEqual(0, entry.Flags.Count);
			Assert.IsTrue(entry.IsPseudo);
		}

		[TestMethod]
		public void TestSimple()
		{
			var entry = NasmInsns.ParseLine("LAHF		void				[	9f]					8086");

			Assert.AreEqual("LAHF", entry.Mnemonic);
			Assert.AreEqual(0, entry.Operands.Count);

			CollectionAssert.AreEqual(new[]
			{
				new NasmEncodingToken(NasmEncodingTokenType.Byte, 0x9F)
			}, entry.EncodingTokens.ToArray());
		}

		[TestMethod]
		public void TestComplex()
		{
			var entry = NasmInsns.ParseLine("PEXTRD		rm32, xmmreg, imm			[mri:	norexw 66 0f 3a 16 /r ib,u]			SSE41");

			Assert.AreEqual("PEXTRD", entry.Mnemonic);

			CollectionAssert.AreEqual(new[]
			{
				new NasmOperand(OperandFields.BaseReg, NasmOperandType.RM32),
				new NasmOperand(OperandFields.ModReg, NasmOperandType.XmmReg),
				new NasmOperand(OperandFields.Immediate, NasmOperandType.Imm),
			}, entry.Operands.ToArray());

			CollectionAssert.AreEqual(new[]
			{
				NasmEncodingTokenType.Rex_NoW,
				new NasmEncodingToken(NasmEncodingTokenType.Byte, 0x66),
				new NasmEncodingToken(NasmEncodingTokenType.Byte, 0x0F),
				new NasmEncodingToken(NasmEncodingTokenType.Byte, 0x3A),
				new NasmEncodingToken(NasmEncodingTokenType.Byte, 0x16),
				NasmEncodingTokenType.ModRM,
				NasmEncodingTokenType.Immediate_Byte_Unsigned,
			}, entry.EncodingTokens.ToArray());
		}
	}
}
