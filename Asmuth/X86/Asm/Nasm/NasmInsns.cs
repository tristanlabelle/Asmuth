using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Asmuth.X86.Asm.Nasm
{
	/// <summary>
	/// Provides methods for manipulating instruction definitions in NASM's insns.dat format.
	/// </summary>
	public static class NasmInsns
	{
		private static readonly Regex instructionLineColumnRegex = new Regex(
			@"(\[[^\]]*\]|\S.*?)(?=(\s|\Z))", RegexOptions.CultureInvariant);

		private static readonly Regex codeStringColumnRegex = new Regex(
			@"\A\[
				(
					(?<operand_fields>[a-z-+]+):
					((?<evex_tuple_type>[a-z0-9]+):)?
				)?
				\s*
				(?<encoding>[^\]\r\n\t]+?)
			\s*\]\Z", RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant);

		public static ICollection<string> PseudoInstructionMnemonics = new[]
		{
			"DB", "DW", "DD", "DQ", "DT", "DO", "DY", "DZ",
			"RESB", "RESW", "RESD", "RESQ", "REST", "RESO", "RESY", "RESZ",
		};

		public static IEnumerable<NasmInsnsEntry> Read(TextReader textReader)
		{
			if (textReader == null) throw new ArgumentNullException(nameof(textReader));

			while (true)
			{
				var line = textReader.ReadLine();
				if (line == null) break;

				if (IsIgnoredLine(line)) continue;
				yield return ParseLine(line);
			}
		}

		public static OperandField? ParseOperandField(char c)
		{
			switch (c)
			{
				case '-': return null;
				case 'r': return OperandField.ModReg;
				case 'm': return OperandField.BaseReg;
				case 'x': return OperandField.IndexReg;
				case 'i': return OperandField.Immediate;
				case 'j': return OperandField.Immediate2;
				case 'v': return OperandField.NonDestructiveReg;
				case 's': return OperandField.IS4;
				default: throw new ArgumentOutOfRangeException(nameof(c));
			}
		}

		public static bool IsIgnoredLine(string line)
		{
			if (line == null) throw new ArgumentNullException(nameof(line));
			// Blank or with comment
			return Regex.IsMatch(line, @"\A\s*(;.*)?\Z", RegexOptions.CultureInvariant);
		}

		public static NasmInsnsEntry ParseLine(string line)
		{
			if (line == null) throw new ArgumentNullException(nameof(line));

			var columnMatches = instructionLineColumnRegex.Matches(line);
			if (columnMatches.Count != 4) throw new FormatException();
			
			var entryBuilder = new NasmInsnsEntry.Builder();

			// Mnemonic
			var mnemonicColumn = columnMatches[0].Value;
			if (!Regex.IsMatch(mnemonicColumn, @"\A[A-Z_0-9]+(cc)?\Z", RegexOptions.CultureInvariant))
				throw new FormatException("Invalid mnemonic column format.");
			entryBuilder.Mnemonic = mnemonicColumn;

			// Encoding
			var codeStringColumn = columnMatches[2].Value;
			var operandFieldsString = string.Empty;
			if (codeStringColumn != "ignore")
			{
				var codeStringMatch = codeStringColumnRegex.Match(codeStringColumn);
				if (!codeStringMatch.Success) throw new FormatException("Invalid code string column format.");

				operandFieldsString = codeStringMatch.Groups["operand_fields"].Value;
				var evexTupleTypesString = codeStringMatch.Groups["evex_tuple_type"].Value;
				var encodingString = codeStringMatch.Groups["encoding"].Value;

				VexEncoding? vexEncoding;
				foreach (var encodingToken in ParseEncoding(encodingString, out vexEncoding))
					entryBuilder.EncodingTokens.Add(encodingToken);
				entryBuilder.VexEncoding = vexEncoding.GetValueOrDefault();

				if (evexTupleTypesString.Length > 0)
				{
					entryBuilder.EVexTupleType = (NasmEVexTupleType)Enum.Parse(
						typeof(NasmEVexTupleType), evexTupleTypesString, ignoreCase: true);
				}
			}

			// Operands
			var operandsColumn = columnMatches[1].Value;
			ParseOperands(entryBuilder, operandFieldsString, operandsColumn);

			// Flags
			var flagsColumn = columnMatches[3].Value;
			ParseFlags(entryBuilder, flagsColumn);
			
			return entryBuilder.Build(reuse: false);
		}

		public static IReadOnlyList<NasmEncodingToken> ParseEncoding(string str, out VexEncoding? vex)
		{
			// In most of the cases 5 tokens should be enough.
			var tokens = new List<NasmEncodingToken>(5);
			vex = null;
			
			foreach (string tokenStr in Regex.Split(str, @"\s+"))
			{
				var tokenType = NasmEncodingToken.TryParseType(tokenStr);
				if (tokenType != NasmEncodingTokenType.None)
				{
					tokens.Add(tokenType);
					continue;
				}

				byte @byte;
				if (Regex.IsMatch(tokenStr, @"\A[0-9a-f]{2}(\+[rc])?\Z")
					&& byte.TryParse(tokenStr.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out @byte))
				{
					var type = NasmEncodingTokenType.Byte;
					if (tokenStr.Length == 4)
					{
						type = tokenStr[tokenStr.Length - 1] == 'r'
							? NasmEncodingTokenType.Byte_PlusRegister
							: NasmEncodingTokenType.Byte_PlusConditionCode;
					}
					tokens.Add(new NasmEncodingToken(type, @byte));
					continue;
				}

				if (Regex.IsMatch(tokenStr, @"\A/[0-7]\Z"))
				{
					tokens.Add(new NasmEncodingToken(NasmEncodingTokenType.ModRM_FixedReg,
						(byte)(tokenStr[1] - '0')));
					continue;
				}

				if (Regex.IsMatch(tokenStr, @"\A(vex|xop|evex)\."))
				{
					if (vex.HasValue) throw new FormatException("Multiple vector XEX prefixes.");
					tokens.Add(NasmEncodingTokenType.Vex);
					vex = ParseVexEncoding(tokenStr);
					continue;
				}

				throw new FormatException("Unexpected NASM encoding token '{0}'".FormatInvariant(tokenStr));
			}

			return tokens;
		}

		#region ParseVex
		public static VexEncoding ParseVexEncoding(string str)
		{
			var tokens = str.ToLowerInvariant().Split('.');
			int tokenIndex = 0;

			VexEncoding encoding = 0;
			switch (tokens[tokenIndex++])
			{
				case "vex": encoding |= VexEncoding.Type_Vex; break;
				case "xop": encoding |= VexEncoding.Type_Xop; break;
				case "evex": encoding |= VexEncoding.Type_EVex; break;
				default: throw new FormatException();
			}

			if (tokens[tokenIndex][0] == 'm')
			{
				// AMD-Style
				// xop.m8.w0.nds.l0.p0
				// vex.m3.w0.nds.l0.p1
				ParseVex_Map(ref encoding, tokens, ref tokenIndex);
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
				ParseVex_Map(ref encoding, tokens, ref tokenIndex);
				ParseVex_RexW(ref encoding, tokens, ref tokenIndex);
			}

			return encoding;
		}

		private static void ParseVex_Vvvv(ref VexEncoding encoding, string[] tokens, ref int tokenIndex)
		{
			switch (tokens[tokenIndex])
			{
				case "nds": encoding |= VexEncoding.NonDestructiveReg_Source; break;
				case "ndd": encoding |= VexEncoding.NonDestructiveReg_Dest; break;
				case "dds": encoding |= VexEncoding.NonDestructiveReg_SecondSource; break;
				default: encoding |= VexEncoding.NonDestructiveReg_Invalid; return;
			}

			++tokenIndex;
		}

		private static void ParseVex_VectorLength(ref VexEncoding encoding, string[] tokens, ref int tokenIndex)
		{
			switch (tokens[tokenIndex])
			{
				case "lz": case "l0": case "128": encoding |= VexEncoding.VectorLength_0; break;
				case "l1": case "256": encoding |= VexEncoding.VectorLength_1; break;
				case "512": encoding |= VexEncoding.VectorLength_2; break;
				case "lig": encoding |= VexEncoding.VectorLength_Ignored; break;
				default: encoding |= VexEncoding.VectorLength_Ignored; return;
			}

			++tokenIndex;
		}

		private static void ParseVex_SimdPrefix_IntelStyle(ref VexEncoding encoding, string[] tokens, ref int tokenIndex)
		{
			switch (tokens[tokenIndex])
			{
				case "66": encoding |= VexEncoding.SimdPrefix_66; break;
				case "f2": encoding |= VexEncoding.SimdPrefix_F2; break;
				case "f3": encoding |= VexEncoding.SimdPrefix_F3; break;
				default: encoding |= VexEncoding.SimdPrefix_None; return;
			}

			++tokenIndex;
		}

		private static void ParseVex_SimdPrefix_AmdStyle(ref VexEncoding encoding, string[] tokens, ref int tokenIndex)
		{
			switch (tokens[tokenIndex])
			{
				case "p0": encoding |= VexEncoding.SimdPrefix_None; break;
				case "p1": encoding |= VexEncoding.SimdPrefix_66; break;
				default: encoding |= VexEncoding.SimdPrefix_None; return;
			}

			++tokenIndex;
		}

		private static void ParseVex_Map(ref VexEncoding encoding, string[] tokens, ref int tokenIndex)
		{
			switch (tokens[tokenIndex++]) // Mandatory
			{
				case "0f": encoding |= VexEncoding.Map_0F; break;
				case "0f38": encoding |= VexEncoding.Map_0F38; break;
				case "m3": case "0f3a": encoding |= VexEncoding.Map_0F3A; break;
				case "m8": encoding |= VexEncoding.Map_Xop8; break;
				case "m9": encoding |= VexEncoding.Map_Xop9; break;
				case "m10": encoding |= VexEncoding.Map_Xop10; break;
				default: throw new FormatException();
			}
		}

		private static void ParseVex_RexW(ref VexEncoding encoding, string[] tokens, ref int tokenIndex)
		{
			if (tokenIndex == tokens.Length) return;
			switch (tokens[tokenIndex++])
			{
				case "w0": encoding |= VexEncoding.RexW_0; break;
				case "w1": encoding |= VexEncoding.RexW_1; break;
				case "wig": encoding |= VexEncoding.RexW_Ignored; break;
				default: encoding |= VexEncoding.RexW_Ignored; return;
			}
		}
		#endregion

		private static void ParseOperands(NasmInsnsEntry.Builder entryBuilder, string fieldsString, string valuesString)
		{
			if (valuesString == "void" || valuesString == "ignore")
			{
				Debug.Assert(fieldsString.Length == 0);
				return;
			}

			if (fieldsString.Length == 0)
			{
				// This only happens for pseudo-instructions
				return;
			}

			valuesString = valuesString.Replace("*", string.Empty); // '*' is for "relaxed", but it's not clear what this encodes
			var values = Regex.Split(valuesString, "[,:]");
			
			if (fieldsString == "r+mi")
			{
				// Hack around the IMUL special case
				fieldsString = "rmi";
				values = new[] { values[0], values[0].Replace("reg", "rm"), values[1] };
			}

			if (values.Length != fieldsString.Length)
				throw new FormatException("Not all operands have associated opcode fields.");

			for (int i = 0; i < values.Length; ++i)
			{
				var field = ParseOperandField(fieldsString[i]);

				var valueComponents = values[i].Split('|');
				var typeString = valueComponents[0];
				var type = (NasmOperandType)Enum.Parse(typeof(NasmOperandType), valueComponents[0], ignoreCase: true);
				entryBuilder.Operands.Add(new NasmOperand(field, type));
				// TODO: Parse NASM operand flags (after the '|')
				// TODO: Support star'ed types like "xmmreg*"
			}
		}

		private static void ParseFlags(NasmInsnsEntry.Builder entryBuilder, string str)
		{
			if (str == "ignore") return;
			foreach (var flagStr in str.Split(','))
			{
				entryBuilder.Flags.Add(flagStr);
			}
		}
    }
}
