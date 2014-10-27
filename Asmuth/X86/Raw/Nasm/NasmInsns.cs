using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw.Nasm
{
	/// <summary>
	/// Provides methods for manipulating instruction definitions in NASM's insns.dat format.
	/// </summary>
	public static class NasmInsns
	{
		private static readonly Regex ignoredLineRegex = new Regex(@"
			\A \s*
			(
				;.*
				| [A-Z]+ (\t+ ignore){3}
			)?
			\Z", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace);
		private static readonly Regex instructionTemplateLineRegex = new Regex(@"
			\A \s*
			(?<mnemonic>[^\t]+) \t+
			(?<operand_values>\S+) \t+
			\[ (?<operand_fields>[-a-z]+:(?<evex_tuple_type>:)?)? \s+ (?<encoding>[^\]\r\n\t]+?) \s* \] \t+
			(?<flags>\S+)
			\s* \Z
			", RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		public static IEnumerable<NasmInstructionTemplate> Read(TextReader textReader)
		{
			Contract.Requires(textReader != null);

			while (true)
			{
				var line = textReader.ReadLine();
				if (line == null) break;

				if (IsIgnoredLine(line)) continue;

				NasmInstructionTemplate instruction;
				ParseLine(line, out instruction);
				yield return instruction;
			}
		}

		public static OperandFields? TryParseOperandField(char c)
		{
			switch (c)
			{
				case '-': return OperandFields.None;
				case 'r': return OperandFields.ModReg;
				case 'm': return OperandFields.ModRM;
				case 'x': return OperandFields.SibIndex;
				case 'i': return OperandFields.Immediate;
				case 'j': return OperandFields.SecondaryImmediate;
				case 'v': return OperandFields.VexNds;
				case 's': return OperandFields.VexIS4;
				default: return null;
			}
		}

		public static bool IsIgnoredLine(string line)
		{
			Contract.Requires(line != null);

			return ignoredLineRegex.IsMatch(line);
		}

		public static void ParseLine(string line, out NasmInstructionTemplate instruction)
		{
			Contract.Requires(line != null);

			var match = instructionTemplateLineRegex.Match(line);
			if (!match.Success) throw new FormatException();

			instruction = default(NasmInstructionTemplate);
			instruction.Mnemonic = match.Groups["mnemonic"].Value;

			ParseEncoding(ref instruction, match.Groups["encoding"].Value);
			ParseOperands(ref instruction, match.Groups["operand_fields"].Value, match.Groups["operand_values"].Value);
			ParseFlags(ref instruction, match.Groups["flags"].Value);

			var evexTupleTypeGroup = match.Groups["evex_tuple_type"];
			if (evexTupleTypeGroup.Success)
				instruction.EVexTupleType = (NasmEVexTupleType)Enum.Parse(typeof(NasmEVexTupleType), evexTupleTypeGroup.Value, ignoreCase: true);
		}

		private static void ParseEncoding(ref NasmInstructionTemplate instruction, string str)
		{
			var tokens = str.Split(' ');
			int tokenIndex = 0;

			if (Regex.IsMatch(tokens[0], @"\A(vex|xop|evex)\."))
			{
				ParseVex(ref instruction, tokens[0]);
				++tokenIndex;
			}
			else
			{
				ParseEncoding_SimdPrefixAndAttributes(ref instruction, tokens, ref tokenIndex);
				ParseEncoding_OpcodeMap(ref instruction, tokens, ref tokenIndex);
			}

			ParseEncoding_MainByte(instruction, tokens, ref tokenIndex);
			ParseEncoding_ModRM(instruction, tokens, ref tokenIndex);
			ParseEncoding_Immediates(instruction, tokens, ref tokenIndex);
			if (instruction.OpcodeValue.GetMap() == OpcodeMap._3DNow)
				ParseEncoding_MainByte(instruction, tokens, ref tokenIndex);

			Contract.Assert(tokenIndex == tokens.Length);
		}

		private static void ParseEncoding_SimdPrefixAndAttributes(InstructionDefinition instruction, IReadOnlyList<string> tokens, ref int tokenIndex)
		{
			while (true)
			{
				switch (tokens[tokenIndex])
				{
					case "adf":
					case "a16":
					case "a32":
					case "a64":
					case "hle":
					case "hlenl":
					case "hlexr":
					case "jmp8":
					case "mustrep":
					case "nohi":
					case "nof3":
					case "norep":
					case "norexb":
					case "np":
					case "odf":
					case "o16":
					case "o32":
					case "o64":
					case "o64nw":
					case "rex.l":
					case "vm32x":
					case "vm64x":
					case "wait":
					case "repe":
						++tokenIndex; // Ignore for now
						break;

					case "66":
						Contract.Assert((instruction.OpcodeValue & Opcode.SimdPrefix_Mask) == Opcode.SimdPrefix_None);
						instruction.OpcodeValue = instruction.OpcodeValue.WithSimdPrefix(SimdPrefix._66);
						++tokenIndex;
						break;

					case "f2i":
						Contract.Assert((instruction.OpcodeValue & Opcode.SimdPrefix_Mask) == Opcode.SimdPrefix_None);
						instruction.OpcodeValue = instruction.OpcodeValue.WithSimdPrefix(SimdPrefix._F2);
						++tokenIndex;
						break;

					case "f3i":
						Contract.Assert((instruction.OpcodeValue & Opcode.SimdPrefix_Mask) == Opcode.SimdPrefix_None);
						instruction.OpcodeValue = instruction.OpcodeValue.WithSimdPrefix(SimdPrefix._F3);
						++tokenIndex;
						break;

					case "norexw":
						Contract.Assert(!instruction.OpcodeMask.HasFlag(Opcode.RexW));
						instruction.OpcodeMask |= Opcode.RexW;
						++tokenIndex;
						break;

					default:
						instruction.OpcodeMask |= Opcode.SimdPrefix_Mask;
						return;
				}
			}
		}

		private static void ParseEncoding_OpcodeMap(InstructionDefinition instruction, IReadOnlyList<string> tokens, ref int tokenIndex)
		{
			if (tokens[tokenIndex] == "0f")
			{
				instruction.OpcodeValue = instruction.OpcodeValue.WithMap(OpcodeMap._0F);
				++tokenIndex;
				
				switch (tokens[tokenIndex])
				{
					case "0f":
						// 0f 0f is not actually a prefix (the second 0f is the opcode byte), but we treat it as such
						instruction.OpcodeValue = instruction.OpcodeValue.WithMap(OpcodeMap._3DNow);
						++tokenIndex;
						break;

					case "38":
						instruction.OpcodeValue = instruction.OpcodeValue.WithMap(OpcodeMap._0F38);
						++tokenIndex;
						break;

					case "3A":
						instruction.OpcodeValue = instruction.OpcodeValue.WithMap(OpcodeMap._0F3A);
						++tokenIndex;
						break;
				}
			}

			instruction.OpcodeMask |= Opcode.Map_Mask;
		}

		private static void ParseEncoding_MainByte(InstructionDefinition instruction, IReadOnlyList<string> tokens, ref int tokenIndex)
		{
			Contract.Assert((instruction.OpcodeMask & Opcode.MainByte_Mask) == 0);

			var token = tokens[tokenIndex];
			
			bool isLow3BitRegister = false;
			if (token.Length == 4 && token.EndsWith("+r"))
			{
				isLow3BitRegister = true;
				token = token.Substring(0, 2);
			}

			byte mainByte = byte.Parse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
			Contract.Assert(!isLow3BitRegister || (mainByte & 7) == 0);

			instruction.OpcodeValue = instruction.OpcodeValue.WithMainByte(mainByte);
			instruction.OpcodeMask |= isLow3BitRegister ? Opcode.MainByte_High5Mask : Opcode.MainByte_Mask;
			++tokenIndex;
		}

		private static void ParseEncoding_ModRM(InstructionDefinition instruction, IReadOnlyList<string> tokens, ref int tokenIndex)
		{
			if (tokenIndex >= tokens.Count) return;

			var token = tokens[tokenIndex];
			if (token.Length == 2)
			{
				if (token[0] == '/')
				{
					if (token[1] == 'r')
					{
						// We allow ModRM without mandating a specific value
						instruction.OpcodeValue |= Opcode.ModRM_Mask;
						++tokenIndex;
					}
					else if (token[1] >= '0' && token[1] <= '7')
					{
						// We only mandate a specific value for the reg field
						byte reg = (byte)(token[1] - '0');
						instruction.OpcodeValue |= (Opcode)((uint)reg << (int)Opcode.ModRM_RegShift);
						instruction.OpcodeMask |= Opcode.ModRM_RegMask;
						++tokenIndex;
					}
				}
				else
				{
					byte @byte;
					if (byte.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out @byte))
					{
						// We mandate a specific value for the entire ModRM field
						instruction.OpcodeValue |= (Opcode)((uint)@byte << (int)Opcode.ModRM_Mask);
						instruction.OpcodeMask |= Opcode.ModRM_Mask;
					}
				}
			}
		}

		private static void ParseEncoding_Immediates(InstructionDefinition instruction, IReadOnlyList<string> tokens, ref int tokenIndex)
		{
			while (tokenIndex < tokens.Count)
			{
				var token = tokens[tokenIndex];
				switch (token)
				{
					case "iwd":	// "iwd seg", meaning ptr16:32, word + dword
					{
						Contract.Assert(tokenIndex + 1 < tokens.Count && tokens[tokenIndex + 1] == "seg");
						instruction.ImmediateSize += 6;	// 
						tokenIndex += 2;
						continue; 
					}

					case "rel": instruction.ImmediateSize += 4; break;
					case "rel8": instruction.ImmediateSize += 1; break;

					default:
					{
						if (Regex.IsMatch(token, @"\Aib(,[su])?\Z"))
						{
							instruction.ImmediateSize += 1; break;
						}
						else if (Regex.IsMatch(token, @"\Aiw?d?q?(?<!i)\Z"))
						{

						}

						var match = Regex.Match(tokens[tokenIndex], @"\Ai(b(,[su])(,[su])?\Z");
						if (!match.Success) return;
						
						switch (token[1])
						{
							case 'b': instruction.ImmediateSize += 1; break;
							case 'w': instruction.ImmediateSize += 2; break;
							case 'd': instruction.ImmediateSize += 4; break;
							case 'q': instruction.ImmediateSize += 8; break;
							default: throw new UnreachableException("Unexpected insns immediate type char.");
						}
						break;
					}
				}

				++tokenIndex;
			}
		}

		#region ParseVex
		private static void ParseVex(ref NasmInstructionTemplate instruction, string str)
		{
			var tokens = str.ToLowerInvariant().Split('.');
			int tokenIndex = 0;

			VexOpcodeEncoding encoding = 0;
			switch (tokens[tokenIndex++])
			{
				case "vex": encoding |= VexOpcodeEncoding.Type_Vex; break;
				case "xop": encoding |= VexOpcodeEncoding.Type_Xop; break;
				case "evex": encoding |= VexOpcodeEncoding.Type_EVex; break;
				default: throw new FormatException();
			}

			if (tokens[tokenIndex][0] == 'm')
			{
				// AMD-Style
				// xop.m8.w0.nds.l0.p0
				// vex.m3.w0.nds.l0.p1
				ParseVex_Map_AmdStyle(ref encoding, tokens, ref tokenIndex);
				ParseVex_RexW(ref encoding, tokens, ref tokenIndex);
				ParseVex_Vvvv(ref encoding, tokens, ref tokenIndex);
				ParseVex_VectorLength(ref encoding, tokens, ref tokenIndex);
				ParseVex_SimdPrefix_AmdStyle(ref encoding, tokens, ref tokenIndex);
			}
			else
			{
				// Intel-Style
				// vex.nds.256.66.0f3a.w0
				// evex.nds.512.66.0f3a.w0
				ParseVex_Vvvv(ref encoding, tokens, ref tokenIndex);
				ParseVex_VectorLength(ref encoding, tokens, ref tokenIndex);
				ParseVex_SimdPrefix_IntelStyle(ref encoding, tokens, ref tokenIndex);
				ParseVex_Map_IntelStyle(ref encoding, tokens, ref tokenIndex);
				ParseVex_RexW(ref encoding, tokens, ref tokenIndex);
			}

			instruction.vexOpcodeEncoding = encoding;
		}

		private static void ParseVex_Vvvv(ref VexOpcodeEncoding encoding, string[] tokens, ref int tokenIndex)
		{
			switch (tokens[tokenIndex])
			{
				case "nds": encoding |= VexOpcodeEncoding.Vvvv_Nds; break;
				case "ndd": encoding |= VexOpcodeEncoding.Vvvv_Ndd; break;
				case "dds": encoding |= VexOpcodeEncoding.Vvvv_Dds; break;
				default: encoding |= VexOpcodeEncoding.Vvvv_Invalid; return;
			}

			++tokenIndex;
		}

		private static void ParseVex_VectorLength(ref VexOpcodeEncoding encoding, string[] tokens, ref int tokenIndex)
		{
			switch (tokens[tokenIndex])
			{
				case "l0": case "128": encoding |= VexOpcodeEncoding.VectorLength_0; break;
				case "l1": case "256": encoding |= VexOpcodeEncoding.VectorLength_1; break;
				case "512": encoding |= VexOpcodeEncoding.VectorLength_2; break;
				case "lig": encoding |= VexOpcodeEncoding.VectorLength_Ignored; break;
				default: encoding |= VexOpcodeEncoding.VectorLength_Ignored; return;
			}

			++tokenIndex;
		}

		private static void ParseVex_SimdPrefix_IntelStyle(ref VexOpcodeEncoding encoding, string[] tokens, ref int tokenIndex)
		{
			switch (tokens[tokenIndex])
			{
				case "66": encoding |= VexOpcodeEncoding.SimdPrefix_66; break;
				case "f2": encoding |= VexOpcodeEncoding.SimdPrefix_F2; break;
				case "f3": encoding |= VexOpcodeEncoding.SimdPrefix_F3; break;
				default: encoding |= VexOpcodeEncoding.SimdPrefix_None; return;
			}

			++tokenIndex;
		}

		private static void ParseVex_SimdPrefix_AmdStyle(ref VexOpcodeEncoding encoding, string[] tokens, ref int tokenIndex)
		{
			switch (tokens[tokenIndex])
			{
				case "p0": encoding |= VexOpcodeEncoding.SimdPrefix_None; break;
				case "p1": encoding |= VexOpcodeEncoding.SimdPrefix_66; break;
				default: encoding |= VexOpcodeEncoding.SimdPrefix_None; return;
			}

			++tokenIndex;
		}

		private static void ParseVex_Map_IntelStyle(ref VexOpcodeEncoding encoding, string[] tokens, ref int tokenIndex)
		{
			switch (tokens[tokenIndex++]) // Mandatory
			{
				case "0f": encoding |= VexOpcodeEncoding.Map_0F; break;
				case "0f38": encoding |= VexOpcodeEncoding.Map_0F38; break;
				case "0f3a": encoding |= VexOpcodeEncoding.Map_0F3A; break;
				default: throw new FormatException();
			}
		}

		private static void ParseVex_Map_AmdStyle(ref VexOpcodeEncoding encoding, string[] tokens, ref int tokenIndex)
		{
			var token = tokens[tokenIndex++];  // Mandatory

			switch (encoding & VexOpcodeEncoding.Type_Mask)
			{
				case VexOpcodeEncoding.Type_Xop:
					switch (token)
					{
						case "m8": encoding |= VexOpcodeEncoding.Map_Xop8; break;
						case "m9": encoding |= VexOpcodeEncoding.Map_Xop9; break;
						case "m10": encoding |= VexOpcodeEncoding.Map_Xop10; break;
						default: throw new FormatException();
					}
					break;

				case VexOpcodeEncoding.Type_Vex:
					switch (token)
					{
						case "m3": encoding |= VexOpcodeEncoding.Map_0F3A; break;
						default: throw new FormatException();
					}
					break;

				default:
					throw new UnreachableException();
			}
		}

		private static void ParseVex_RexW(ref VexOpcodeEncoding encoding, string[] tokens, ref int tokenIndex)
		{
			switch (tokenIndex < tokens.Length ? tokens[tokenIndex] : "")
			{
				case "w0": encoding |= VexOpcodeEncoding.RexW_0; break;
				case "w1": encoding |= VexOpcodeEncoding.RexW_1; break;
				case "wig": encoding |= VexOpcodeEncoding.RexW_Ignored; break;
				default: encoding |= VexOpcodeEncoding.RexW_Ignored; return;
			}

			++tokenIndex;
		}
		#endregion

		private static void ParseOperands(ref NasmInstructionTemplate instruction, string fieldsString, string valuesString)
		{
			if (fieldsString == null)
			{
				Contract.Assert(valuesString == "void");
				return;
			}

			var values = valuesString.Split(',');
			Contract.Assert(values.Length == fieldsString.Length);

			instruction.Operands = new NasmOperand[values.Length];

			for (int i = 0; i < values.Length; ++i)
			{
				instruction.Operands[i].Field = TryParseOperandField(fieldsString[i]).Value;
				var valueComponents = values[i].Split('|');
				// TODO: Parse NASM operand value components
			}
		}

		private static void ParseFlags(ref NasmInstructionTemplate instruction, string str)
		{
			foreach (var flag in str.Split(','))
			{
				var enumerantName = char.IsDigit(flag[0]) ? '_' + flag : flag;
				var enumerant = Enum.Parse(typeof(NasmInstructionFlag), flag, ignoreCase: true);

				if ((byte)enumerant < 64) instruction.LowFlags |= 1UL << (int)enumerant;
				else instruction.HighFlags |= 1UL << ((int)enumerant - 64);
			}
		}
    }
}
