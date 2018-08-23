using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Asmuth.X86
{
	public sealed partial class InstructionEncodingTable : IInstructionDecoderLookup
	{
		public static InstructionEncodingTable Instance { get; } = new InstructionEncodingTable();

		private static bool Lookup(ushort[] table, byte mainByte)
			=> (table[mainByte >> 4] & (1 << (mainByte & 0b1111))) != 0;

		public static bool HasModRM(OpcodeMap map, byte mainByte)
		{
			switch (map)
			{
				case OpcodeMap.Default: return Lookup(opcode_NoEscape_HasModRM, mainByte);

				case OpcodeMap.Escape0F:
					if (!Lookup(opcode_Escape0F_HasModRMValid, mainByte))
						throw new ArgumentException("Unknown opcode.");
					return Lookup(opcode_Escape0F_HasModRM, mainByte);

				default: throw new NotImplementedException();
			}
		}

		public static ImmediateSize? GetImmediateSize(OpcodeMap opcodeMap, byte mainByte, ModRM? modRM)
		{
			if (opcodeMap == OpcodeMap.Default)
			{
				switch (mainByte & 0xF0)
				{
					// ALU ops, imm8 and imm16/32 "overloads" + miscs without immediates
					case 0x00: case 0x10: case 0x20: case 0x30:
						// ALU ops, imm8 and imm16/32 "overloads" + miscs without immediates
						if ((mainByte & 0b111) == 0b100) return ImmediateSize.Fixed8;
						if ((mainByte & 0b111) == 0b101) return ImmediateSize.Operand16Or32;
						return ImmediateSize.Zero;

					case 0x40: // INC+r/DEC+r/REX
					case 0x50: // PUSH+r/POP+r
						return ImmediateSize.Zero;

					case 0x60:
						if (mainByte == 0x68 || mainByte == 0x69)
							return ImmediateSize.Operand16Or32; // PUSH/IMUL
						if (mainByte == 0x6A || mainByte == 0x6B)
							return ImmediateSize.Fixed8; // PUSH/IMUL
						return ImmediateSize.Zero; // PUSHA/POPA/BOUND/ARPL/Prefixes/INS/OUTS

					case 0x70: return ImmediateSize.Fixed8; // Jcc rel8

					case 0x80:
						// ALU
						if (mainByte == 0x80 || mainByte == 0x82 || mainByte == 0x83) return ImmediateSize.Fixed8;
						if (mainByte == 0x81) return ImmediateSize.Operand16Or32;
						return ImmediateSize.Zero; // TEST,XCHG,MOV,LEA,POP

					case 0x90: return ImmediateSize.Zero; // XCHG, misc

					case 0xA0:
						if (mainByte == 0xA8) return ImmediateSize.Fixed8; // TEST
						if (mainByte == 0xA9) return ImmediateSize.Operand16Or32; // TEST
						return ImmediateSize.Zero; // MOV/MOVS/CMPS/STOS/LODS/SCAS

					case 0xB0: // MOV
						return mainByte < 0xB8 ? ImmediateSize.Fixed8 : ImmediateSize.Operand16Or32Or64;

					case 0xC0:
						if (mainByte <= 0xC1) return ImmediateSize.Fixed8; // Shift group imm8
						if (mainByte == 0xC2) return ImmediateSize.Fixed16; // ret imm16
						if (mainByte == 0xC6) return ImmediateSize.Fixed8; // MOV imm8
						if (mainByte == 0xC7) return ImmediateSize.Operand16Or32; // MOV imm8
						if (mainByte == 0xC8) return ImmediateSize.Fixed24; // ENTER imm16, imm8
						if (mainByte == 0xCA) return ImmediateSize.Fixed16; // FAR RET imm16
						if (mainByte == 0xCD) return ImmediateSize.Fixed8; // INT imm8
						return ImmediateSize.Zero;

					case 0xD0:
						// D0-D3: Shift group
						if (mainByte == 0xD4 || mainByte == 0xD5) return ImmediateSize.Fixed8; // AAM/AAD
						return ImmediateSize.Zero; // D6-D7: XLAT, D8-DF: FPU

					case 0xE0:
						if (mainByte <= 0xE7) return ImmediateSize.Fixed8; // LOOP/JCXZ/IN/OUT
						if (mainByte < 0xE9) return ImmediateSize.Operand16Or32; // NEAR CALL/JMP rel*
						if (mainByte == 0xEA) return ImmediateSize.Operand32Or48; // JMP ptr16:16/32
						if (mainByte == 0xEB) return ImmediateSize.Fixed8; // JUMP rel8
						return ImmediateSize.Zero; // IN/OUT

					case 0xF0:
						if (mainByte < 0xF6) return ImmediateSize.Zero; // LOCK/INT1/REPNE/REP/HLT/CMC
						if (mainByte < 0xF8)
						{
							// Unary group 3: TEST/???/NOT/NEG/MUL/IMUL/DIV/IDIV
							if (!modRM.HasValue) return null;
							byte modReg = modRM.Value.Reg;
							if (modReg == 1) throw new NotSupportedException();
							if (modReg > 1) return ImmediateSize.Zero;
							return mainByte == 0xF6 ? ImmediateSize.Fixed8 : ImmediateSize.Operand16Or32;
						}

						return ImmediateSize.Zero; // CLC/STC/CLI/STI/CLD/STD/Group 4/Group 5

					default:
						throw new NotImplementedException();
				}
			}
			else if (opcodeMap == OpcodeMap.Escape0F)
			{
				// Very few immediates here
				if (mainByte >= 0x70 && mainByte <= 0x73) return ImmediateSize.Fixed8;
				if ((mainByte & 0xF0) == 0x80) return ImmediateSize.Operand16Or32; // Jcc rel16/32
				if (mainByte == 0xC2 || mainByte == 0xC4 || mainByte == 0xC5 || mainByte == 0xC6)
					return ImmediateSize.Fixed8;
				return ImmediateSize.Zero;
			}

			throw new NotImplementedException();
		}
		
		public InstructionDecoderLookupResult Lookup(CodeSegmentType codeSegmentType,
			ImmutableLegacyPrefixList legacyPrefixes, NonLegacyPrefixes nonLegacyPrefixes,
			byte mainByte, ModRM? modRM, byte? imm8)
		{
			bool hasModRM = HasModRM(nonLegacyPrefixes.OpcodeMap, mainByte);
			if (!hasModRM && modRM.HasValue) throw new ArgumentException();

			ImmediateSize? immediateSize = GetImmediateSize(nonLegacyPrefixes.OpcodeMap, mainByte, modRM);
			if (!immediateSize.HasValue)
			{
				// We need to read the ModRM byte
				Debug.Assert(hasModRM && modRM == null);
				return InstructionDecoderLookupResult.Ambiguous_RequireModRM;
			}

			int immediateSizeInBytes = immediateSize.Value.InBytes(
				operandSize: codeSegmentType.GetIntegerOperandSize(legacyPrefixes, nonLegacyPrefixes));
			return InstructionDecoderLookupResult.Success(hasModRM, immediateSizeInBytes);
		}
	}
}
