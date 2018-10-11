using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Asmuth
{
	public interface IEmbeddedKeyCollection<T, TKey> : ICollection<T>
	{
		T this[TKey key] { get; }
		TKey GetKey(T item);
		bool TryFind(TKey key, out T item);
	}

	[DebuggerDisplay("Count={Count}")]
	public sealed class EmbeddedKeyCollection<T, TKey> : IEmbeddedKeyCollection<T, TKey>
	{
		private readonly Dictionary<TKey, T> items;
		private readonly Func<T, TKey> keyGetter;

		public EmbeddedKeyCollection(Func<T, TKey> keyGetter, IEqualityComparer<TKey> keyComparer)
		{
			this.keyGetter = keyGetter ?? throw new ArgumentNullException(nameof(keyGetter));
			this.items = new Dictionary<TKey, T>(keyComparer);
		}

		public EmbeddedKeyCollection(Func<T, TKey> keyGetter)
			: this(keyGetter, EqualityComparer<TKey>.Default) { }

		public int Count => items.Count;

		public T this[TKey key] => items[key];

		public void Add(T item) => items.Add(keyGetter(item), item);
		public void Clear() => items.Clear();
		public TKey GetKey(T item) => keyGetter(item);

		public bool Contains(T item)
			=> items.TryGetValue(keyGetter(item), out T value)
				&& EqualityComparer<T>.Default.Equals(value, item);

		public void CopyTo(T[] array, int arrayIndex)
			=> items.Values.CopyTo(array, arrayIndex);

		public Dictionary<TKey, T>.ValueCollection.Enumerator GetEnumerator()
			=> items.Values.GetEnumerator();

		public bool Remove(T item)
		{
			var key = keyGetter(item);
			return items.TryGetValue(key, out T value)
				&& EqualityComparer<T>.Default.Equals(value, item)
				&& items.Remove(key);
		}

		public bool TryFind(TKey key, out T item) => items.TryGetValue(key, out item);

		bool ICollection<T>.IsReadOnly => false;
		IEnumerator<T> IEnumerable<T>.GetEnumerator() => items.Values.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => items.Values.GetEnumerator();
	}

	public static class EmbeddedKeyCollectionExtensions
	{
		public static T Get<T, TKey>(this IEmbeddedKeyCollection<T, TKey> collection, TKey key) => collection[key];

		public static T GetOrDefault<T, TKey>(this IEmbeddedKeyCollection<T, TKey> collection, TKey key)
			=> collection.TryFind(key, out T item) ? item : default;

		public static T GetOrDefault<T, TKey>(this IEmbeddedKeyCollection<T, TKey> collection, TKey key, T defaultValue)
			=> collection.TryFind(key, out T item) ? item : defaultValue;

		public static T? GetNullable<T, TKey>(this IEmbeddedKeyCollection<T, TKey> collection, TKey key) where T : struct
			=> collection.TryFind(key, out T item) ? item : (T?)null;
	}
}
