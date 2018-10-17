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

		public static ImmediateSizeEncoding? GetImmediateSize(
			OpcodeMap opcodeMap, byte mainByte, ModRM? modRM)
		{
			if (opcodeMap == OpcodeMap.Default)
			{
				switch (mainByte & 0xF0)
				{
					// ALU ops, imm8 and imm16/32 "overloads" + miscs without immediates
					case 0x00: case 0x10: case 0x20: case 0x30:
						// ALU ops, imm8 and imm16/32 "overloads" + miscs without immediates
						if ((mainByte & 0b111) == 0b100) return ImmediateSizeEncoding.Byte;
						if ((mainByte & 0b111) == 0b101) return ImmediateSizeEncoding.WordOrDword_OperandSize;
						return ImmediateSizeEncoding.Zero;

					case 0x40: // INC+r/DEC+r/REX
					case 0x50: // PUSH+r/POP+r
						return ImmediateSizeEncoding.Zero;

					case 0x60:
						if (mainByte == 0x68 || mainByte == 0x69)
							return ImmediateSizeEncoding.WordOrDword_OperandSize; // PUSH/IMUL
						if (mainByte == 0x6A || mainByte == 0x6B)
							return ImmediateSizeEncoding.Byte; // PUSH/IMUL
						return ImmediateSizeEncoding.Zero; // PUSHA/POPA/BOUND/ARPL/Prefixes/INS/OUTS

					case 0x70: return ImmediateSizeEncoding.Byte; // Jcc rel8

					case 0x80:
						// ALU
						if (mainByte == 0x80 || mainByte == 0x82 || mainByte == 0x83) return ImmediateSizeEncoding.Byte;
						if (mainByte == 0x81) return ImmediateSizeEncoding.WordOrDword_OperandSize;
						return ImmediateSizeEncoding.Zero; // TEST,XCHG,MOV,LEA,POP

					case 0x90: return ImmediateSizeEncoding.Zero; // XCHG, misc

					case 0xA0:
						if (mainByte == 0xA8) return ImmediateSizeEncoding.Byte; // TEST
						if (mainByte == 0xA9) return ImmediateSizeEncoding.WordOrDword_OperandSize; // TEST
						return ImmediateSizeEncoding.Zero; // MOV/MOVS/CMPS/STOS/LODS/SCAS

					case 0xB0: // MOV
						return mainByte < 0xB8 ? ImmediateSizeEncoding.Byte
							: ImmediateSizeEncoding.WordOrDwordOrQword_OperandSize;

					case 0xC0:
						if (mainByte <= 0xC1) return ImmediateSizeEncoding.Byte; // Shift group imm8
						if (mainByte == 0xC2) return ImmediateSizeEncoding.Word; // ret imm16
						if (mainByte == 0xC6) return ImmediateSizeEncoding.Byte; // MOV imm8
						if (mainByte == 0xC7) return ImmediateSizeEncoding.WordOrDword_OperandSize; // MOV imm8
						if (mainByte == 0xC8) return ImmediateSizeEncoding.FromBytes(6); // ENTER imm16, imm8
						if (mainByte == 0xCA) return ImmediateSizeEncoding.Word; // FAR RET imm16
						if (mainByte == 0xCD) return ImmediateSizeEncoding.Byte; // INT imm8
						return ImmediateSizeEncoding.Zero;

					case 0xD0:
						// D0-D3: Shift group
						if (mainByte == 0xD4 || mainByte == 0xD5) return ImmediateSizeEncoding.Byte; // AAM/AAD
						return ImmediateSizeEncoding.Zero; // D6-D7: XLAT, D8-DF: FPU

					case 0xE0:
						if (mainByte <= 0xE7) return ImmediateSizeEncoding.Byte; // LOOP/JCXZ/IN/OUT
						if (mainByte < 0xE9) return ImmediateSizeEncoding.WordOrDword_OperandSize; // NEAR CALL/JMP rel*
						if (mainByte == 0xEA) return ImmediateSizeEncoding.FromBytes(2, ImmediateVariableSize.WordOrDword_OperandSize); // JMP ptr16:16/32
						if (mainByte == 0xEB) return ImmediateSizeEncoding.Byte; // JUMP rel8
						return ImmediateSizeEncoding.Zero; // IN/OUT

					case 0xF0:
						if (mainByte < 0xF6) return ImmediateSizeEncoding.Zero; // LOCK/INT1/REPNE/REP/HLT/CMC
						if (mainByte < 0xF8)
						{
							// Unary group 3: TEST/???/NOT/NEG/MUL/IMUL/DIV/IDIV
							if (!modRM.HasValue) return null; // Need ModR/M
							byte modReg = modRM.Value.Reg;
							if (modReg == 1) throw new NotSupportedException();
							if (modReg > 1) return ImmediateSizeEncoding.Zero;
							return mainByte == 0xF6 ? ImmediateSizeEncoding.Byte : ImmediateSizeEncoding.WordOrDword_OperandSize;
						}

						return ImmediateSizeEncoding.Zero; // CLC/STC/CLI/STI/CLD/STD/Group 4/Group 5

					default:
						throw new NotImplementedException();
				}
			}
			else if (opcodeMap == OpcodeMap.Escape0F)
			{
				// Very few immediates here
				if (mainByte >= 0x70 && mainByte <= 0x73) return ImmediateSizeEncoding.Byte;
				if ((mainByte & 0xF0) == 0x80) return ImmediateSizeEncoding.WordOrDword_OperandSize; // Jcc rel16/32
				if (mainByte == 0xC2 || mainByte == 0xC4 || mainByte == 0xC5 || mainByte == 0xC6)
					return ImmediateSizeEncoding.Byte;
				return ImmediateSizeEncoding.Zero;
			}

			throw new NotImplementedException();
		}
		
		public InstructionDecoderLookupResult Lookup(CodeSegmentType codeSegmentType,
			ImmutableLegacyPrefixList legacyPrefixes, NonLegacyPrefixes nonLegacyPrefixes,
			byte mainByte, ModRM? modRM, byte? imm8)
		{
			bool hasModRM = HasModRM(nonLegacyPrefixes.OpcodeMap, mainByte);
			if (!hasModRM && modRM.HasValue) throw new ArgumentException();

			ImmediateSizeEncoding? immediateSize = GetImmediateSize(nonLegacyPrefixes.OpcodeMap, mainByte, modRM);
			if (!immediateSize.HasValue)
			{
				// We need to read the ModRM byte
				Debug.Assert(hasModRM && modRM == null);
				return InstructionDecoderLookupResult.Ambiguous_RequireModRM;
			}
			
			return InstructionDecoderLookupResult.Success(hasModRM,
				immediateSize.Value.InBytes(codeSegmentType, legacyPrefixes, nonLegacyPrefixes));
		}
	}
}
