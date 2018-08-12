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
		[TestMethod]
		public void TestNop()
		{
			var table = new OpcodeEncodingTable<string>();
			table.Add(default, 0x90, "nop");

			var instruction = DecodeSingle_32Bits(table, 0x90);
			Assert.AreEqual(XexType.Escapes, instruction.Xex.Type);
			Assert.AreEqual(0x90, instruction.MainOpcodeByte);
		}

		[TestMethod]
		public void TestMultiByteNops()
		{
			var table = new OpcodeEncodingTable<string>();
			table.Add(default, 0x90, "nop");
			table.Add(OEF.Map_0F | OEF.ModRM_Present, 0x1F, "nop"); // 3+ byte nop

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

			var instructions = Decode(table, CodeSegmentType._32Bits, nops);
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
			AssertModRMSibRoundTrip(
				ModRM.Mod_Indirect | ModRM.Reg_3 | ModRM.RM_Sib,
				Sib.Base_A | Sib.Index_A | Sib.Scale_1);
		}

		private static void AssertModRMSibRoundTrip(ModRM modRM, Sib sib)
		{
			var table = new OpcodeEncodingTable<string>();
			table.Add(OEF.OperandSize_Dword | OEF.RexW_0 | OEF.ModRM_Present | OEF.ModRM_FixedReg, 0xF7, ModRM.Reg_3, "neg r/m32");

			var instruction = DecodeSingle_32Bits(table, 0xF7, (byte)modRM, (byte)sib);
			Assert.AreEqual(modRM, instruction.ModRM);
			Assert.AreEqual(sib, instruction.Sib);
		}

		[TestMethod]
		public void TestModRMFixedIndirectAmbiguity()
		{
			var table = new OpcodeEncodingTable<string>();
			table.Add(OEF.ModRM_Present | OEF.ModRM_FixedReg | OEF.ModRM_RM_Indirect, 0xD9, ModRM.Reg_2, "fst");
			table.Add(OEF.ModRM_Present | OEF.ModRM_FixedReg | OEF.ModRM_RM_Fixed, 0xD9, (ModRM)0xD0, "fnop");

			Assert.AreEqual("fst", DecodeSingleForTag_32Bits(table, 0xD9, 0b00_010_000)); // Indirect eax /2
			Assert.AreEqual("fnop", DecodeSingleForTag_32Bits(table, 0xD9, 0b11_010_000)); // Direct eax /2
		}

		[TestMethod]
		public void TestDisplacementSizes()
		{
			var table = new OpcodeEncodingTable<string>();
			table.Add(OEF.Map_0F | OEF.ModRM_Present, 0x1F, "nop");

			// TODO: Test 16 bits too
			Assert.AreEqual(DisplacementSize.None, DecodeSingle_32Bits(table, 0x0F, 0x1F, (byte)ModRM.Mod_Direct).DisplacementSize);
			Assert.AreEqual(DisplacementSize._8Bits, DecodeSingle_32Bits(table, 0x0F, 0x1F, (byte)ModRM.Mod_IndirectDisplacement8, 0x00).DisplacementSize);
			Assert.AreEqual(DisplacementSize._16Bits, DecodeSingle_32Bits(table, 0x67, 0x0F, 0x1F, (byte)ModRM.Mod_IndirectLongDisplacement, 0x00, 0x00).DisplacementSize);
			Assert.AreEqual(DisplacementSize._32Bits, DecodeSingle_32Bits(table, 0x0F, 0x1F, (byte)ModRM.Mod_IndirectLongDisplacement, 0x00, 0x00, 0x00, 0x00).DisplacementSize);
			Assert.AreEqual(DisplacementSize._32Bits, DecodeSingle_64Bits(table, 0x0F, 0x1F, (byte)ModRM.Mod_IndirectLongDisplacement, 0x00, 0x00, 0x00, 0x00).DisplacementSize);
		}

		[TestMethod]
		public void TestImmSizes()
		{
			var table = new OpcodeEncodingTable<string>();
			table.Add(OEF.HasMainByteReg | OEF.ImmediateSize_8, 0xB0, "mov r8, imm8");
			table.Add(OEF.OperandSize_Word | OEF.RexW_0 | OEF.HasMainByteReg | OEF.ImmediateSize_16, 0xB8, "mov r16, imm16");
			table.Add(OEF.OperandSize_Dword | OEF.RexW_0 | OEF.HasMainByteReg | OEF.ImmediateSize_32, 0xB8, "mov r32, imm32");
			table.Add(OEF.RexW_1 | OEF.HasMainByteReg | OEF.ImmediateSize_64, 0xB8, "mov r64, imm64");

			var instructions = new[]
			{
				DecodeSingle_32Bits(table, 0xB0, 0x01), // MOV AL,imm8
				DecodeSingle_16Bits(table, 0xB8, 0x01, 0x00), // MOV AX,imm16
				DecodeSingle_32Bits(table, 0xB8, 0x01, 0x00, 0x00, 0x00), // MOV EAX,imm32
				DecodeSingle_64Bits(table, 0x48, 0xB8, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00) // MOV RAX,imm64
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
			var table = new OpcodeEncodingTable<string>();
			var commonFlags = OEF.OperandSize_Dword | OEF.RexW_0 | OEF.ModRM_Present | OEF.ModRM_FixedReg;
			table.Add(commonFlags | OEF.ImmediateSize_32, 0xF7, ModRM.Reg_0, "test r/m32, imm32");
			table.Add(commonFlags, 0xF7, ModRM.Reg_3, "neg r/m32");
			
			Assert.AreEqual(4, DecodeSingle_32Bits(table, 0xF7, ModRM_Reg(0), 0x00, 0x01, 0x02, 0x03).ImmediateSizeInBytes);
			Assert.AreEqual(0, DecodeSingle_32Bits(table, 0xF7, ModRM_Reg(3)).ImmediateSizeInBytes);
		}

		[TestMethod]
		public void TestRexAfter0F()
		{
			var table = new OpcodeEncodingTable<string>();
			table.Add(OEF.Map_0F | OEF.ModRM_Present, 0x45, "cmovne"); // 0x45 should not be interpreted as REX

			var instruction = DecodeSingle_64Bits(table, 0x0F, 0x45, 0xC8); // cmovne ecx, eax
			Assert.AreEqual(OpcodeMap.Escape0F, instruction.OpcodeMap);
			Assert.AreEqual((byte)0x45, instruction.MainOpcodeByte);
			Assert.IsTrue(instruction.ModRM.HasValue);
		}

		[TestMethod]
		public void TestRexIncDecAmbiguity()
		{
			var table = new OpcodeEncodingTable<string>();
			table.Add(OEF.HasMainByteReg, 0x40, "inc r16/32");
			table.Add(OEF.HasMainByteReg, 0x48, "dec r16/32");
			table.Add(OEF.ModRM_Present, 01, "add rm, r");
			
			Assert.AreEqual(0x42, DecodeSingle_32Bits(table, 0x42).MainOpcodeByte);
			Assert.AreEqual(0x4C, DecodeSingle_32Bits(table, 0x4C).MainOpcodeByte);
			Assert.AreEqual(0x01, DecodeSingle_64Bits(table, 0x42, 0x01, 0x00).MainOpcodeByte);
			Assert.AreEqual(0x01, DecodeSingle_64Bits(table, 0x4C, 0x01, 0x00).MainOpcodeByte);
		}

		[TestMethod]
		public void TestVexLesAmbiguity()
		{
			// In IA32 mode, VEX prefixes can be ambiguous with other instructions
			// See intel reference vol 2A 2.3.5
			var table = new OpcodeEncodingTable<string>();
			table.Add(OEF.ModRM_Present | OEF.ModRM_RM_Indirect, 0xC4, "les m/r");
			table.Add(OEF.ModRM_Present | OEF.ModRM_RM_Indirect, 0xC5, "lds m/r");
			table.Add(OEF.XexType_Vex | OEF.VexL_128 | OEF.SimdPrefix_F3 | OEF.Map_0F | OEF.ModRM_Present, 0xE6, "VCVTDQ2PD xmm1, xmm2/m64");
			
			Assert.AreEqual(0xC4, DecodeSingle_32Bits(table, 0xC4, 0x01).MainOpcodeByte);
			Assert.AreEqual(0xC5, DecodeSingle_32Bits(table, 0xC5, 0x01).MainOpcodeByte);
			Assert.AreEqual(0xE6, DecodeSingle_32Bits(table, 0xC4, 0xC1, 0x7A, 0xE6, 0x00).MainOpcodeByte);
			Assert.AreEqual(0xE6, DecodeSingle_64Bits(table, 0xC4, 0x01, 0x7A, 0xE6, 0x00).MainOpcodeByte);
		}

		[TestMethod]
		public void TestAddpd()
		{
			var table = new OpcodeEncodingTable<string>();
			table.Add(OEF.SimdPrefix_66 | OEF.Map_0F | OEF.ModRM_Present, 0x58, "ADDPD xmm1,xmm2/m128");
			
			var modRM = ModRMEnum.FromComponents(3, 1, 2);
			var instruction = DecodeSingle_32Bits(table, 0x66, 0x0F, 0x58, (byte)modRM);
			Assert.AreEqual(SimdPrefix._66, instruction.PotentialSimdPrefix);
			Assert.AreEqual(XexType.Escapes, instruction.Xex.Type);
			Assert.AreEqual(OpcodeMap.Escape0F, instruction.OpcodeMap);
			Assert.AreEqual(0x58, instruction.MainOpcodeByte);
			Assert.AreEqual(modRM, instruction.ModRM);
		}

		[TestMethod]
		public void TestVaddpd()
		{
			var table = new OpcodeEncodingTable<string>();
			table.Add(OEF.XexType_Vex | OEF.VexL_128 | OEF.SimdPrefix_66 | OEF.Map_0F | OEF.ModRM_Present, 0x58, "ADDPD xmm1,xmm2,xmm3/m128");

			var modRM = ModRMEnum.FromComponents(3, 1, 2);
			var vex = Vex3Xop.Header_Vex3
				| Vex3Xop.NoRegExtensions | Vex3Xop.NotNonDestructiveReg_Unused
				| Vex3Xop.SimdPrefix_66 | Vex3Xop.OpcodeMap_0F;
			var instruction = DecodeSingle_32Bits(table, vex.GetFirstByte(), vex.GetSecondByte(), vex.GetThirdByte(), 0x58, (byte)modRM);
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
		
		[TestMethod]
		public void TestImm8ExtAmbiguity()
		{
			var table = new OpcodeEncodingTable<string>();
			var flags = OEF.SimdPrefix_None | OEF.Map_0F | OEF.ModRM_Present | OEF.Imm8Ext_Fixed | OEF.ImmediateSize_8;
			table.Add(flags, 0xC2, default(ModRM), 0x00, "cmpeqps");
			table.Add(flags, 0xC2, default(ModRM), 0x01, "cmpltps");

			Assert.AreEqual("cmpeqps", DecodeSingleForTag_32Bits(table, 0x0F, 0xC2, 0x00, 0x00));
			Assert.AreEqual("cmpltps", DecodeSingleForTag_32Bits(table, 0x0F, 0xC2, 0x00, 0x01));
		}

		private static byte ModRM_Reg(byte reg) => (byte)ModRMEnum.FromComponents(mod: 3, reg: reg, rm: 0);

		private static Instruction[] Decode(IInstructionDecoderLookup lookup,
			CodeSegmentType codeSegmentType, params byte[] bytes)
		{
			var decoder = new InstructionDecoder(codeSegmentType, lookup);
			var instructions = new List<Instruction>();
			for (int i = 0; i < bytes.Length; ++i)
			{
				if (!decoder.Consume(bytes[i]))
				{
					Assert.AreNotEqual(InstructionDecodingState.Error, decoder.State);
					instructions.Add(decoder.GetInstruction());
					decoder.Reset();
				}
			}
			Assert.AreEqual(InstructionDecodingState.Initial, decoder.State);
			return instructions.ToArray();
		}
		
		private static Instruction DecodeSingle(IInstructionDecoderLookup lookup,
			CodeSegmentType codeSegmentType, params byte[] bytes)
		{
			var instructions = Decode(lookup, codeSegmentType, bytes);
			Assert.AreEqual(1, instructions.Length);
			return instructions[0];
		}

		private static Instruction DecodeSingle_16Bits(IInstructionDecoderLookup lookup, params byte[] bytes)
			=> DecodeSingle(lookup, CodeSegmentType._16Bits, bytes);

		private static Instruction DecodeSingle_32Bits(IInstructionDecoderLookup lookup, params byte[] bytes)
			=> DecodeSingle(lookup, CodeSegmentType._32Bits, bytes);

		private static Instruction DecodeSingle_64Bits(IInstructionDecoderLookup lookup, params byte[] bytes)
			=> DecodeSingle(lookup, CodeSegmentType._64Bits, bytes);
		
		private static object DecodeSingleForTag(IInstructionDecoderLookup lookup,
			CodeSegmentType codeSegmentType, params byte[] bytes)
		{
			var decoder = new InstructionDecoder(codeSegmentType, lookup);
			for (int i = 0; i < bytes.Length; ++i)
			{
				if (!decoder.Consume(bytes[i]))
				{
					Assert.AreEqual(InstructionDecodingState.Completed, decoder.State);
					Assert.AreEqual(i, bytes.Length - 1);
					return decoder.LookupTag;
				}
			}

			Assert.Fail();
			return null;
		}


		private static object DecodeSingleForTag_32Bits(
			IInstructionDecoderLookup lookup, params byte[] bytes)
			=> DecodeSingleForTag(lookup, CodeSegmentType._32Bits, bytes);
	}
}
