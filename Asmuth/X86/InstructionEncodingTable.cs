using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Text;

namespace Asmuth.X86
{
	public sealed partial class InstructionEncodingTable : IInstructionDecoderLookup
	{
		private static readonly object lookupSuccessTag = new object();

		public static InstructionEncodingTable Instance { get; } = new InstructionEncodingTable();

		private static bool Lookup(ushort[] table, byte opcode)
			=> (table[opcode >> 4] & (1 << (opcode & 0b1111))) != 0;

		public static bool HasModRM(OpcodeMap map, byte opcode)
		{
			switch (map)
			{
				case OpcodeMap.Default: return Lookup(opcode_NoEscape_HasModRM, opcode);

				case OpcodeMap.Escape0F:
					if (!Lookup(opcode_Escape0F_HasModRMValid, opcode))
						throw new ArgumentException("Unknown opcode.");
					return Lookup(opcode_Escape0F_HasModRM, opcode);

				default: throw new NotImplementedException();
			}
		}

		public static ImmediateSize? GetImmediateSize(OpcodeMap opcodeMap, byte opcode, byte? modReg)
		{
			if (opcodeMap == OpcodeMap.Default)
			{
				switch (opcode & 0xF0)
				{
					// ALU ops, imm8 and imm16/32 "overloads" + miscs without immediates
					case 0x00: case 0x10: case 0x20: case 0x30:
						// ALU ops, imm8 and imm16/32 "overloads" + miscs without immediates
						if ((opcode & 0xF) == 0x4) return ImmediateSize.Fixed8;
						if ((opcode & 0xF) == 0x5) return ImmediateSize.Operand16Or32;
						return ImmediateSize.Zero;

					case 0x40: // INC+r/DEC+r/REX
					case 0x50: // PUSH+r/POP+r
						return ImmediateSize.Zero;

					case 0x60:
						if (opcode == 0x68 || opcode == 0x69)
							return ImmediateSize.Operand16Or32; // PUSH/IMUL
						if (opcode == 0x6A || opcode == 0x6B)
							return ImmediateSize.Fixed8; // PUSH/IMUL
						return ImmediateSize.Zero; // PUSHA/POPA/BOUND/ARPL/Prefixes/INS/OUTS

					case 0x70: return ImmediateSize.Fixed8; // Jcc rel8

					case 0x80:
						// ALU
						if (opcode == 0x80 || opcode == 0x82 || opcode == 0x83) return ImmediateSize.Fixed8;
						if (opcode == 0x81) return ImmediateSize.Operand16Or32;
						return ImmediateSize.Zero; // TEST,XCHG,MOV,LEA,POP

					case 0x90: return ImmediateSize.Zero; // XCHG, misc

					case 0xA0:
						if (opcode == 0xA8) return ImmediateSize.Fixed8; // TEST
						if (opcode == 0xA9) return ImmediateSize.Operand16Or32; // TEST
						return ImmediateSize.Zero; // MOV/MOVS/CMPS/STOS/LODS/SCAS

					case 0xB0: // MOV
						return opcode < 0xB8 ? ImmediateSize.Fixed8 : ImmediateSize.Operand16Or32Or64;

					case 0xF0:
						if (opcode < 0xF6) return ImmediateSize.Zero; // LOCK/INT1/REPNE/REP/HLT/CMC
						if (opcode < 0xF8)
						{
							// Unary group 3: TEST/???/NOT/NEG/MUL/IMUL/DIV/IDIV
							if (!modReg.HasValue) return null;
							if (modReg == 1) throw new NotSupportedException();
							if (modReg > 1) return ImmediateSize.Zero;
							return opcode == 0xF6 ? ImmediateSize.Fixed8 : ImmediateSize.Operand16Or32;
						}

						return ImmediateSize.Zero; // CLC/STC/CLI/STI/CLD/STD/Group 4/Group 5

					default:
						throw new NotImplementedException();
				}
			}
			else if (opcodeMap == OpcodeMap.Escape0F)
			{
				// Very few immediates here
				if (opcode >= 0x70 && opcode <= 0x73) return ImmediateSize.Fixed8;
				if ((opcode & 0xF0) == 0x80) return ImmediateSize.Operand16Or32; // Jcc rel16/32
				if (opcode == 0xC2 || opcode == 0xC4 || opcode == 0xC5 || opcode == 0xC6)
					return ImmediateSize.Fixed8;
				return ImmediateSize.Zero;
			}

			throw new NotImplementedException();
		}
		
		public object TryLookup(CodeSegmentType codeSegmentType,
			ImmutableLegacyPrefixList legacyPrefixes, Xex xex, byte opcode, byte? modReg,
			out bool hasModRM, out int immediateSizeInBytes)
		{
			hasModRM = HasModRM(xex.OpcodeMap, opcode);
			if (!hasModRM && modReg.HasValue) throw new ArgumentException();

			ImmediateSize? immediateSize = GetImmediateSize(xex.OpcodeMap, opcode, modReg);
			if (!immediateSize.HasValue)
			{
				// We need to read the ModRM byte
				Contract.Assert(hasModRM && modReg == null);
				immediateSizeInBytes = -1;
				return null;
			}

			immediateSizeInBytes = immediateSize.Value.InBytes(
				operandSize: codeSegmentType.GetIntegerOperandSize(legacyPrefixes, xex),
				addressSize: codeSegmentType.GetEffectiveAddressSize(legacyPrefixes));
			return lookupSuccessTag;
		}
	}
}
