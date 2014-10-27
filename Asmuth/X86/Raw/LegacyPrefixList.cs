using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	/// <summary>
	/// A list of legacy prefixes that an instruction can have.
	/// Does not allow multiple prefixes from the same group.
	/// </summary>
	public struct LegacyPrefixList : IList<LegacyPrefix>, IReadOnlyList<LegacyPrefix>
	{
		public const int Capacity = 5;

		// Bytes 0, 1, 2, 3, 4 can contain prefixes, byte 7 is the count
		private ulong storage;

		public LegacyPrefix this[int index]
		{
			get { return (LegacyPrefix)((storage >> (index * 8)) & 0xFF); }
			set { throw new NotImplementedException(); }
		}

		#region Properties
		public int Count => (int)(storage >> 56);
		public bool IsEmpty => Count == 0;

		public LegacyPrefix Last
		{
			get
			{
				Contract.Requires(!IsEmpty);
				return this[Count - 1];
			}
		}
		#endregion

		#region Methods
		public void Add(LegacyPrefix item) => Insert(Count, item);
		public void Clear() => storage = 0;
		public bool Contains(LegacyPrefix item) => IndexOf(item) >= 0;

		public bool ContainsGroups(InstructionFields fields)
		{
			var count = Count;
			for (int i = 0; i < count; ++i)
				fields &= ~this[count].GetFieldOrNone();
			return fields == 0;
		}

		public void CopyTo(LegacyPrefix[] array, int arrayIndex)
		{
			throw new NotImplementedException();
		}

		public IEnumerator<LegacyPrefix> GetEnumerator()
		{
			throw new NotImplementedException();
		}

		public int IndexOf(LegacyPrefix item)
		{
			var count = Count;
			for (int i = 0; i < count; ++i)
				if (this[i] == item)
					return i;
			return -1;
		}

		public void Insert(int index, LegacyPrefix item)
		{
			var field = item.GetFieldOrNone();
			if (field == InstructionFields.None) throw new ArgumentException();
			if (ContainsGroups(field)) throw new InvalidOperationException();
			storage += 1UL << 56;  // Increment the count
			throw new NotImplementedException();
		}

		public bool Remove(LegacyPrefix item)
		{
			int index = IndexOf(item);
			if (index < 0) return false;
			RemoveAt(index);
			return true;
		}

		public void RemoveAt(int index)
		{
			throw new NotImplementedException();
		}
		#endregion

		bool ICollection<LegacyPrefix>.IsReadOnly => false;
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
