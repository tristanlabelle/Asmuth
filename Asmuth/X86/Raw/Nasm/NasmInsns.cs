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

		public static IEnumerable<NasmInsnsEntry> Read(TextReader textReader)
		{
			Contract.Requires(textReader != null);

			while (true)
			{
				var line = textReader.ReadLine();
				if (line == null) break;

				if (IsIgnoredLine(line)) continue;
				yield return ParseLine(line);
			}
		}

		public static OperandFields? TryParseOperandField(char c)
		{
			switch (c)
			{
				case '-': return OperandFields.None;
				case 'r': return OperandFields.ModRM_Reg;
				case 'm': return OperandFields.ModRM_RM;
				case 'x': return OperandFields.Sib_Index;
				case 'i': return OperandFields.Immediate;
				case 'j': return OperandFields.SecondImmediate;
				case 'v': return OperandFields.Vex_V;
				case 's': return OperandFields.EVex_IS4;
				default: return null;
			}
		}

		public static bool IsIgnoredLine(string line)
		{
			Contract.Requires(line != null);

			return ignoredLineRegex.IsMatch(line);
		}

		public static NasmInsnsEntry ParseLine(string line)
		{
			Contract.Requires(line != null);

			var match = instructionTemplateLineRegex.Match(line);
			if (!match.Success) throw new FormatException();

			var entryBuilder = new NasmInsnsEntry.Builder();
			entryBuilder.Mnemonic = match.Groups["mnemonic"].Value;

			ParseEncoding(entryBuilder, match.Groups["encoding"].Value);
			ParseOperands(entryBuilder, match.Groups["operand_fields"].Value, match.Groups["operand_values"].Value);
			ParseFlags(entryBuilder, match.Groups["flags"].Value);

			var evexTupleTypeGroup = match.Groups["evex_tuple_type"];
			if (evexTupleTypeGroup.Success)
				entryBuilder.EVexTupleType = (NasmEVexTupleType)Enum.Parse(typeof(NasmEVexTupleType), evexTupleTypeGroup.Value, ignoreCase: true);

			return entryBuilder.Build(reuse: false);
		}

		private static void ParseEncoding(NasmInsnsEntry.Builder entryBuilder, string str)
		{
			var tokens = str.Split(' ');
			int tokenIndex = 0;

			if (Regex.IsMatch(tokens[0], @"\A(vex|xop|evex)\."))
			{
				ParseVex(entryBuilder, tokens[0]);
				++tokenIndex;
			}

			Contract.Assert(tokenIndex == tokens.Length);
		}

		private static void ParseEncodingTokens(NasmInsnsEntry.Builder entryBuilder, IReadOnlyList<string> tokens, ref int tokenIndex)
		{
			while (tokenIndex < tokens.Count)
			{
				var token = tokens[tokenIndex++];

				var encodingFlag = NasmEncodingFlagsEnum.TryFromNasmName(token);
				if (encodingFlag != NasmEncodingFlag.None)
				{
					entryBuilder.EncodingTokens.Add(new NasmEncodingToken(encodingFlag));
					continue;
				}

				if (token == "/r")
				{
					entryBuilder.EncodingTokens.Add(new NasmEncodingToken(NasmEncodingTokenType.ModRM, 0xFF));
					continue;
				}

				byte @byte;
				if ((token.Length == 2 || (token.Length == 4 && token.EndsWith("+r")))
					&& byte.TryParse(token.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out @byte))
				{
					var type = token.Length == 2 ? NasmEncodingTokenType.Byte : NasmEncodingTokenType.BytePlusRegister;
					entryBuilder.EncodingTokens.Add(new NasmEncodingToken(type, @byte));
				}

				throw new FormatException("Unexpected NASM encoding token '{0}'".FormatInvariant(token));
			}
		}

		#region ParseVex
		private static void ParseVex(NasmInsnsEntry.Builder entryBuilder, string str)
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

			entryBuilder.EncodingTokens.Add(new NasmEncodingToken(NasmEncodingTokenType.Vex));
			entryBuilder.VexEncoding = encoding;
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

		private static void ParseOperands(NasmInsnsEntry.Builder entryBuilder, string fieldsString, string valuesString)
		{
			if (fieldsString == null)
			{
				Contract.Assert(valuesString == "void");
				return;
			}

			var values = valuesString.Split(',');
			Contract.Assert(values.Length == fieldsString.Length);

			for (int i = 0; i < values.Length; ++i)
			{
				var field = TryParseOperandField(fieldsString[i]).Value;

				var valueComponents = values[i].Split('|');
				var typeString = valueComponents[0];
				var type = (NasmOperandType)Enum.Parse(typeof(NasmOperandType), valueComponents[0]);
				entryBuilder.Operands.Add(new NasmOperand(field, type));
				// TODO: Parse NASM operand flags (after the '|')
			}
		}

		private static void ParseFlags(NasmInsnsEntry.Builder entryBuilder, string str)
		{
			foreach (var flagStr in str.Split(','))
			{
				var enumerantName = char.IsDigit(flagStr[0]) ? '_' + flagStr : flagStr;
				var flag = (NasmInstructionFlag)Enum.Parse(typeof(NasmInstructionFlag), flagStr, ignoreCase: true);
				entryBuilder.Flags.Add(flag);
			}
		}
    }
}
