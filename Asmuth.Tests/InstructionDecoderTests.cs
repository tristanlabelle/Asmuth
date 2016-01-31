using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace Asmuth.X86.Raw
{
	[TestClass]
	public sealed class InstructionDecoderTests
	{
		private sealed class InstructionLookup : IInstructionDecoderLookup
		{
			public static readonly InstructionLookup Instance = new InstructionLookup();

			public bool TryLookup(InstructionDecodingMode mode, Opcode opcode, out bool hasModRM, out OperandSize? immediateSize)
			{
				if (opcode.GetMainByte() == 0x90)
				{
					// NOP
					hasModRM = false;
					immediateSize = null;
					return true;
				}
				else if (opcode.GetMap() == OpcodeMap.Leading0F && opcode.GetMainByte() == 0x1F)
				{
					// 3+ byte nop form
					hasModRM = true;
					immediateSize = null;
					return true;
				}
				else
				{
					hasModRM = false;
					immediateSize = null;
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
				0x66, 0x0F, 0x1F, 0x84, 0x00, 0x00, 0x00, 0x00, 0x00,
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
					instruction.LegacyPrefixes.Tail == LegacyPrefix.OperandSizeOverride);
				Assert.AreEqual(length >= 5 && length != 7, instruction.Sib.HasValue);
				Assert.AreEqual(length <= 3, instruction.DisplacementSizeInBytes == 0);
				Assert.AreEqual(length >= 4 && length <= 6, instruction.DisplacementSizeInBytes == 1);
				Assert.AreEqual(length >= 7, instruction.DisplacementSizeInBytes == 4);
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
