using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;

namespace Asmuth.X86
{
	[TestClass]
	public sealed class InstructionDecoderTests
	{
		private sealed class InstructionLookup : IInstructionDecoderLookup
		{
			private static readonly object found = new object();
			public static readonly InstructionLookup Instance = new InstructionLookup();

			public object TryLookup(CodeSegmentType codeSegmentType,
				ImmutableLegacyPrefixList legacyPrefixes, Xex xex, byte opcode, byte? modReg,
				out bool hasModRM, out int immediateSizeInBytes)
			{
				if (xex.OpcodeMap == OpcodeMap.Default && opcode == 0x90)
				{
					// NOP
					hasModRM = false;
					immediateSizeInBytes = 0;
					return found;
				}
				else if (xex.OpcodeMap == OpcodeMap.Escape0F && opcode == 0x1F)
				{
					// 3+ byte nop form
					hasModRM = true;
					immediateSizeInBytes = 0;
					return found;
				}
				else if (xex.OpcodeMap == OpcodeMap.Escape0F && opcode == 0x45)
				{
					// 0F 45 /r = cmovne ecx, eax
					// 0x45 should not be interpreted as REX
					hasModRM = true;
					immediateSizeInBytes = 0;
					return found;
				}
				else if (xex.OpcodeMap == OpcodeMap.Default && (opcode & 0xF8) == 0xB0)
				{
					// MOV x8, imm8
					hasModRM = false;
					immediateSizeInBytes = 1;
					return found;
				}
				else if (xex.OpcodeMap == OpcodeMap.Default && (opcode & 0xF8) == 0xB8)
				{
					// Mov r(16|32|64), imm(16|32|64)
					hasModRM = false;
					immediateSizeInBytes = codeSegmentType.GetIntegerOperandSize(
						@override: legacyPrefixes.HasAddressSizeOverride,
						rexW: xex.OperandSize64).InBytes();
					return found;
				}
				else if (xex.OpcodeMap == OpcodeMap.Default && opcode == 0xF7)
				{
					// TEST: F7 /0 imm(16|32)
					// MUL: F7 /4
					hasModRM = true;

					if (!modReg.HasValue)
					{
						// We need the ModRM byte to report the immediate size
						immediateSizeInBytes = -1;
						return null;
					}

					if (modReg == 0)
					{
						immediateSizeInBytes = codeSegmentType.GetWordOrDwordImmediateSize(
							legacyPrefixes, xex).InBytes();
					}
					else if (modReg == 4)
					{
						immediateSizeInBytes = 0;
					}
					else
					{
						throw new NotImplementedException();
					}

					return found;
				}
				else if (xex.OpcodeMap == OpcodeMap.Escape0F
					&& (xex.SimdPrefix ?? legacyPrefixes.GetSimdPrefix(xex.OpcodeMap)) == SimdPrefix._66
					&& opcode == 0x58)
				{
					hasModRM = true;
					immediateSizeInBytes = 0;
					return found;
				}
				else
				{
					hasModRM = false;
					immediateSizeInBytes = 0;
					return null;
				}
			}
		}

		[TestMethod]
		public void TestNop()
		{
			var instruction = DecodeSingle_32Bits(0x90);
			Assert.AreEqual(XexType.Escapes, instruction.Xex.Type);
			Assert.AreEqual(0x90, instruction.MainByte);
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
				Assert.AreEqual(length <= 2 ? (byte)0x90 : (byte)0x1F, instruction.MainByte);
				Assert.AreEqual(
					length == 2 || length == 6 || length == 9,
					instruction.LegacyPrefixes.Count == 1);
				Assert.AreEqual(length >= 5 && length != 7, instruction.Sib.HasValue);
				Assert.AreEqual(length <= 3, instruction.DisplacementSize == DisplacementSize._0);
				Assert.AreEqual(length >= 4 && length <= 6, instruction.DisplacementSize == DisplacementSize._8);
				Assert.AreEqual(length >= 7, instruction.DisplacementSize == DisplacementSize._32);
			}
		}

		[TestMethod]
		public void TestDisplacementSizes()
		{
			Assert.AreEqual(DisplacementSize._0, DecodeSingle_32Bits(0x0F, 0x1F, (byte)ModRM.Mod_Direct).DisplacementSize);
			Assert.AreEqual(DisplacementSize._8, DecodeSingle_32Bits(0x0F, 0x1F, (byte)ModRM.Mod_IndirectDisplacement8, 0x00).DisplacementSize);
			Assert.AreEqual(DisplacementSize._16, DecodeSingle_32Bits(0x67, 0x0F, 0x1F, (byte)ModRM.Mod_IndirectLongDisplacement, 0x00, 0x00).DisplacementSize);
			Assert.AreEqual(DisplacementSize._32, DecodeSingle_32Bits(0x0F, 0x1F, (byte)ModRM.Mod_IndirectLongDisplacement, 0x00, 0x00, 0x00, 0x00).DisplacementSize);
			Assert.AreEqual(DisplacementSize._32, DecodeSingle_64Bits(0x0F, 0x1F, (byte)ModRM.Mod_IndirectLongDisplacement, 0x00, 0x00, 0x00, 0x00).DisplacementSize);
		}

		[TestMethod]
		public void TestMovImm()
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
			// MUL: F7 /4
			Assert.AreEqual(4, DecodeSingle_32Bits(0xF7, ModRM_Reg(0), 0x00, 0x01, 0x02, 0x03).ImmediateSizeInBytes);
			Assert.AreEqual(0, DecodeSingle_32Bits(0xF7, ModRM_Reg(4)).ImmediateSizeInBytes);
		}

		[TestMethod]
		public void TestRexAfter0F()
		{
			var instruction = DecodeSingle_64Bits(0x0F, 0x45, 0xC8); // cmovne ecx, eax
			Assert.AreEqual(OpcodeMap.Escape0F, instruction.OpcodeMap);
			Assert.AreEqual((byte)0x45, instruction.MainByte);
			Assert.IsTrue(instruction.ModRM.HasValue);
		}

		[TestMethod]
		public void TestVex2Nop()
		{
			var vex = Vex2.Reserved_Value;
			var instruction = DecodeSingle_32Bits(vex.GetFirstByte(), vex.GetSecondByte(), 0x90);
			Assert.AreEqual(XexType.Vex2, instruction.Xex.Type);
			Assert.AreEqual(0x90, instruction.MainByte);
		}

		[TestMethod]
		public void TestAddpd()
		{
			// ADDPD xmm1, xmm2/m128 - 66 0F 58 /r
			var modRM = ModRMEnum.FromComponents(3, 1, 2);
			var instruction = DecodeSingle_32Bits(0x66, 0x0F, 0x58, (byte)modRM);
			Assert.AreEqual(SimdPrefix._66, instruction.SimdPrefix);
			Assert.AreEqual(XexType.Escapes, instruction.Xex.Type);
			Assert.AreEqual(OpcodeMap.Escape0F, instruction.OpcodeMap);
			Assert.AreEqual(0x58, instruction.MainByte);
			Assert.AreEqual(modRM, instruction.ModRM);
		}

		[TestMethod]
		public void TestVaddpd()
		{
			// VADDPD xmm1, xmm2, xmm3/m128 - VEX.NDS.128.66.0F.WIG 58 /r
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
			Assert.AreEqual(SimdPrefix._66, instruction.SimdPrefix);
			Assert.AreEqual(OpcodeMap.Escape0F, instruction.OpcodeMap);
			Assert.AreEqual(0x58, instruction.MainByte);
			Assert.AreEqual(modRM, instruction.ModRM);
		}
		
		private static byte ModRM_Reg(byte reg) => (byte)ModRMEnum.FromComponents(mod: 3, reg: reg, rm: 0);

		private static Instruction[] Decode(CodeSegmentType codeSegmentType, params byte[] bytes)
		{
			var decoder = new InstructionDecoder(InstructionLookup.Instance, codeSegmentType);
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
