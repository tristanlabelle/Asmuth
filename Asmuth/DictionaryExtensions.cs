using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth
{
	public static class DictionaryExtensions
	{
		public static TValue GetValueOrDefault<TKey, TValue>(
			this IDictionary<TKey, TValue> dictionary, TKey key)
		{
			return dictionary.TryGetValue(key, out TValue value) ? value : default;
		}

		public static TValue GetValueOrDefault<TKey, TValue>(
			this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue)
		{
			return dictionary.TryGetValue(key, out TValue value) ? value : defaultValue;
		}

		public static TValue? GetValueAsNullable<TKey, TValue>(
			this IDictionary<TKey, TValue> dictionary, TKey key) where TValue : struct
		{
			return dictionary.TryGetValue(key, out TValue value) ? value : (TValue?)null;
		}
	}
}
