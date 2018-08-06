using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth
{
	partial class MultiDictionary<TKey, TValue>
	{
		public struct ValueCollection : ICollection<TValue>, IReadOnlyCollection<TValue>
		{
			private readonly MultiDictionary<TKey, TValue> dictionary;
			private readonly TKey key;

			internal ValueCollection(MultiDictionary<TKey, TValue> dictionary, TKey key)
			{
				this.dictionary = dictionary;
				this.key = key;
			}

			public int Count
			{
				get
				{
					ICollection<TValue> values;
					return dictionary.items.TryGetValue(key, out values) ? values.Count : 0;
				}
			}

			public bool Contains(TValue value)
			{
				ICollection<TValue> values;
				return dictionary.items.TryGetValue(key, out values) && values.Contains(value);
			}

			public void CopyTo(TValue[] array, int arrayIndex)
			{
				ICollection<TValue> values;
				if (dictionary.items.TryGetValue(key, out values))
					values.CopyTo(array, arrayIndex);
			}

			public IEnumerator<TValue> GetEnumerator()
			{
				ICollection<TValue> values;
				var enumerable = dictionary.items.TryGetValue(key, out values)
					? values : EmptyArray<TValue>.Rank1;
				return enumerable.GetEnumerator();
			}

			public void Add(TValue value)
			{
				ICollection<TValue> values;
				if (!dictionary.items.TryGetValue(key, out values))
				{
					values = dictionary.valueCollectionFactory();
					Debug.Assert(values is IReadOnlyCollection<TValue>);
					dictionary.items.Add(key, values);
				}
				values.Add(value);
			}

			public bool Remove(TValue value)
			{
				ICollection<TValue> values;
				if (!dictionary.items.TryGetValue(key, out values) || !values.Remove(value))
					return false;

				if (values.Count == 0) dictionary.items.Remove(key);
				return true;
			}

			public void Clear()
			{
				dictionary.items.Remove(key);
			}

			bool ICollection<TValue>.IsReadOnly => false;
			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		}
	}
}
