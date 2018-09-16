using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Asmuth.X86.Xed
{
	partial struct XedBlot
	{
		private enum TokenType : byte
		{
			BinaryOrHexLiteral,
			DecimalLiteral,
			BitsPattern, // Includes bit patterns like 'mm', 'rxbw', 'ssss_uuuu' and '1_ddd'
			Identifier,
			Equal,
			NotEqual,
			OpenBracket,
			CloseBracket,
			OpenCloseParen,
		}

		private readonly struct Token : IEquatable<Token>
		{
			private readonly string str;
			public TokenType Type { get; }
			private readonly byte value;
			private readonly byte bitCount;

			private Token(string str, TokenType type)
			{
				this.str = str;
				this.Type = type;
				this.value = 0;
				this.bitCount = 0;
			}

			private Token(TokenType type)
			{
				this.str = null;
				this.Type = type;
				this.value = 0;
				this.bitCount = 0;
			}

			private Token(byte value, byte bitCount)
			{
				if (bitCount == 0 || bitCount > 8)
					throw new ArgumentOutOfRangeException(nameof(bitCount));

				this.str = null;
				this.Type = TokenType.BinaryOrHexLiteral;
				this.value = value;
				this.bitCount = bitCount;
			}

			private Token(byte decimalValue)
			{
				this.str = null;
				this.Type = TokenType.DecimalLiteral;
				this.value = decimalValue;
				this.bitCount = 0;
			}

			public string Identifier => Type == TokenType.Identifier
				? str : throw new InvalidOperationException();
			public string BitsPattern => Type == TokenType.BitsPattern
				? str : throw new InvalidOperationException();
			public byte Value => Type == TokenType.BinaryOrHexLiteral || Type == TokenType.DecimalLiteral
				? value : throw new InvalidOperationException();
			public byte BitCount => Type == TokenType.BinaryOrHexLiteral
				? bitCount : throw new InvalidOperationException();

			public bool Equals(Token other) => Type == other.Type
				&& str == other.str
				&& value == other.value && bitCount == other.bitCount;
			public override bool Equals(object obj) => obj is Token && Equals((Token)obj);
			public override int GetHashCode() => (int)Type;
			public static bool Equals(Token lhs, Token rhs) => lhs.Equals(rhs);
			public static bool operator ==(Token lhs, Token rhs) => Equals(lhs, rhs);
			public static bool operator !=(Token lhs, Token rhs) => !Equals(lhs, rhs);

			public static Token MakeIdentifier(string name) => new Token(name, TokenType.Identifier);
			public static Token MakeBitsPattern(string str) => new Token(str, TokenType.BitsPattern);
			public static Token BinaryOrHexLiteral(byte value, byte bitCount)
				=> new Token(value, bitCount);
			public static Token DecimalLiteral(byte value) => new Token(value);

			public static readonly Token Equal = new Token(TokenType.Equal);
			public static readonly Token NotEqual = new Token(TokenType.NotEqual);
			public static readonly Token OpenBracket = new Token(TokenType.OpenBracket);
			public static readonly Token CloseBracket = new Token(TokenType.CloseBracket);
			public static readonly Token OpenCloseParen = new Token(TokenType.OpenCloseParen);
		}


		public static XedBlot Parse(string str, bool predicate)
		{
			var tokens = Tokenize(str).ToList();
			if (tokens.Count == 0) throw new FormatException();

			if (tokens[0].Type == TokenType.Identifier)
			{
				if (tokens[1].Type == TokenType.OpenCloseParen)
				{
					if (tokens.Count > 2) throw new FormatException();
					return Call(tokens[0].Identifier);
				}

				string field = tokens[0].Identifier;
				if (tokens[1].Type == TokenType.NotEqual)
				{
					if (tokens.Count != 3) throw new FormatException();
					if (tokens[2].Type != TokenType.BinaryOrHexLiteral
						&& tokens[2].Type != TokenType.DecimalLiteral)
						throw new FormatException();
					return new XedPredicateBlot(field, false, tokens[2].Value);
				}

				if (tokens[1].Type == TokenType.Equal)
				{
					if (!predicate && tokens.Count == 4
						&& tokens[2].Type == TokenType.Identifier
						&& tokens[3].Type == TokenType.OpenCloseParen)
					{
						// "BASE0=ArAX()" case
						return XedAssignmentBlot.Call(field, tokens[2].Identifier);
					}

					if (tokens.Count != 3) throw new FormatException();

					if (tokens[2].Type == TokenType.DecimalLiteral)
					{
						return predicate
							? (XedBlot)new XedPredicateBlot(field, equal: true, tokens[2].Value)
							: (XedBlot)new XedAssignmentBlot(field, tokens[2].Value);
					}
					else if (tokens[2].Type == TokenType.Identifier)
					{
						// "OUTREG=XED_REG_XMM0" case
						var constantName = tokens[2].Identifier;
						return XedAssignmentBlot.NamedConstant(field, constantName);
					}
					else if (tokens[2].Type == TokenType.BitsPattern)
					{
						// "REXW=w" case
						var spans = ParseBitsPattern(tokens[2].BitsPattern);
						if (spans.Length != 1) throw new FormatException();
						return new XedAssignmentBlot(field, spans[0].Letter, spans[0].BitCount);
					}
					else throw new FormatException();
				}

				if (tokens[1].Type == TokenType.OpenBracket)
				{
					if (tokens.Count != 4 || tokens[3].Type != TokenType.CloseBracket)
						throw new FormatException();

					var bitsToken = tokens[2];
					if (bitsToken.Type == TokenType.BitsPattern)
						return new XedBitsBlot(field, ParseBitsPattern(bitsToken.BitsPattern));
					if (bitsToken.Type == TokenType.BinaryOrHexLiteral)
						return new XedBitsBlot(field, bitsToken.Value, bitsToken.BitCount);

					throw new FormatException();
				}

				throw new FormatException();
			}
			else if (tokens[0].Type == TokenType.BinaryOrHexLiteral)
			{
				if (tokens.Count > 1) throw new FormatException();
				return new XedBitsBlotSpan(tokens[0].Value, tokens[0].BitCount);
			}
			else if (tokens[0].Type == TokenType.BitsPattern)
			{
				if (tokens.Count > 1) throw new FormatException();
				return new XedBitsBlot(ParseBitsPattern(tokens[0].BitsPattern));
			}
			else throw new FormatException();

			throw new UnreachableException();
		}

		private static ImmutableArray<XedBitsBlotSpan> ParseBitsPattern(string str)
		{
			var spans = ImmutableArray.CreateBuilder<XedBitsBlotSpan>();
			int startIndex = 0;
			while (true)
			{
				// Advance to next var
				if (startIndex == str.Length) break;
				char firstChar = str[startIndex];
				if (firstChar == '_')
				{
					startIndex++;
					continue;
				}

				int length = 1;
				if (firstChar == '0' || firstChar == '1')
				{
					byte value = (byte)(firstChar - '0');
					while (true)
					{
						if (startIndex + length == str.Length) break;
						char c = str[startIndex + length];
						if (c != '0' && c != '1') break;
						value = (byte)((value << 1) | (c - '0'));
						length++;
					}

					spans.Add(new XedBitsBlotSpan(value, bitCount: length));
				}
				else if (firstChar >= 'a' || firstChar <= 'z')
				{
					while (startIndex + length < str.Length
						&& str[startIndex + length] == firstChar)
					{
						length++;
					}

					spans.Add(new XedBitsBlotSpan(firstChar, length));
				}
				else throw new FormatException();

				startIndex += length;
			}

			return spans.ToImmutable();
		}

		private static char AtOrNull(string str, int index)
			=> index < str.Length ? str[index] : '\0';

		private static IEnumerable<Token> Tokenize(string str)
		{
			int startIndex = 0;
			while (startIndex != str.Length)
			{
				// Skip whitespace
				if (char.IsWhiteSpace(str[startIndex]))
				{
					startIndex++;
					continue;
				}

				int length = 1;
				char startChar = str[startIndex];
				if (startChar == '=') yield return Token.Equal;
				else if (startChar == '[') yield return Token.OpenBracket;
				else if (startChar == ']') yield return Token.CloseBracket;
				else if (startChar == '(')
				{
					if (AtOrNull(str, startIndex + 1) != ')') throw new FormatException();
					length = 2;
					yield return Token.OpenCloseParen;
				}
				else if (startChar == '!')
				{
					if (AtOrNull(str, startIndex + 1) != '=') throw new FormatException();
					length = 2;
					yield return Token.NotEqual;
				}
				else if (startChar >= '0' && startChar <= '9')
				{
					char secondChar = AtOrNull(str, startIndex + 1);
					if (startChar == '0' && secondChar == 'x')
					{
						// Hex
						length = 2;
						byte value = 0;
						while (true)
						{
							char c = AtOrNull(str, startIndex + length);
							byte digit;
							if (c >= '0' && c <= '9') { digit = (byte)(c - '0'); }
							else if (c >= 'A' && c <= 'F') { digit = (byte)(c - 'A' + 10); }
							else if (c >= 'a' && c <= 'f') { digit = (byte)(c - 'a' + 10); }
							else
							{
								if (char.IsLetterOrDigit(c))
									throw new FormatException();
								break;
							}

							value = (byte)((value << 4) | digit);
							length++;
						}
						
						yield return Token.BinaryOrHexLiteral(value, (byte)((length - 2) * 4));
					}
					else if (startChar == '0' && secondChar == 'b')
					{
						// Binary
						length = 2;
						byte value = 0;
						while (true)
						{
							char c = AtOrNull(str, startIndex + length);
							if (c != '0' && c != '1')
							{
								if (char.IsLetterOrDigit(c))
									throw new FormatException();
								break;
							}

							value = (byte)((value << 1) | (c - '0'));
							length++;
						}

						yield return Token.BinaryOrHexLiteral(value, (byte)(length - 2));
					}
					else
					{
						// Dec or bit pattern starting with 0-9
						length = 0;
						byte value = 0;
						bool potentialBitPattern = true;
						while (true)
						{
							char c = AtOrNull(str, startIndex + length);
							if (c < '0' || c > '9')
							{
								if (char.IsLetter(c) || c == '_')
								{
									if (!potentialBitPattern) throw new FormatException();
								}
								else
								{
									// Everything was digits, not a bit pattern
									potentialBitPattern = false;
								}
									
								break;
							}
							if (c >= '2' && c <= '9') potentialBitPattern = false;

							if (value > 25) throw new FormatException();
							value = (byte)(value * 10 + (c - '0'));
							length++;
						}

						if (potentialBitPattern)
						{
							// Bit pattern starting with 0/1
							length = 0;
							while (true)
							{
								char c = AtOrNull(str, startIndex + length);
								if ((c >= 'a' && c <= 'z') || c == '0' || c == '1' || c == '_')
								{
									length++;
								}
								else if (char.IsLetterOrDigit(c))
									throw new FormatException();
								else
									break;
							}

							yield return Token.MakeBitsPattern(str.Substring(startIndex, length));
						}
						else
						{
							yield return Token.DecimalLiteral(value);
						}
					}
				}
				else if (char.IsLetter(startChar) || startChar == '_')
				{
					// Identifier or bits pattern
					length = 0;
					bool bitsPattern = true;
					while (true)
					{
						char c = AtOrNull(str, startIndex + length);
						if (c >= 'a' && c <= 'z') { }
						else if (c == '0' || c == '1' || c == '_') { }
						else if (c >= '2' && c <= '9') bitsPattern = false;
						else if (c >= 'A' && c <= 'Z') bitsPattern = false;
						else break;
						++length;
					}

					var substr = str.Substring(startIndex, length);
					yield return bitsPattern
						? Token.MakeBitsPattern(substr)
						: Token.MakeIdentifier(substr);
				}
				else throw new FormatException();

				startIndex += length;
			}
		}
	}
}
