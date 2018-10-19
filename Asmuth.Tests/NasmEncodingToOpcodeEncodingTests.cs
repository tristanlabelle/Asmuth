using Asmuth.X86;
using Asmuth.X86.Encoding.Nasm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Encoding.Nasm
{
	[TestClass]
	public sealed class NasmEncodingToOpcodeEncodingTests
	{
		[TestMethod]
		public void TestMainByte()
		{
			AssertEncoding("37", new OpcodeEncoding.Builder
			{
				MainByte = 0x37
			}); // AAA

			AssertEncoding("48+r", new OpcodeEncoding.Builder
			{
				MainByte = 0x48,
				AddressingForm = AddressingFormEncoding.MainByteEmbeddedRegister
			}); // DEC reg
		}

		[TestMethod]
		public void TestModRM()
		{
			AssertEncoding("00 /r", new OpcodeEncoding.Builder
			{
				MainByte = 0x00,
				AddressingForm = ModRMEncoding.Any
			}); // ADD r/m8, r8

			AssertEncoding("f6 /3", NasmOperandType.RM8, new OpcodeEncoding.Builder
			{
				MainByte = 0xF6,
				AddressingForm = new ModRMEncoding(ModRMModEncoding.RegisterOrMemory, reg: 3)
			}); // NEG r/m8

			AssertEncoding("d8 /0", NasmOperandType.Mem32, new OpcodeEncoding.Builder
			{
				MainByte = 0xD8,
				AddressingForm = new ModRMEncoding(ModRMModEncoding.Memory, 0)
			}); // FADD m32

			AssertEncoding("d8 c0+r", new OpcodeEncoding.Builder
			{
				MainByte = 0xD8,
				AddressingForm = new ModRMEncoding(ModRMModEncoding.Register, reg: 0)
			}); // FADD

			AssertEncoding("d9 f2", new OpcodeEncoding.Builder
			{
				MainByte = 0xD9,
				AddressingForm = ModRMEncoding.FromFixedValue(0xF2)
			}); // FPTAN
		}

		[TestMethod]
		public void TestImm()
		{
			AssertEncoding("04 ib", new OpcodeEncoding.Builder
			{
				MainByte = 0x04,
				ImmediateSize = ImmediateSizeEncoding.Byte
			}); // ADD reg_al, imm

			AssertEncoding("05 iw", new OpcodeEncoding.Builder
			{
				MainByte = 0x05,
				ImmediateSize = ImmediateSizeEncoding.Word
			}); // ADD reg_ax, imm

			AssertEncoding("05 id", new OpcodeEncoding.Builder
			{
				MainByte = 0x05,
				ImmediateSize = ImmediateSizeEncoding.Dword
			}); // ADD reg_eax, imm
		}

		[TestMethod]
		public void TestFixedModRMAndImm()
		{
			AssertEncoding("d5 0a", new OpcodeEncoding.Builder
			{
				MainByte = 0xD5,
				ImmediateSize = ImmediateSizeEncoding.Byte,
				Imm8Ext = 0x0A
			}); // AAD

			AssertEncoding("dd d1", new OpcodeEncoding.Builder
			{
				MainByte = 0xDD,
				AddressingForm = ModRMEncoding.FromFixedValue(0xD1)
			}); // FST
		}

		[TestMethod]
		public void TestEscapes()
		{
			AssertEncoding("0f a2", new OpcodeEncoding.Builder
			{
				Map = OpcodeMap.Escape0F,
				MainByte = 0xA2
			}); // CPUID

			AssertEncoding("0f 38 c9 /r", new OpcodeEncoding.Builder
			{
				Map = OpcodeMap.Escape0F38,
				MainByte = 0xC9,
				AddressingForm = ModRMEncoding.Any
			}); // SHA1MSG1

			AssertEncoding("0f 3a cc /r ib", new OpcodeEncoding.Builder
			{
				Map = OpcodeMap.Escape0F3A,
				MainByte = 0xCC,
				AddressingForm = ModRMEncoding.Any,
				ImmediateSize = ImmediateSizeEncoding.Byte
			}); // SHA1RNDS4
		}

		[TestMethod]
		public void TestSimdPrefixes()
		{
			AssertEncoding("np 0f 10 /r", new OpcodeEncoding.Builder
			{
				SimdPrefix = SimdPrefix.None,
				Map = OpcodeMap.Escape0F,
				MainByte = 0x10,
				AddressingForm = ModRMEncoding.Any
			}); // MOVUPS

			AssertEncoding("66 0f 10 /r", new OpcodeEncoding.Builder
			{
				SimdPrefix = SimdPrefix._66,
				Map = OpcodeMap.Escape0F,
				MainByte = 0x10,
				AddressingForm = ModRMEncoding.Any
			}); // MOVUPD

			AssertEncoding("f2 0f 10 /r", new OpcodeEncoding.Builder
			{
				SimdPrefix = SimdPrefix._F2,
				Map = OpcodeMap.Escape0F,
				MainByte = 0x10,
				AddressingForm = ModRMEncoding.Any
			}); // MOVSD

			AssertEncoding("f3 0f 10 /r", new OpcodeEncoding.Builder
			{
				SimdPrefix = SimdPrefix._F3,
				Map = OpcodeMap.Escape0F,
				MainByte = 0x10,
				AddressingForm = ModRMEncoding.Any
			}); // MOVSS
		}

		[TestMethod]
		public void TestVex()
		{
			// Test with different L, pp, mm and w bits
			AssertEncoding("vex.128.0f 10 /r", new OpcodeEncoding.Builder
			{
				VexType = VexType.Vex,
				VectorSize = AvxVectorSize._128,
				SimdPrefix = SimdPrefix.None,
				Map = OpcodeMap.Escape0F,
				MainByte = 0x10,
				AddressingForm = ModRMEncoding.Any
			}); // VMOVUPS

			AssertEncoding("vex.nds.lig.f2.0f 10 /r", new OpcodeEncoding.Builder
			{
				VexType = VexType.Vex,
				SimdPrefix = SimdPrefix._F2,
				Map = OpcodeMap.Escape0F,
				MainByte = 0x10,
				AddressingForm = ModRMEncoding.Any
			}); // VMOVSS

			AssertEncoding("vex.dds.256.66.0f38.w1 98 /r", new OpcodeEncoding.Builder
			{
				VexType = VexType.Vex,
				VectorSize = AvxVectorSize._256,
				SimdPrefix = SimdPrefix._66,
				OperandSize = OperandSizeEncoding.Promotion,
				Map = OpcodeMap.Escape0F38,
				MainByte = 0x98,
				AddressingForm = ModRMEncoding.Any
			}); // VFMADD132PD

			AssertEncoding("xop.m8.w0.nds.l0.p0 a2 /r /is4", new OpcodeEncoding.Builder
			{
				VexType = VexType.Xop,
				Map = OpcodeMap.Xop8,
				OperandSize = OperandSizeEncoding.NoPromotion,
				VectorSize = AvxVectorSize._128,
				SimdPrefix = SimdPrefix.None,
				MainByte = 0xA2,
				AddressingForm = ModRMEncoding.Any,
				ImmediateSize = ImmediateSizeEncoding.Byte
			}); // VPCMOV
		}

		[TestMethod]
		public void TestVariantCounts()
		{
			var entry = NasmInsns.ParseLine("CMOVcc reg32,reg32 [rm: o32 0f 40+c /r] P6");
			Assert.IsTrue(entry.HasConditionCodeVariants);

			int addressSizeVariantCount, operandSizeVariantCount;
			entry = NasmInsns.ParseLine("MOV reg_eax,mem_offs [-i: o32 a1 iwdq] 386,SM");
			entry.GetAddressAndOperandSizeVariantCounts(
				out addressSizeVariantCount, out operandSizeVariantCount);
			Assert.AreEqual(3, addressSizeVariantCount);
			Assert.AreEqual(1, operandSizeVariantCount);

			entry = NasmInsns.ParseLine("JMP imm:imm [ji: odf ea iwd iw] 8086,NOLONG");
			entry.GetAddressAndOperandSizeVariantCounts(
				out addressSizeVariantCount, out operandSizeVariantCount);
			Assert.AreEqual(1, addressSizeVariantCount);
			Assert.AreEqual(2, operandSizeVariantCount);
		}

		private static void AssertEncoding(string nasmEncodingStr,
			NasmOperandType? rmOperandType, OpcodeEncoding expectedEncoding)
		{
			var nasmEncodingTokens = NasmInsns.ParseEncoding(
				nasmEncodingStr, out VexEncoding? vexEncoding);

			var @params = new NasmInsnsEntry.OpcodeEncodingConversionParams();
			if (rmOperandType.HasValue) @params.SetRMFlagsFromOperandType(rmOperandType.Value);

			var actualEncoding = NasmInsnsEntry.ToOpcodeEncoding(nasmEncodingTokens, vexEncoding, in @params);
			Assert.AreEqual(SetComparisonResult.Equal, OpcodeEncoding.Compare(actualEncoding, expectedEncoding));
		}

		private static void AssertEncoding(string nasmEncodingStr, OpcodeEncoding expectedEncoding)
			=> AssertEncoding(nasmEncodingStr, null, expectedEncoding);
	}
}
