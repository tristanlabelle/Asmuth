﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace Asmuth.X86
{
	[TestClass]
	public sealed class InstructionDecoderTests
	{
		private sealed class InstructionLookup : IInstructionDecoderLookup
		{
			public static readonly InstructionLookup Instance = new InstructionLookup();

			public bool TryLookup(InstructionDecodingMode mode,
				ImmutableLegacyPrefixList legacyPrefixes, Xex xex, byte opcode,
				out bool hasModRM, out int immediateSizeInBytes)
			{
				if (xex.OpcodeMap == OpcodeMap.Default && opcode == 0x90)
				{
					// NOP
					hasModRM = false;
					immediateSizeInBytes = 0;
					return true;
				}
				else if (xex.OpcodeMap == OpcodeMap.Escape0F && opcode == 0x1F)
				{
					// 3+ byte nop form
					hasModRM = true;
					immediateSizeInBytes = 0;
					return true;
				}
				else if (xex.OpcodeMap == OpcodeMap.Default && (opcode & 0xF8) == 0xB0)
				{
					// MOV x8, imm8
					hasModRM = false;
					immediateSizeInBytes = 1;
					return true;
				}
				else if (xex.OpcodeMap == OpcodeMap.Default && (opcode & 0xF8) == 0xB8)
				{
					// Mov r(16|32|64), imm(16|32|64)
					OperandSize operandSize;
					if (mode == InstructionDecodingMode.SixtyFourBit)
						operandSize = xex.OperandSize64 ? OperandSize.Qword : OperandSize.Dword;
					else
						operandSize = mode.GetDefaultOperandSize().OverrideWordDword(
							legacyPrefixes.Contains(LegacyPrefix.OperandSizeOverride));

					hasModRM = false;
					immediateSizeInBytes = operandSize.InBytes();
					return true;
				}
				else
				{
					hasModRM = false;
					immediateSizeInBytes = 0;
					return false;
				}
			}
		}

		[TestMethod]
		public void TestDecodeNop()
		{
			var instruction = DecodeSingle32(0x90);
			Assert.AreEqual(0x90, instruction.MainByte);
			Assert.AreEqual(InstructionFields.Opcode, instruction.Fields);
		}

		[TestMethod]
		public void TestDecodeNopVariations()
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

			var instructions = Decode32(nops);
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
				Assert.AreEqual(length <= 3, instruction.DisplacementSizeInBytes == 0);
				Assert.AreEqual(length >= 4 && length <= 6, instruction.DisplacementSizeInBytes == 1);
				Assert.AreEqual(length >= 7, instruction.DisplacementSizeInBytes == 4);
			}
		}

		[TestMethod]
		public void TestMovImm()
		{
			var instructions = new[]
			{
				DecodeSingle(InstructionDecodingMode.IA32_Default32, 0xB0, 0x01), // MOV AL,imm8
				DecodeSingle(InstructionDecodingMode.IA32_Default16, 0xB8, 0x01, 0x00), // MOV AX,imm16
				DecodeSingle(InstructionDecodingMode.IA32_Default32, 0xB8, 0x01, 0x00, 0x00, 0x00), // MOV EAX,imm32
				DecodeSingle(InstructionDecodingMode.SixtyFourBit, 0x48, 0xB8, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00) // MOV RAX,imm64
			};

			for (int i = 0; i < instructions.Length; ++i)
			{
				var instruction = instructions[i];
				Assert.AreEqual(1 << i, instruction.ImmediateSizeInBytes);
				Assert.AreEqual(1UL, instruction.Immediate);
			}
		}

		public static Instruction[] Decode(InstructionDecodingMode mode, params byte[] bytes)
		{
			var decoder = new InstructionDecoder(InstructionLookup.Instance, mode);
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
			return instructions.ToArray();
		}

		public static Instruction[] Decode32(params byte[] bytes)
			=> Decode(InstructionDecodingMode.IA32_Default32, bytes);

		public static Instruction DecodeSingle(InstructionDecodingMode mode, params byte[] bytes)
		{
			var instructions = Decode(mode, bytes);
			Assert.AreEqual(1, instructions.Length);
			return instructions[0];
		}

		public static Instruction DecodeSingle32(params byte[] bytes)
			=> DecodeSingle(InstructionDecodingMode.IA32_Default32, bytes);
	}
}