using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth
{
	/// <summary>
	/// A dictionary collection which can contain multiple values per key.
	/// </summary>
	public sealed partial class MultiDictionary<TKey, TValue> :
		IDictionary<TKey, ICollection<TValue>>,
		IReadOnlyDictionary<TKey, IReadOnlyCollection<TValue>>
	{
		private readonly Dictionary<TKey, ICollection<TValue>> items;
		private readonly Func<ICollection<TValue>> valueCollectionFactory;

		public MultiDictionary()
		{
			items = new Dictionary<TKey, ICollection<TValue>>();
			valueCollectionFactory = () => new List<TValue>();
		}

		public ICollection<TKey> Keys => items.Keys;

		public int KeyCount
		{
			get { return items.Count; }
		}

		public ValueCollection this[TKey key]
		{
			get { return new ValueCollection(this, key); }
		}

		public IEnumerator<KeyValuePair<TKey, ICollection<TValue>>> GetEnumerator() => items.GetEnumerator();

		public void Add(TKey key, TValue value) => this[key].Add(value);
		public bool ContainsKey(TKey key) => items.ContainsKey(key);
		public bool Remove(TKey key) => items.Remove(key);

		public void Clear() => items.Clear();

		#region Explicit Interface Implementations
		int ICollection<KeyValuePair<TKey, ICollection<TValue>>>.Count => KeyCount;
		int IReadOnlyCollection<KeyValuePair<TKey, IReadOnlyCollection<TValue>>>.Count => KeyCount;
		bool ICollection<KeyValuePair<TKey, ICollection<TValue>>>.IsReadOnly => false;
		ICollection<ICollection<TValue>> IDictionary<TKey, ICollection<TValue>>.Values => items.Values;
		IEnumerable<TKey> IReadOnlyDictionary<TKey, IReadOnlyCollection<TValue>>.Keys => items.Keys;

		IEnumerable<IReadOnlyCollection<TValue>> IReadOnlyDictionary<TKey, IReadOnlyCollection<TValue>>.Values
		{
			get { throw new NotImplementedException(); }
		}

		void ICollection<KeyValuePair<TKey, ICollection<TValue>>>.Add(KeyValuePair<TKey, ICollection<TValue>> item)
		{
			throw new NotSupportedException();
		}

		bool ICollection<KeyValuePair<TKey, ICollection<TValue>>>.Remove(KeyValuePair<TKey, ICollection<TValue>> item)
		{
			throw new NotSupportedException();
		}

		bool ICollection<KeyValuePair<TKey, ICollection<TValue>>>.Contains(KeyValuePair<TKey, ICollection<TValue>> item)
		{
			throw new NotSupportedException();
		}

		void ICollection<KeyValuePair<TKey, ICollection<TValue>>>.CopyTo(KeyValuePair<TKey, ICollection<TValue>>[] array, int arrayIndex)
		{
			throw new NotImplementedException();
		}
		
		ICollection<TValue> IDictionary<TKey, ICollection<TValue>>.this[TKey key]
		{
			get
			{
				if (!ContainsKey(key)) throw new KeyNotFoundException();
				return this[key];
			}
			set { throw new NotSupportedException(); }
		}

		IReadOnlyCollection<TValue> IReadOnlyDictionary<TKey, IReadOnlyCollection<TValue>>.this[TKey key]
		{
			get
			{
				if (!ContainsKey(key)) throw new KeyNotFoundException();
				return this[key];
			}
		}

		void IDictionary<TKey, ICollection<TValue>>.Add(TKey key, ICollection<TValue> value)
		{
			throw new NotSupportedException();
		}
		
		bool IDictionary<TKey, ICollection<TValue>>.TryGetValue(TKey key, out ICollection<TValue> value)
		{
			throw new NotSupportedException();
		}

		bool IReadOnlyDictionary<TKey, IReadOnlyCollection<TValue>>.TryGetValue(TKey key, out IReadOnlyCollection<TValue> value)
		{
			throw new NotSupportedException();
		}

		IEnumerator<KeyValuePair<TKey, IReadOnlyCollection<TValue>>> IEnumerable<KeyValuePair<TKey, IReadOnlyCollection<TValue>>>.GetEnumerator()
		{
			foreach (var pair in items)
				yield return new KeyValuePair<TKey, IReadOnlyCollection<TValue>>(
					pair.Key, (IReadOnlyCollection<TValue>)pair.Value);
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		#endregion
	}
}
