using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;

namespace Asmuth.X86
{
	using OEF = OpcodeEncodingFlags;

	[TestClass]
	public sealed class InstructionDecoderTests
	{
		private static readonly IInstructionDecoderLookup lookup;

		static InstructionDecoderTests()
		{
			var table = new OpcodeEncodingTable<string>();
			table.Add(default, 0x90, "nop");
			table.Add(OEF.Map_0F | OEF.ModRM_Present, 0x1F, "nop"); // 3+ byte nop
			table.Add(OEF.Map_0F | OEF.ModRM_Present, 0x45, "cmovne"); // 0x45 should not be interpreted as REX
			table.Add(OEF.HasMainByteReg | OEF.ImmediateSize_8, 0xB0, "mov r8, imm8");
			table.Add(OEF.OperandSize_Word | OEF.RexW_0 | OEF.HasMainByteReg | OEF.ImmediateSize_16, 0xB8, "mov r16, imm16");
			table.Add(OEF.OperandSize_Dword | OEF.RexW_0 | OEF.HasMainByteReg | OEF.ImmediateSize_32, 0xB8, "mov r32, imm32");
			table.Add(OEF.RexW_1 | OEF.HasMainByteReg | OEF.ImmediateSize_64, 0xB8, "mov r64, imm64");
			table.Add(OEF.OperandSize_Dword | OEF.RexW_0 | OEF.ModRM_Present | OEF.ModRM_FixedReg | OEF.ImmediateSize_32, 0xF7, ModRM.Reg_0, "test r/m32, imm32");
			table.Add(OEF.OperandSize_Dword | OEF.RexW_0 | OEF.ModRM_Present | OEF.ModRM_FixedReg, 0xF7, ModRM.Reg_3, "neg r/m32");
			table.Add(OEF.SimdPrefix_66 | OEF.Map_0F | OEF.ModRM_Present, 0x58, "ADDPD xmm,xmm/m");
			table.Add(OEF.XexType_Vex | OEF.VexL_128 | OEF.SimdPrefix_66 | OEF.Map_0F | OEF.ModRM_Present, 0x58, "ADDPD xmm1,xmm2,xmm3/m128");

			lookup = table;
		}

		[TestMethod]
		public void TestNop()
		{
			var instruction = DecodeSingle_32Bits(0x90);
			Assert.AreEqual(XexType.Escapes, instruction.Xex.Type);
			Assert.AreEqual(0x90, instruction.MainOpcodeByte);
		}

		[TestMethod]
		public void TestMultiByteNops()
		{
			var nops = new byte[]
			{
				// Vol 2, Table 4-9. Recommended Multi-Byte Sequence of NOP Instruction
				0x90,
				0x66, 0x90,
				0x0F, 0x1F, 0x00,
				0x0F, 0x1F, 0x40, 0x00,
				0x0F, 0x1F, 0x44, 0x00, 0x00,
				0x66, 0x0F, 0x1F, 0x44, 0x00, 0x00,
				0x0F, 0x1F, 0x80, 0x00, 0x00, 0x00, 0x00,
				0x0F, 0x1F, 0x84, 0x00, 0x00, 0x00, 0x00, 0x00,
				0x66, 0x0F, 0x1F, 0x84, 0x00, 0x00, 0x00, 0x00, 0x00
			};

			var instructions = Decode(CodeSegmentType._32Bits, nops);
			Assert.AreEqual(9, instructions.Length);
			
			for (int i = 0; i < instructions.Length; ++i)
			{
				var instruction = instructions[i];
				int length = instruction.SizeInBytes;

				Assert.AreEqual(i + 1, length);
				Assert.AreEqual(length <= 2 ? (byte)0x90 : (byte)0x1F, instruction.MainOpcodeByte);
				Assert.AreEqual(
					length == 2 || length == 6 || length == 9,
					instruction.LegacyPrefixes.Count == 1);
				Assert.AreEqual(length >= 5 && length != 7, instruction.Sib.HasValue);
				Assert.AreEqual(length <= 3, instruction.DisplacementSize == DisplacementSize.None);
				Assert.AreEqual(length >= 4 && length <= 6, instruction.DisplacementSize == DisplacementSize._8Bits);
				Assert.AreEqual(length >= 7, instruction.DisplacementSize == DisplacementSize._32Bits);
			}
		}

		[TestMethod]
		public void TestSib()
		{
			// F7 /3 NEG 
			AssertModRMSibRoundTrip(
				ModRM.Mod_Indirect | ModRM.Reg_3 | ModRM.RM_Sib,
				Sib.Base_A | Sib.Index_A | Sib.Scale_1);
		}

		private static void AssertModRMSibRoundTrip(ModRM modRM, Sib sib)
		{
			var instruction = DecodeSingle_32Bits(0xF7, (byte)modRM, (byte)sib);
			Assert.AreEqual(modRM, instruction.ModRM);
			Assert.AreEqual(sib, instruction.Sib);
		}

		[TestMethod]
		public void TestDisplacementSizes()
		{
			Assert.AreEqual(DisplacementSize.None, DecodeSingle_32Bits(0x0F, 0x1F, (byte)ModRM.Mod_Direct).DisplacementSize);
			Assert.AreEqual(DisplacementSize._8Bits, DecodeSingle_32Bits(0x0F, 0x1F, (byte)ModRM.Mod_IndirectDisplacement8, 0x00).DisplacementSize);
			Assert.AreEqual(DisplacementSize._16Bits, DecodeSingle_32Bits(0x67, 0x0F, 0x1F, (byte)ModRM.Mod_IndirectLongDisplacement, 0x00, 0x00).DisplacementSize);
			Assert.AreEqual(DisplacementSize._32Bits, DecodeSingle_32Bits(0x0F, 0x1F, (byte)ModRM.Mod_IndirectLongDisplacement, 0x00, 0x00, 0x00, 0x00).DisplacementSize);
			Assert.AreEqual(DisplacementSize._32Bits, DecodeSingle_64Bits(0x0F, 0x1F, (byte)ModRM.Mod_IndirectLongDisplacement, 0x00, 0x00, 0x00, 0x00).DisplacementSize);
		}

		[TestMethod]
		public void TestImmSizes()
		{
			var instructions = new[]
			{
				DecodeSingle_32Bits(0xB0, 0x01), // MOV AL,imm8
				DecodeSingle_16Bits(0xB8, 0x01, 0x00), // MOV AX,imm16
				DecodeSingle_32Bits(0xB8, 0x01, 0x00, 0x00, 0x00), // MOV EAX,imm32
				DecodeSingle_64Bits(0x48, 0xB8, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00) // MOV RAX,imm64
			};

			for (int i = 0; i < instructions.Length; ++i)
			{
				var instruction = instructions[i];
				Assert.AreEqual(1 << i, instruction.ImmediateSizeInBytes);
				Assert.AreEqual((byte)1, instruction.Immediate.GetByte(0));
			}
		}

		[TestMethod]
		public void TestModRMRequiredForImmediateSize()
		{
			// TEST: F7 /0 imm(16|32)
			// NEG: F7 /3
			Assert.AreEqual(4, DecodeSingle_32Bits(0xF7, ModRM_Reg(0), 0x00, 0x01, 0x02, 0x03).ImmediateSizeInBytes);
			Assert.AreEqual(0, DecodeSingle_32Bits(0xF7, ModRM_Reg(3)).ImmediateSizeInBytes);
		}

		[TestMethod]
		public void TestRexAfter0F()
		{
			var instruction = DecodeSingle_64Bits(0x0F, 0x45, 0xC8); // cmovne ecx, eax
			Assert.AreEqual(OpcodeMap.Escape0F, instruction.OpcodeMap);
			Assert.AreEqual((byte)0x45, instruction.MainOpcodeByte);
			Assert.IsTrue(instruction.ModRM.HasValue);
		}

		[TestMethod]
		public void TestVexIA32()
		{
			// In IA32 mode, VEX prefixes can be ambiguous with other instructions
			// See intel reference vol 2A 2.3.5
			throw new NotImplementedException();
		}

		[TestMethod]
		public void TestAddpd()
		{
			// ADDPD xmm1, xmm2/m128 - 66 0F 58 /r
			var modRM = ModRMEnum.FromComponents(3, 1, 2);
			var instruction = DecodeSingle_32Bits(0x66, 0x0F, 0x58, (byte)modRM);
			Assert.AreEqual(SimdPrefix._66, instruction.PotentialSimdPrefix);
			Assert.AreEqual(XexType.Escapes, instruction.Xex.Type);
			Assert.AreEqual(OpcodeMap.Escape0F, instruction.OpcodeMap);
			Assert.AreEqual(0x58, (byte)instruction.MainOpcodeByte);
			Assert.AreEqual(modRM, instruction.ModRM);
		}

		[TestMethod]
		public void TestVaddpd()
		{
			var modRM = ModRMEnum.FromComponents(3, 1, 2);
			var vex = Vex3Xop.Header_Vex3
				| Vex3Xop.NoRegExtensions | Vex3Xop.NotNonDestructiveReg_Unused
				| Vex3Xop.SimdPrefix_66 | Vex3Xop.OpcodeMap_0F;
			var instruction = DecodeSingle_32Bits(vex.GetFirstByte(), vex.GetSecondByte(), vex.GetThirdByte(), 0x58, (byte)modRM);
			Assert.AreEqual(XexType.Vex3, instruction.Xex.Type);
			Assert.AreEqual(OperandSize._128, instruction.Xex.VectorSize);
			Assert.AreEqual((byte)0, instruction.Xex.NonDestructiveReg);
			Assert.IsFalse(instruction.Xex.OperandSize64);
			Assert.IsFalse(instruction.Xex.ModRegExtension);
			Assert.IsFalse(instruction.Xex.BaseRegExtension);
			Assert.IsFalse(instruction.Xex.IndexRegExtension);
			Assert.AreEqual(SimdPrefix._66, instruction.PotentialSimdPrefix);
			Assert.AreEqual(OpcodeMap.Escape0F, instruction.OpcodeMap);
			Assert.AreEqual(0x58, instruction.MainOpcodeByte);
			Assert.AreEqual(modRM, instruction.ModRM);
		}
		
		private static byte ModRM_Reg(byte reg) => (byte)ModRMEnum.FromComponents(mod: 3, reg: reg, rm: 0);

		private static Instruction[] Decode(CodeSegmentType codeSegmentType, params byte[] bytes)
		{
			var decoder = new InstructionDecoder(codeSegmentType, lookup);
			var instructions = new List<Instruction>();
			for (int i = 0; i < bytes.Length; ++i)
			{
				if (!decoder.Feed(bytes[i]))
				{
					Assert.AreNotEqual(InstructionDecodingState.Error, decoder.State);
					instructions.Add(decoder.GetInstruction());
					decoder.Reset();
				}
			}
			Assert.AreEqual(InstructionDecodingState.Initial, decoder.State);
			return instructions.ToArray();
		}
		
		private static Instruction DecodeSingle(CodeSegmentType codeSegmentType, params byte[] bytes)
		{
			var instructions = Decode(codeSegmentType, bytes);
			Assert.AreEqual(1, instructions.Length);
			return instructions[0];
		}

		private static Instruction DecodeSingle_16Bits(params byte[] bytes)
			=> DecodeSingle(CodeSegmentType._16Bits, bytes);

		private static Instruction DecodeSingle_32Bits(params byte[] bytes)
			=> DecodeSingle(CodeSegmentType._32Bits, bytes);

		private static Instruction DecodeSingle_64Bits(params byte[] bytes)
			=> DecodeSingle(CodeSegmentType._64Bits, bytes);
	}
}
