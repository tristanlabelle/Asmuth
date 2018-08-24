using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86
{
	partial struct VexEncoding
	{
		public static VexEncoding Parse(string str)
		{
			str = str.Trim().ToLowerInvariant();
			var tokens = str.Split('.');
			int tokenIndex = 0;

			var builder = new Builder();
			switch (tokens[tokenIndex++])
			{
				case "vex": builder.Type = VexType.Vex; break;
				case "xop": builder.Type = VexType.Xop; break;
				case "evex": builder.Type = VexType.EVex; break;
				default: throw new FormatException();
			}
			
			if (tokens[tokenIndex][0] == 'm')
			{
				// AMD-Style
				// xop.m8.w0.nds.l0.p0
				// vex.m3.w0.nds.l0.p1
				builder.OpcodeMap = ParseOpcodeMap(tokens, ref tokenIndex);
				builder.RexW = TryParseRexW(tokens, ref tokenIndex);
				builder.RegOperand = TryParseVvvv(tokens, ref tokenIndex);
				builder.VectorSize = TryParseVectorSize(tokens, ref tokenIndex);
				builder.SimdPrefix = TryParseSimdPrefix(tokens, ref tokenIndex);
			}
			else
			{
				// Intel-Style
				// vex.nds.256.66.0f3a.w0
				// evex.nds.512.66.0f3a.w0
				builder.RegOperand = TryParseVvvv(tokens, ref tokenIndex);
				builder.VectorSize = TryParseVectorSize(tokens, ref tokenIndex);
				builder.SimdPrefix = TryParseSimdPrefix(tokens, ref tokenIndex);
				builder.OpcodeMap = ParseOpcodeMap(tokens, ref tokenIndex);
				builder.RexW = TryParseRexW(tokens, ref tokenIndex);
			}

			if (tokenIndex != tokens.Length) throw new FormatException();

			return builder.Build();
		}

		private static VexRegOperand TryParseVvvv(string[] tokens, ref int tokenIndex)
		{
			if (tokenIndex == tokens.Length) return VexRegOperand.Invalid;
			switch (tokens[tokenIndex])
			{
				case "nds": tokenIndex++; return VexRegOperand.Source;
				case "ndd": tokenIndex++; return VexRegOperand.Dest;
				case "dds": tokenIndex++; return VexRegOperand.SecondSource;
				default: return VexRegOperand.Invalid;
			}
		}

		private static SseVectorSize? TryParseVectorSize(string[] tokens, ref int tokenIndex)
		{
			if (tokenIndex == tokens.Length) return null;
			switch (tokens[tokenIndex])
			{
				case "lz": case "l0": case "128": tokenIndex++; return SseVectorSize._128;
				case "l1": case "256": tokenIndex++; return SseVectorSize._256;
				case "512": tokenIndex++; return SseVectorSize._512;
				case "lig": tokenIndex++; return null;
				default: return null;
			}
		}

		private static SimdPrefix TryParseSimdPrefix(string[] tokens, ref int tokenIndex)
		{
			if (tokenIndex == tokens.Length) return SimdPrefix.None;
			switch (tokens[tokenIndex])
			{
				case "p0": tokenIndex++; return SimdPrefix.None;
				case "p1": case "66": tokenIndex++; return SimdPrefix._66;
				case "f3": tokenIndex++; return SimdPrefix._F3;
				case "f2": tokenIndex++; return SimdPrefix._F2;
				default: return SimdPrefix.None;
			}
		}

		private static OpcodeMap ParseOpcodeMap(string[] tokens, ref int tokenIndex)
		{
			switch (tokens[tokenIndex++]) // Mandatory
			{
				case "0f": return OpcodeMap.Escape0F;
				case "0f38": return OpcodeMap.Escape0F38;
				case "0f3a": return OpcodeMap.Escape0F38;
				case "m3": return OpcodeMap.Escape0F3A;
				case "m8": return OpcodeMap.Xop8;
				case "m9": return OpcodeMap.Xop9;
				case "m10": return OpcodeMap.Xop10;
				default: throw new FormatException();
			}
		}

		private static bool? TryParseRexW(string[] tokens, ref int tokenIndex)
		{
			if (tokenIndex == tokens.Length) return null;
			switch (tokens[tokenIndex])
			{
				case "wig": tokenIndex++; return null;
				case "w0": tokenIndex++; return false;
				case "w1": tokenIndex++; return true;
				default: return null;
			}
		}
	}
}
