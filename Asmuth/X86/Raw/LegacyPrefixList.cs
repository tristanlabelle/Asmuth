using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	/// <summary>
	/// A list of legacy prefixes that an instruction can have.
	/// Does not allow multiple prefixes from the same group.
	/// </summary>
	[StructLayout(LayoutKind.Sequential, Size = sizeof(uint))]
	public struct ImmutableLegacyPrefixList : IList<LegacyPrefix>, IReadOnlyList<LegacyPrefix>
	{
		private static readonly LegacyPrefix[] lookup = new[]
		{
			LegacyPrefix.OperandSizeOverride,
			LegacyPrefix.AddressSizeOverride,
			LegacyPrefix.Lock,
			LegacyPrefix.RepeatNonZero,
			LegacyPrefix.RepeatZero,
			LegacyPrefix.CSSegmentOverride,
			LegacyPrefix.SSSegmentOverride,
			LegacyPrefix.DSSegmentOverride,
			LegacyPrefix.ESSegmentOverride,
			LegacyPrefix.FSSegmentOverride,
			LegacyPrefix.GSSegmentOverride,
		};

		public static readonly ImmutableLegacyPrefixList Empty;
		public const int Capacity = 5;

		// Top byte is count
		// Low bytes store items as multiples of lookup.Count
		private readonly uint storage;

		private ImmutableLegacyPrefixList(uint storage) { this.storage = storage; }

		#region Properties
		public int Count => (int)(storage >> 24);
		public bool IsEmpty => Count == 0;
		public LegacyPrefix? Tail => IsEmpty ? (LegacyPrefix?)null : this[Count - 1];
		#endregion

		public LegacyPrefix this[int index]
		{
			get
			{
				return lookup[(storage & 0xFFFFFF) / GetMultiple(index) % lookup.Length];
			}
			set { throw new NotImplementedException(); }
		}

		#region Methods
		public bool Contains(LegacyPrefix item) => IndexOf(item) >= 0;

		public InstructionFields GetGroups()
		{
			var count = Count;
			uint temp = storage & 0xFFFFFF;
			var groups = (InstructionFields)0;
			for (int i = 0; i < count;)
			{
				groups |= lookup[temp % lookup.Length].GetFieldOrNone();
				i++;
				temp /= (uint)lookup.Length;
			}
			return groups;
		}

		public bool ContainsGroups(InstructionFields fields)
		{
			var count = 0;
			uint temp = storage & 0xFFFFFF;
			for (int i = 0; i < count;)
			{
				fields &= ~lookup[temp % lookup.Length].GetFieldOrNone();
				if (fields == 0) return true;
				i++;
				temp /= (uint)lookup.Length;
			}
			return false;
		}

		public void CopyTo(LegacyPrefix[] array, int arrayIndex)
		{
			var count = 0;
			uint temp = storage & 0xFFFFFF;
			for (int i = 0; i < count;)
			{
				array[arrayIndex + i] = lookup[temp % lookup.Length];
				i++;
				temp /= (uint)lookup.Length;
			}
		}

		public int IndexOf(LegacyPrefix item)
		{
			var count = Count;
			uint temp = storage & 0xFFFFFF;
			for (int i = 0; i < count;)
			{
				if (lookup[temp % lookup.Length] == item)
					return i;
				i++;
				temp /= (uint)lookup.Length;
			}

			return -1;
		}

		public ImmutableLegacyPrefixList Add(LegacyPrefix item) => Insert(Count, item);
		public ImmutableLegacyPrefixList Clear() => Empty;

		public ImmutableLegacyPrefixList Insert(int index, LegacyPrefix item)
		{
			if (index < 0 || index > Count) throw new ArgumentOutOfRangeException(nameof(index));
			var field = item.GetFieldOrNone();
			if (field == InstructionFields.None) throw new ArgumentException();
			if (ContainsGroups(field)) throw new InvalidOperationException();

			uint newStorage = storage & 0xFFFFFF;
			uint multiple = GetMultiple(index);
			newStorage = (newStorage / multiple * (uint)lookup.Length + ToIndex(item)) * multiple + newStorage % multiple;
			newStorage |= (storage & 0xFF000000) + 0x01000000;
			return new ImmutableLegacyPrefixList(newStorage);
		}

		public ImmutableLegacyPrefixList Remove(LegacyPrefix item)
		{
			int index = IndexOf(item);
			return index < 0 ? this : RemoveAt(index);
		}

		public ImmutableLegacyPrefixList RemoveAt(int index)
		{
			if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));

			uint newStorage = storage & 0xFFFFFF;
			uint multiple = GetMultiple(index);
			newStorage = newStorage / multiple / (uint)lookup.Length * multiple + newStorage % multiple;
			newStorage |= (storage & 0xFF000000) - 0x01000000;
			return new ImmutableLegacyPrefixList(newStorage);
		}

		public override string ToString()
		{
			var str = new StringBuilder();
			str.Append('[');
			for (int i = 0; i < Count; ++i)
			{
				if (i > 0) str.Append(", ");
				str.AppendFormat(CultureInfo.InvariantCulture, "X2", (byte)this[i]);
			}
			str.Append(']');
			return str.ToString();
		}

		private static uint GetMultiple(int index)
		{
			if (index > Capacity) throw new ArgumentOutOfRangeException(nameof(index));
			uint multiple = 1;
			while (index > 0)
			{
				multiple *= 7;
				index--;
			}
			return multiple;
		}

		private static uint ToIndex(LegacyPrefix prefix)
		{
			int index = Array.IndexOf(lookup, prefix);
			if (index < 0) throw new ArgumentException(nameof(prefix));
			return (uint)index;
		}
		#endregion

		public static implicit operator ImmutableLegacyPrefixList(LegacyPrefixList list) => list.ToImmutable();

		bool ICollection<LegacyPrefix>.IsReadOnly => true;
		void ICollection<LegacyPrefix>.Add(LegacyPrefix prefix) { throw new NotSupportedException(); }
		bool ICollection<LegacyPrefix>.Remove(LegacyPrefix prefix) { throw new NotSupportedException(); }
		void ICollection<LegacyPrefix>.Clear() { throw new NotSupportedException(); }
		int IList<LegacyPrefix>.IndexOf(LegacyPrefix prefix) { throw new NotSupportedException(); }
		void IList<LegacyPrefix>.Insert(int index, LegacyPrefix prefix) { throw new NotSupportedException(); }
		void IList<LegacyPrefix>.RemoveAt(int index) { throw new NotSupportedException(); }
		IEnumerator<LegacyPrefix> IEnumerable<LegacyPrefix>.GetEnumerator() { throw new NotImplementedException(); }
		IEnumerator IEnumerable.GetEnumerator() { throw new NotImplementedException(); }
	}

	public sealed class LegacyPrefixList : IList<LegacyPrefix>, IReadOnlyList<LegacyPrefix>
	{
		public const int Capacity = ImmutableLegacyPrefixList.Capacity;

		private ImmutableLegacyPrefixList list;

		public LegacyPrefixList() { }
		public LegacyPrefixList(ImmutableLegacyPrefixList list) { this.list = list; }

		public int Count => list.Count;
		public bool IsEmpty => list.IsEmpty;
		public LegacyPrefix? Tail => list.Tail;

		public LegacyPrefix this[int index]
		{
			get { return list[index]; }
			set { throw new NotImplementedException(); }
		}

		public bool Contains(LegacyPrefix prefix) => list.Contains(prefix);
		public int IndexOf(LegacyPrefix prefix) => list.IndexOf(prefix);
		public void CopyTo(LegacyPrefix[] array, int arrayIndex) => list.CopyTo(array, arrayIndex);
		public void Add(LegacyPrefix prefix) => list = list.Add(prefix);
		public void RemoveAt(int index) => list = list.RemoveAt(index);
		public void Clear() => list = list.Clear();

		public bool Remove(LegacyPrefix prefix)
		{
			int index = list.IndexOf(prefix);
			if (index < 0) return false;
			list = list.RemoveAt(index);
			return true;
		}

		public ImmutableLegacyPrefixList ToImmutable() => list;

		public override string ToString() => list.ToString();

		public static implicit operator LegacyPrefixList(ImmutableLegacyPrefixList list) => new LegacyPrefixList(list);

		bool ICollection<LegacyPrefix>.IsReadOnly => false;
		void IList<LegacyPrefix>.Insert(int index, LegacyPrefix prefix) { throw new NotSupportedException(); }
		IEnumerator<LegacyPrefix> IEnumerable<LegacyPrefix>.GetEnumerator() { throw new NotImplementedException(); }
		IEnumerator IEnumerable.GetEnumerator() { throw new NotImplementedException(); }
	}
}
