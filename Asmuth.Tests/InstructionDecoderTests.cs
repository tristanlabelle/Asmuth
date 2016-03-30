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

			public object TryLookup(CodeContext mode,
				ImmutableLegacyPrefixList legacyPrefixes, Xex xex, byte opcode,
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
					OperandSize operandSize;
					if (mode == CodeContext.SixtyFourBit)
						operandSize = xex.OperandSize64 ? OperandSize.Qword : OperandSize.Dword;
					else
						operandSize = mode.GetDefaultOperandSize().OverrideWordDword(
							legacyPrefixes.HasOperandSizeOverride);

					hasModRM = false;
					immediateSizeInBytes = operandSize.InBytes();
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
			var instruction = DecodeSingle_Protected32(0x90);
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

			var instructions = Decode_Protected32(nops);
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
			Assert.AreEqual(DisplacementSize._0, DecodeSingle_Protected32(0x0F, 0x1F, (byte)ModRM.Mod_Direct).DisplacementSize);
			Assert.AreEqual(DisplacementSize._8, DecodeSingle_Protected32(0x0F, 0x1F, (byte)ModRM.Mod_IndirectDisplacement8, 0x00).DisplacementSize);
			Assert.AreEqual(DisplacementSize._16, DecodeSingle_Protected32(0x67, 0x0F, 0x1F, (byte)ModRM.Mod_IndirectLongDisplacement, 0x00, 0x00).DisplacementSize);
			Assert.AreEqual(DisplacementSize._32, DecodeSingle_Protected32(0x0F, 0x1F, (byte)ModRM.Mod_IndirectLongDisplacement, 0x00, 0x00, 0x00, 0x00).DisplacementSize);
			Assert.AreEqual(DisplacementSize._32, DecodeSingle_64Bit(0x0F, 0x1F, (byte)ModRM.Mod_IndirectLongDisplacement, 0x00, 0x00, 0x00, 0x00).DisplacementSize);
		}

		[TestMethod]
		public void TestMovImm()
		{
			var instructions = new[]
			{
				DecodeSingle_IA32e(AddressSize._32, 0xB0, 0x01), // MOV AL,imm8
				DecodeSingle_IA32e(AddressSize._16, 0xB8, 0x01, 0x00), // MOV AX,imm16
				DecodeSingle_IA32e(AddressSize._32, 0xB8, 0x01, 0x00, 0x00, 0x00), // MOV EAX,imm32
				DecodeSingle_IA32e(AddressSize._64, 0x48, 0xB8, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00) // MOV RAX,imm64
			};

			for (int i = 0; i < instructions.Length; ++i)
			{
				var instruction = instructions[i];
				Assert.AreEqual(1 << i, instruction.ImmediateSizeInBytes);
				Assert.AreEqual(1UL, instruction.Immediate);
			}
		}

		[TestMethod]
		public void TestVex2Nop()
		{
			var vex = Vex2.Reserved_Value;
			var instruction = DecodeSingle_Protected32(vex.GetFirstByte(), vex.GetSecondByte(), 0x90);
			Assert.AreEqual(XexType.Vex2, instruction.Xex.Type);
			Assert.AreEqual(0x90, instruction.MainByte);
		}

		[TestMethod]
		public void TestAddpd()
		{
			// ADDPD xmm1, xmm2/m128 - 66 0F 58 /r
			var modRM = ModRMEnum.FromComponents(3, 1, 2);
			var instruction = DecodeSingle_Protected32(0x66, 0x0F, 0x58, (byte)modRM);
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
			var instruction = DecodeSingle_Protected32(vex.GetFirstByte(), vex.GetSecondByte(), vex.GetThirdByte(), 0x58, (byte)modRM);
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

		private static Instruction[] Decode(CodeContext context, params byte[] bytes)
		{
			var decoder = new InstructionDecoder(InstructionLookup.Instance, context);
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

		private static Instruction[] Decode_IA32e(AddressSize defaultAddressSize, params byte[] bytes)
			=> Decode(CodeContextEnum.GetIA32e(defaultAddressSize), bytes);

		private static Instruction[] Decode_Protected32(params byte[] bytes)
			=> Decode(CodeContext.Protected_Default32, bytes);

		private static Instruction DecodeSingle(CodeContext context, params byte[] bytes)
		{
			var instructions = Decode(context, bytes);
			Assert.AreEqual(1, instructions.Length);
			return instructions[0];
		}

		private static Instruction DecodeSingle_IA32e(AddressSize defaultAddressSize, params byte[] bytes)
			=> DecodeSingle(CodeContextEnum.GetIA32e(defaultAddressSize), bytes);

		private static Instruction DecodeSingle_Protected32(params byte[] bytes)
			=> DecodeSingle(CodeContext.Protected_Default32, bytes);

		private static Instruction DecodeSingle_64Bit(params byte[] bytes)
			=> DecodeSingle(CodeContext.SixtyFourBit, bytes);
	}
}
