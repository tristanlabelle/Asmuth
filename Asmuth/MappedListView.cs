using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Asmuth
{
	public sealed class MappedListView<T, U> : IList<U>, IReadOnlyList<U>
	{
		public struct Enumerator : IEnumerator<U>
		{
			private readonly IEnumerator<T> underlying;
			private readonly Func<T, U> mapping;

			internal Enumerator(IEnumerator<T> underlying, Func<T, U> mapping)
			{
				this.underlying = underlying;
				this.mapping = mapping;
			}

			public U Current => mapping(underlying.Current);

			public void Dispose() => underlying.Dispose();
			public bool MoveNext() => underlying.MoveNext();
			public void Reset() => underlying.Reset();

			object IEnumerator.Current => Current;
		}

		private readonly IReadOnlyList<T> underlying;
		private readonly Func<T, U> mapping;

		public MappedListView(IReadOnlyList<T> underlying, Func<T, U> mapping)
		{
			this.underlying = underlying ?? throw new ArgumentNullException(nameof(underlying));
			this.mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
		}

		public U this[int index] => mapping(underlying[index]);

		public int Count => underlying.Count;

		public bool Contains(U item) => IndexOf(item) >= 0;

		public void CopyTo(U[] array, int arrayIndex)
		{
			for (int i = 0; i < underlying.Count; ++i)
				array[arrayIndex + i] = mapping(underlying[i]);
		}

		public Enumerator GetEnumerator() => new Enumerator(underlying.GetEnumerator(), mapping);

		public int IndexOf(U item)
		{
			var comparer = EqualityComparer<U>.Default;
			for (int i = 0; i < underlying.Count; ++i)
				if (comparer.Equals(item, mapping(underlying[i])))
					return i;
			return -1;
		}
		
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		IEnumerator<U> IEnumerable<U>.GetEnumerator() => GetEnumerator();
		bool ICollection<U>.IsReadOnly => true;
		void ICollection<U>.Add(U item) => throw new NotSupportedException();
		void ICollection<U>.Clear() => throw new NotSupportedException();
		bool ICollection<U>.Remove(U item) => throw new NotSupportedException();
		U IList<U>.this[int index] { get => this[index]; set => throw new NotSupportedException(); }
		void IList<U>.Insert(int index, U item) => throw new NotSupportedException();
		void IList<U>.RemoveAt(int index) => throw new NotSupportedException();
	}
}
