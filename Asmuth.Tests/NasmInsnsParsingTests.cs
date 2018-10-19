using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Encoding.Nasm
{
	[TestClass]
	public sealed class NasmInsnsParsingTests
	{
		[TestMethod]
		public void TestPseudo()
		{
			var entry = NasmInsns.ParseLine("DB ignore ignore ignore");

			Assert.AreEqual("DB", entry.Mnemonic);
			Assert.AreEqual(0, entry.Operands.Count);
			Assert.AreEqual(0, entry.EncodingTokens.Count);
			Assert.AreEqual(0, entry.Flags.Count);
			Assert.IsTrue(entry.IsPseudo);
		}

		[TestMethod]
		public void TestSimple()
		{
			var entry = NasmInsns.ParseLine("LAHF void [9f] 8086");

			Assert.AreEqual("LAHF", entry.Mnemonic);
			Assert.AreEqual(0, entry.Operands.Count);

			CollectionAssert.AreEqual(new[]
			{
				new NasmEncodingToken(NasmEncodingTokenType.Byte, 0x9F)
			}, entry.EncodingTokens.ToArray());
		}

		[TestMethod]
		public void TestEmbeddedRegister()
		{
			var entry = NasmInsns.ParseLine("PUSH reg32 [r: o32 50+r] 386,NOLONG");

			Assert.AreEqual("PUSH", entry.Mnemonic);

			CollectionAssert.AreEqual(new[]
			{
				new NasmOperand(OperandField.ModReg, NasmOperandType.Reg32, NasmOperandFlags.None)
			}, entry.Operands.ToArray());

			CollectionAssert.AreEqual(new[]
			{
				NasmEncodingTokenType.OperandSize_32,
				new NasmEncodingToken(NasmEncodingTokenType.Byte_PlusRegister, 0x50)
			}, entry.EncodingTokens.ToArray());
		}

		[TestMethod]
		public void TestComplex()
		{
			var entry = NasmInsns.ParseLine("PEXTRD rm32,xmmreg,imm [mri: norexw 66 0f 3a 16 /r ib,u] SSE41");

			Assert.AreEqual("PEXTRD", entry.Mnemonic);

			CollectionAssert.AreEqual(new[]
			{
				new NasmOperand(OperandField.BaseReg, NasmOperandType.RM32, NasmOperandFlags.None),
				new NasmOperand(OperandField.ModReg, NasmOperandType.XmmReg, NasmOperandFlags.None),
				new NasmOperand(OperandField.Immediate, NasmOperandType.Imm, NasmOperandFlags.None),
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
