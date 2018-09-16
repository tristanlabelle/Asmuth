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
			Identifier, // Includes bit patterns like 'mm', 'rxbw' and 'ssss_uuuu'
			Equal,
			NotEqual,
			OpenBracket,
			CloseBracket,
			OpenCloseParen,
		}

		private readonly struct Token : IEquatable<Token>
		{
			private readonly string identifier;
			public TokenType Type { get; }
			private readonly byte value;
			private readonly byte bitCount;

			public Token(string identifier)
			{
				this.identifier = identifier;
				this.Type = TokenType.Identifier;
				this.value = 0;
				this.bitCount = 0;
			}

			private Token(TokenType type)
			{
				this.identifier = null;
				this.Type = type;
				this.value = 0;
				this.bitCount = 0;
			}

			private Token(byte value, byte bitCount)
			{
				if (bitCount == 0 || bitCount > 8)
					throw new ArgumentOutOfRangeException(nameof(bitCount));

				this.identifier = null;
				this.Type = TokenType.BinaryOrHexLiteral;
				this.value = value;
				this.bitCount = bitCount;
			}

			private Token(byte decimalValue)
			{
				this.identifier = null;
				this.Type = TokenType.DecimalLiteral;
				this.value = decimalValue;
				this.bitCount = 0;
			}

			public string Identifier => identifier ?? throw new InvalidOperationException();
			public byte Value => Type == TokenType.BinaryOrHexLiteral || Type == TokenType.DecimalLiteral
				? value : throw new InvalidOperationException();
			public byte BitCount => Type == TokenType.BinaryOrHexLiteral
				? bitCount : throw new InvalidOperationException();

			public bool Equals(Token other) => Type == other.Type
				&& identifier == other.identifier
				&& value == other.value && bitCount == other.bitCount;
			public override bool Equals(object obj) => obj is Token && Equals((Token)obj);
			public override int GetHashCode() => (int)Type;
			public static bool Equals(Token lhs, Token rhs) => lhs.Equals(rhs);
			public static bool operator ==(Token lhs, Token rhs) => Equals(lhs, rhs);
			public static bool operator !=(Token lhs, Token rhs) => !Equals(lhs, rhs);
			
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
				if (tokens.Count == 1)
					return new XedBitsBlot(ParseVariableBits(tokens[0].Identifier));
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
						return new XedAssignmentBlot(field, tokens[2].Identifier);
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
						// "REXW=w" case
						var spans = ParseVariableBits(tokens[2].Identifier);
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
					if (bitsToken.Type == TokenType.Identifier)
						return new XedBitsBlot(field, ParseVariableBits(bitsToken.Identifier));
					if (bitsToken.Type == TokenType.BinaryOrHexLiteral)
						return new XedBitsBlot(field, bitsToken.Value, bitsToken.BitCount);

					throw new FormatException();
				}

				throw new FormatException();
			}
			else if (tokens[0].Type == TokenType.BinaryOrHexLiteral)
			{
				return new XedBitsBlotSpan(tokens[0].Value, tokens[0].BitCount);
			}
			else throw new FormatException();

			throw new UnreachableException();
		}

		private static ImmutableArray<XedBitsBlotSpan> ParseVariableBits(string identifier)
		{
			var spans = ImmutableArray.CreateBuilder<XedBitsBlotSpan>();
			int startIndex = 0;
			while (true)
			{
				// Advance to next var
				if (startIndex == identifier.Length) break;
				char letter = identifier[startIndex];
				if (letter == '_')
				{
					startIndex++;
					continue;
				}

				if (!char.IsLetter(letter)) throw new FormatException();

				int length = 1;
				while (startIndex + length < identifier.Length
					&& identifier[startIndex + length] == letter)
				{
					length++;
				}

				spans.Add(new XedBitsBlotSpan(letter, length));
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
				else if (char.IsDigit(startChar))
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
						// Dec
						length = 0;
						byte value = 0;
						while (true)
						{
							char c = AtOrNull(str, startIndex + length);
							if (c < '0' || c > '9')
							{
								if (char.IsLetterOrDigit(c))
									throw new FormatException();
								break;
							}

							if (value > 25) throw new FormatException();
							value = (byte)(value * 10 + (c - '0'));
							length++;
						}

						yield return Token.DecimalLiteral(value);
					}
				}
				else if (char.IsLetter(startChar))
				{
					// Identifier
					while (true)
					{
						char c = AtOrNull(str, startIndex + length);
						if (!char.IsLetterOrDigit(c) && c != '_') break;
						++length;
					}
					yield return new Token(str.Substring(startIndex, length));
				}
				else throw new FormatException();

				startIndex += length;
			}
		}
	}
}
