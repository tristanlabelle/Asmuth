using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Asmuth
{
	public sealed class SmallDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
	{
		private readonly List<KeyValuePair<TKey, TValue>> entries = new List<KeyValuePair<TKey, TValue>>();
		private readonly IEqualityComparer<TKey> keyComparer;

		public SmallDictionary(IEqualityComparer<TKey> keyComparer)
		{
			this.keyComparer = keyComparer ?? throw new ArgumentNullException(nameof(keyComparer));
		}

		public SmallDictionary() : this(EqualityComparer<TKey>.Default) { }

		public TValue this[TKey key]
		{
			get => entries[GetIndex(key)].Value;
			set
			{
				if (TryGetIndex(key, out int index))
					entries[index] = new KeyValuePair<TKey, TValue>(key, value);
				else
					entries.Add(new KeyValuePair<TKey, TValue>(key, value));
			}
		}

		public ICollection<TKey> Keys => new MappedListView<KeyValuePair<TKey, TValue>, TKey>(entries, p => p.Key);
		public ICollection<TValue> Values => new MappedListView<KeyValuePair<TKey, TValue>, TValue>(entries, p => p.Value);
		public int Count => entries.Count;

		public void Add(TKey key, TValue value)
		{
			if (ContainsKey(key)) throw new ArgumentException();
			entries.Add(new KeyValuePair<TKey, TValue>(key, value));
		}

		public void Clear() => entries.Clear();

		public bool ContainsKey(TKey key) => FindIndex(key) >= 0;

		public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
		{
			throw new NotImplementedException();
		}

		List<KeyValuePair<TKey, TValue>>.Enumerator GetEnumerator() => entries.GetEnumerator();

		public bool Remove(TKey key)
		{
			int index = FindIndex(key);
			if (index < 0) return false;
			entries.SwapRemoveAt(index);
			return true;
		}

		public bool TryGetValue(TKey key, out TValue value)
		{
			int index = FindIndex(key);
			if (index < 0)
			{
				value = default;
				return false;
			}

			value = entries[index].Value;
			return true;
		}

		private int FindIndex(TKey key)
		{
			for (int i = 0; i < entries.Count; ++i)
				if (keyComparer.Equals(entries[i].Key, key))
					return i;
			return -1;
		}

		private int GetIndex(TKey key)
		{
			int index = FindIndex(key);
			if (index < 0) throw new KeyNotFoundException(nameof(key));
			return index;
		}
		
		private bool TryGetIndex(TKey key, out int index)
		{
			index = FindIndex(key);
			return index >= 0;
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
			=> GetEnumerator();
		bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;
		void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
			=> Add(item.Key, item.Value);
		bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
			=> TryGetIndex(item.Key, out int index)
			&& EqualityComparer<TValue>.Default.Equals(item.Value, entries[index].Value);
		bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
		{
			int index = FindIndex(item.Key);
			if (index < 0) return false;
			var entry = entries[index];
			if (!EqualityComparer<TValue>.Default.Equals(item.Value, entry.Value)) return false;
			entries.SwapRemoveAt(index);
			return true;
		}
		IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Keys;
		IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Values;
	}
}
