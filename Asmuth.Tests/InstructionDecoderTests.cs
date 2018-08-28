using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;

namespace Asmuth.X86
{
	[TestClass]
	public sealed class InstructionDecoderTests
	{
		private static readonly OpcodeEncoding Nop = new OpcodeEncoding.Builder
		{
			MainByte = 0x90
		};

		private static readonly OpcodeEncoding Nop_RM = new OpcodeEncoding.Builder
		{
			Map = OpcodeMap.Escape0F,
			ModRM = ModRMEncoding.Any,
			MainByte = 0x1F
		};

		private static readonly OpcodeEncoding Add_RM_R = new OpcodeEncoding.Builder
		{
			MainByte = 0x01,
			ModRM = ModRMEncoding.Any
		};
		
		[TestMethod]
		public void TestNop()
		{
			var table = new OpcodeEncodingTable<string>();
			table.Add(Nop, "nop");

			var instruction = DecodeSingle_32Bits(table, 0x90);
			Assert.AreEqual(NonLegacyPrefixesForm.Escapes, instruction.NonLegacyPrefixes.Form);
			Assert.AreEqual(0x90, instruction.MainOpcodeByte);
		}

		[TestMethod]
		public void TestMultiByteNops()
		{
			var table = new OpcodeEncodingTable<string>();
			table.Add(Nop, "nop");
			table.Add(Nop_RM, "nop");

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

			var instructions = Decode(table, CodeSegmentType.IA32_Default32, nops);
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
				Assert.AreEqual(length >= 4 && length <= 6, instruction.DisplacementSize == DisplacementSize.SByte);
				Assert.AreEqual(length >= 7, instruction.DisplacementSize == DisplacementSize.SDword);
			}
		}

		[TestMethod]
		public void TestSib()
		{
			AssertModRMSibRoundTrip(
				ModRM.WithSib(ModRMMod.Indirect, reg: 3),
				new Sib(SibScale._1, GprCode.A, GprCode.A));
		}

		private static void AssertModRMSibRoundTrip(ModRM modRM, Sib sib)
		{
			var table = new OpcodeEncodingTable<string>();
			table.Add(Add_RM_R, "add r/m, r");

			var instruction = DecodeSingle_32Bits(table, Add_RM_R.MainByte, (byte)modRM, (byte)sib);
			Assert.AreEqual(modRM, instruction.ModRM);
			Assert.AreEqual(sib, instruction.Sib);
		}

		[TestMethod]
		public void TestModRMFixedIndirectAmbiguity()
		{
			var table = new OpcodeEncodingTable<string>();

			table.Add(new OpcodeEncoding.Builder
			{
				MainByte = 0xD9,
				ModRM = ModRMEncoding.FromFixedRegMemRM(2)
			}, "fst");

			table.Add(new OpcodeEncoding.Builder
			{
				MainByte = 0xD9,
				ModRM = ModRMEncoding.FromFixedValue(0xD0)
			}, "fnop");

			Assert.AreEqual("fst", DecodeSingleForTag_32Bits(table, 0xD9, 0b00_010_000)); // Indirect eax /2
			Assert.AreEqual("fnop", DecodeSingleForTag_32Bits(table, 0xD9, 0b11_010_000)); // Direct eax /2
		}

		[TestMethod]
		public void TestDisplacementSizes()
		{
			var table = new OpcodeEncodingTable<string>();
			table.Add(Nop_RM, "nop");

			var directModRM = new ModRM(ModRMMod.Direct, (byte)0, 0);
			var indirectDisp8ModRM = new ModRM(ModRMMod.IndirectDisp8, (byte)0, 0);
			var indirectLongDispModRM = new ModRM(ModRMMod.IndirectLongDisp, (byte)0, 0);
			Assert.AreEqual(DisplacementSize.None, DecodeSingle_32Bits(table, 0x0F, 0x1F, directModRM).DisplacementSize);
			Assert.AreEqual(DisplacementSize.SByte, DecodeSingle_32Bits(table, 0x0F, 0x1F, indirectDisp8ModRM, 0x00).DisplacementSize);
			Assert.AreEqual(DisplacementSize.SDword, DecodeSingle_32Bits(table, 0x0F, 0x1F, indirectLongDispModRM, 0x00, 0x00, 0x00, 0x00).DisplacementSize);
			Assert.AreEqual(DisplacementSize.SWord, DecodeSingle_32Bits(table, 0x67, 0x0F, 0x1F, indirectLongDispModRM, 0x00, 0x00).DisplacementSize);
			Assert.AreEqual(DisplacementSize.SWord, DecodeSingle_16Bits(table, 0x0F, 0x1F, indirectLongDispModRM, 0x00, 0x00).DisplacementSize);
			Assert.AreEqual(DisplacementSize.SDword, DecodeSingle_16Bits(table, 0x67, 0x0F, 0x1F, indirectLongDispModRM, 0x00, 0x00, 0x00, 0x00).DisplacementSize);
			Assert.AreEqual(DisplacementSize.SDword, DecodeSingle_64Bits(table, 0x0F, 0x1F, indirectLongDispModRM, 0x00, 0x00, 0x00, 0x00).DisplacementSize);
		}

		[TestMethod]
		public void TestImmSizes()
		{
			var table = new OpcodeEncodingTable<string>();

			table.Add(new OpcodeEncoding.Builder
			{
				MainByte = 0xB0,
				ModRM = ModRMEncoding.MainByteReg,
				ImmediateSizeInBytes = sizeof(sbyte)
			}, "mov r8, imm8");

			table.Add(new OpcodeEncoding.Builder
			{
				LongMode = false,
				OperandSize = OperandSizeEncoding.Word,
				OperandSizePromotion = false,
				MainByte = 0xB8,
				ModRM = ModRMEncoding.MainByteReg,
				ImmediateSizeInBytes = sizeof(short)
			}, "mov r16, imm16");

			table.Add(new OpcodeEncoding.Builder
			{
				OperandSize = OperandSizeEncoding.Dword,
				OperandSizePromotion = false,
				MainByte = 0xB8,
				ModRM = ModRMEncoding.MainByteReg,
				ImmediateSizeInBytes = sizeof(int)
			}, "mov r32, imm32");

			table.Add(new OpcodeEncoding.Builder
			{
				LongMode = true,
				OperandSizePromotion = true,
				MainByte = 0xB8,
				ModRM = ModRMEncoding.MainByteReg,
				ImmediateSizeInBytes = sizeof(long)
			}, "mov r64, imm64");

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
				Assert.AreEqual((byte)1, instruction.ImmediateData.GetByte(0));
			}
		}

		[TestMethod]
		public void TestModRMRequiredForImmediateSize()
		{
			var table = new OpcodeEncodingTable<string>();
			
			table.Add(new OpcodeEncoding.Builder
			{
				OperandSize = OperandSizeEncoding.Dword,
				OperandSizePromotion = false,
				MainByte = 0xF7,
				ModRM = ModRMEncoding.FromFixedRegAnyRM(0),
				ImmediateSizeInBytes = sizeof(int)
			}, "test r/m32, imm32");

			table.Add(new OpcodeEncoding.Builder
			{
				OperandSize = OperandSizeEncoding.Dword,
				OperandSizePromotion = false,
				MainByte = 0xF7,
				ModRM = ModRMEncoding.FromFixedRegAnyRM(3)
			}, "neg r/m32");
			
			Assert.AreEqual(4, DecodeSingle_32Bits(table, 0xF7, ModRM_Reg(0), 0x00, 0x01, 0x02, 0x03).ImmediateSizeInBytes);
			Assert.AreEqual(0, DecodeSingle_32Bits(table, 0xF7, ModRM_Reg(3)).ImmediateSizeInBytes);
		}

		[TestMethod]
		public void TestMOffs()
		{
			var table = new OpcodeEncodingTable<string>();

			// Add all possible variants of "mov eax,moffs*"
			for (var addressSize = AddressSize._16; addressSize <= AddressSize._64; ++addressSize)
			{
				var minOperandSize = addressSize == AddressSize._64
					? IntegerSize.Dword : IntegerSize.Word;
				var maxOperandSize = minOperandSize + 1;
				for (IntegerSize operandSize = minOperandSize; operandSize <= maxOperandSize; operandSize++)
				{
					var builder = new OpcodeEncoding.Builder
					{
						AddressSize = addressSize,
						MainByte = 0xC7,
						ImmediateSizeInBytes = addressSize.InBytes()
					};
					
					if (addressSize == AddressSize._16)
					{
						builder.LongMode = false;
						builder.OperandSizePromotion = false;
					}
					else if (addressSize == AddressSize._64)
					{
						builder.LongMode = true;
					}

					if (operandSize == IntegerSize.Word)
					{
						builder.OperandSize = OperandSizeEncoding.Word;
						builder.LongMode = false;
						builder.OperandSizePromotion = false;
					}
					else if (operandSize == IntegerSize.Dword)
					{
						builder.OperandSize = OperandSizeEncoding.Dword;
					}
					else if (operandSize == IntegerSize.Qword)
					{
						builder.LongMode = true;
						builder.OperandSizePromotion = true;
					}

					table.Add(builder, $"[a{addressSize.InBits()}] mov eax,moffs{operandSize.InBits()}");
				}
			}

			// In different code segment types
			Assert.AreEqual(2, DecodeSingle_16Bits(table, 0x66, 0xC7, 0x00, 0x00).ImmediateSizeInBytes);
			Assert.AreEqual(4, DecodeSingle_32Bits(table, 0x66, 0xC7, 0x00, 0x00, 0x00, 0x00).ImmediateSizeInBytes);
			Assert.AreEqual(8, DecodeSingle_64Bits(table, 0xC7, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00).ImmediateSizeInBytes);

			// With address size override
			Assert.AreEqual(4, DecodeSingle_16Bits(table, 0x67, 0xC7, 0x00, 0x00, 0x00, 0x00).ImmediateSizeInBytes);
			Assert.AreEqual(2, DecodeSingle_32Bits(table, 0x67, 0xC7, 0x00, 0x00).ImmediateSizeInBytes);
			Assert.AreEqual(4, DecodeSingle_64Bits(table, 0x67, 0xC7, 0x00, 0x00, 0x00, 0x00).ImmediateSizeInBytes);

			// With operand size override (no impact)
			Assert.AreEqual(4, DecodeSingle_32Bits(table, 0x66, 0xC7, 0x00, 0x00, 0x00, 0x00).ImmediateSizeInBytes);
		}

		[TestMethod]
		public void TestRexAfter0F()
		{
			var CMovNE = new OpcodeEncoding.Builder
			{
				Map = OpcodeMap.Escape0F,
				MainByte = 0x45, // 0x45 should not be interpreted as REX
				ModRM = ModRMEncoding.Any
			};

			var table = new OpcodeEncodingTable<string>();
			table.Add(CMovNE, "cmovne");

			var instruction = DecodeSingle_64Bits(table, 0x0F, 0x45, 0xC8); // cmovne ecx, eax
			Assert.AreEqual(OpcodeMap.Escape0F, instruction.OpcodeMap);
			Assert.AreEqual((byte)0x45, instruction.MainOpcodeByte);
			Assert.IsTrue(instruction.ModRM.HasValue);
		}

		[TestMethod]
		public void TestRexIncDecAmbiguity()
		{
			var table = new OpcodeEncodingTable<string>();

			table.Add(new OpcodeEncoding.Builder
			{
				LongMode = false,
				MainByte = 0x40,
				ModRM = ModRMEncoding.MainByteReg
			}, "inc r16/32");

			table.Add(new OpcodeEncoding.Builder
			{
				LongMode = false,
				MainByte = 0x48,
				ModRM = ModRMEncoding.MainByteReg
			}, "dec r16/32");

			table.Add(new OpcodeEncoding.Builder
			{
				MainByte = 0x01,
				ModRM = ModRMEncoding.Any
			}, "add rm, r");
			
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
			table.Add(new OpcodeEncoding.Builder
			{
				MainByte = 0xC4,
				ModRM = ModRMEncoding.AnyReg_MemRM
			}, "les m/r");

			table.Add(new OpcodeEncoding.Builder
			{
				MainByte = 0xC5,
				ModRM = ModRMEncoding.AnyReg_MemRM
			}, "lds m/r");

			table.Add(new OpcodeEncoding.Builder
			{
				VexType = VexType.Vex,
				VectorSize = SseVectorSize._128,
				SimdPrefix = SimdPrefix._F3,
				Map = OpcodeMap.Escape0F,
				MainByte = 0xE6,
				ModRM = ModRMEncoding.Any
			}, "VCVTDQ2PD xmm1, xmm2/m64");
			
			Assert.AreEqual(0xC4, DecodeSingle_32Bits(table, 0xC4, 0x01).MainOpcodeByte);
			Assert.AreEqual(0xC5, DecodeSingle_32Bits(table, 0xC5, 0x01).MainOpcodeByte);
			Assert.AreEqual(0xE6, DecodeSingle_32Bits(table, 0xC4, 0xC1, 0x7A, 0xE6, 0x00).MainOpcodeByte);
			Assert.AreEqual(0xE6, DecodeSingle_64Bits(table, 0xC4, 0x01, 0x7A, 0xE6, 0x00).MainOpcodeByte);
		}

		[TestMethod]
		public void TestAddpd()
		{
			var table = new OpcodeEncodingTable<string>();

			table.Add(new OpcodeEncoding.Builder
			{
				SimdPrefix = SimdPrefix._66,
				Map = OpcodeMap.Escape0F,
				MainByte = 0x58,
				ModRM = ModRMEncoding.Any
			}, "ADDPD xmm1,xmm2/m128");
			
			var modRM = ModRM.WithDirectRM(1, 2);
			var instruction = DecodeSingle_32Bits(table, 0x66, 0x0F, 0x58, modRM);
			Assert.AreEqual(SimdPrefix._66, instruction.PotentialSimdPrefix);
			Assert.AreEqual(NonLegacyPrefixesForm.Escapes, instruction.NonLegacyPrefixes.Form);
			Assert.AreEqual(OpcodeMap.Escape0F, instruction.OpcodeMap);
			Assert.AreEqual(0x58, instruction.MainOpcodeByte);
			Assert.AreEqual(modRM, instruction.ModRM);
		}

		[TestMethod]
		public void TestVaddpd()
		{
			var table = new OpcodeEncodingTable<string>();

			table.Add(new OpcodeEncoding.Builder
			{
				VexType = VexType.Vex,
				VectorSize = SseVectorSize._128,
				SimdPrefix = SimdPrefix._66,
				Map = OpcodeMap.Escape0F,
				MainByte = 0x58,
				ModRM = ModRMEncoding.Any
			}, "ADDPD xmm1,xmm2,xmm3/m128");

			var modRM = ModRM.WithDirectRM(1, 2);
			var vex = new Vex3Xop.Builder
			{
				SimdPrefix = SimdPrefix._66,
				OpcodeMap = OpcodeMap.Escape0F
			}.Build();

			var instruction = DecodeSingle_32Bits(table, vex.FirstByte, vex.SecondByte, vex.ThirdByte, 0x58, modRM);
			Assert.AreEqual(NonLegacyPrefixesForm.Vex3, instruction.NonLegacyPrefixes.Form);
			Assert.AreEqual(SseVectorSize._128, instruction.NonLegacyPrefixes.VectorSize);
			Assert.AreEqual((byte)0, instruction.NonLegacyPrefixes.NonDestructiveReg);
			Assert.IsFalse(instruction.NonLegacyPrefixes.OperandSizePromotion);
			Assert.IsFalse(instruction.NonLegacyPrefixes.ModRegExtension);
			Assert.IsFalse(instruction.NonLegacyPrefixes.BaseRegExtension);
			Assert.IsFalse(instruction.NonLegacyPrefixes.IndexRegExtension);
			Assert.AreEqual(SimdPrefix._66, instruction.PotentialSimdPrefix);
			Assert.AreEqual(OpcodeMap.Escape0F, instruction.OpcodeMap);
			Assert.AreEqual(0x58, instruction.MainOpcodeByte);
			Assert.AreEqual(modRM, instruction.ModRM);
		}
		
		[TestMethod]
		public void TestImm8ExtAmbiguity()
		{
			var table = new OpcodeEncodingTable<string>();

			var builder = new OpcodeEncoding.Builder
			{
				SimdPrefix = SimdPrefix.None,
				Map = OpcodeMap.Escape0F,
				MainByte = 0xC2,
				ModRM = ModRMEncoding.Any,
				ImmediateSizeInBytes = 1
			};

			builder.Imm8Ext = 0x00;
			table.Add(builder, "cmpeqps");

			builder.Imm8Ext = 0x01;
			table.Add(builder, "cmpltps");

			Assert.AreEqual("cmpeqps", DecodeSingleForTag_32Bits(table, 0x0F, 0xC2, 0x00, 0x00));
			Assert.AreEqual("cmpltps", DecodeSingleForTag_32Bits(table, 0x0F, 0xC2, 0x00, 0x01));
		}

		private static byte ModRM_Reg(byte reg) => ModRM.WithDirectRM(reg, 0);

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
			=> DecodeSingle(lookup, CodeSegmentType.IA32_Default16, bytes);

		private static Instruction DecodeSingle_32Bits(IInstructionDecoderLookup lookup, params byte[] bytes)
			=> DecodeSingle(lookup, CodeSegmentType.IA32_Default32, bytes);

		private static Instruction DecodeSingle_64Bits(IInstructionDecoderLookup lookup, params byte[] bytes)
			=> DecodeSingle(lookup, CodeSegmentType.LongMode, bytes);
		
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
			=> DecodeSingleForTag(lookup, CodeSegmentType.IA32_Default32, bytes);
	}
}
