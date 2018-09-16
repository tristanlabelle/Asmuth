using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Xed
{
	public static class XedBitPattern
	{
		public static bool IsConstant(string pattern)
		{
			foreach (var c in pattern)
				if (c != '0' && c != '1' && c != '_')
					return false;
			return true;
		}

		public static string Normalize(string pattern)
		{
			if (pattern == null) throw new ArgumentNullException(nameof(pattern));
			if (pattern.Length == 0) throw new ArgumentException();

			char[] normalizedChars = null;
			int normalizedLength = 0;
			for (int i = 0; i < pattern.Length; ++i)
			{
				char c = pattern[i];
				if (c == '_')
				{
					if (normalizedChars == null)
					{
						normalizedChars = new char[pattern.Length - 1];
						normalizedLength = i;
						pattern.CopyTo(0, normalizedChars, 0, normalizedLength);
					}
					continue;
				}

				if (normalizedChars != null)
				{
					normalizedChars[normalizedLength] = c;
					normalizedLength++;
				}

				if (c == '0' || c == '1') continue;
				if (c >= 'a' && c <= 'z')
				{
					if (i == 0 || pattern[i - 1] == c) continue;
					if (pattern.IndexOf(c) != i)
						throw new FormatException("Duplicate bit pattern variable.");
					continue;
				}

				throw new FormatException("Invalid bit pattern character.");
			}

			return normalizedChars == null ? pattern
				: new string(normalizedChars, 0, normalizedLength);
		}
	}
}
