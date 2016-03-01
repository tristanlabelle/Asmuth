using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	/// <summary>
	/// A list of legacy prefixes that an instruction can have.
	/// Does not allow multiple prefixes from the same group.
	/// </summary>
	[StructLayout(LayoutKind.Sequential, Size = sizeof(uint))]
	public struct ImmutableLegacyPrefixList : IList<LegacyPrefix>, IReadOnlyList<LegacyPrefix>
	{
		private static readonly LegacyPrefix[] lookup =
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

		private static readonly uint[] multiples =
		{
			1,
			(uint)lookup.Length,
			(uint)(lookup.Length * lookup.Length),
			(uint)(lookup.Length * lookup.Length * lookup.Length),
			(uint)(lookup.Length * lookup.Length * lookup.Length * lookup.Length),
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
		public bool HasOperandSizeOverride => Contains(LegacyPrefix.OperandSizeOverride);
		public bool HasAddressSizeOverride => Contains(LegacyPrefix.AddressSizeOverride);

		public SegmentRegister? SegmentOverride
		{
			get
			{
				var prefix = GetPrefixFromGroup(InstructionFields.LegacySegmentOverride);
				if (!prefix.HasValue) return null;
				switch (prefix.Value)
				{
					case LegacyPrefix.CSSegmentOverride: return SegmentRegister.CS;
					case LegacyPrefix.DSSegmentOverride: return SegmentRegister.DS;
					case LegacyPrefix.ESSegmentOverride: return SegmentRegister.ES;
					case LegacyPrefix.FSSegmentOverride: return SegmentRegister.FS;
					case LegacyPrefix.GSSegmentOverride: return SegmentRegister.GS;
					case LegacyPrefix.SSSegmentOverride: return SegmentRegister.SS;
					default: throw new UnreachableException();
				}
			}
		}
		#endregion

		public LegacyPrefix this[int index]
			=> lookup[(storage & 0xFFFFFF) / multiples[index] % lookup.Length];

		#region Methods
		public SimdPrefix GetSimdPrefix(OpcodeMap map)
		{
			if (IsEmpty || map == OpcodeMap.Default) return SimdPrefix.None;
			switch (this[Count - 1])
			{
				case LegacyPrefix.OperandSizeOverride: return SimdPrefix._66;
				case LegacyPrefix.RepeatNotEqual: return SimdPrefix._F2;
				case LegacyPrefix.RepeatEqual: return SimdPrefix._F3;
				default: return SimdPrefix.None;
			}
		}

		public bool Contains(LegacyPrefix item) => IndexOf(item) >= 0;

		public InstructionFields GetGroups()
		{
			var groups = (InstructionFields)0;
			for (int i = 0; i < Count; ++i)
				groups |= this[i].GetFieldOrNone();
			return groups;
		}
		
		public LegacyPrefix? GetPrefixFromGroup(InstructionFields group)
		{
			for (int i = 0; i < Count; ++i)
				if (this[i].GetFieldOrNone() == group)
					return this[i];
			return null;
		}

		public void CopyTo(LegacyPrefix[] array, int arrayIndex)
		{
			for (int i = 0; i < Count; ++i)
				array[arrayIndex + i] = this[i];
		}

		public int IndexOf(LegacyPrefix item)
		{
			for (int i = 0; i < Count; ++i)
				if (this[i] == item)
					return i;
			return -1;
		}

		public ImmutableLegacyPrefixList SetAt(int index, LegacyPrefix item)
		{
			var group = item.GetFieldOrNone();
			for (int i = 0; i < Count; ++i)
				if (i != index && this[i].GetFieldOrNone() == group)
					throw new ArgumentException();

			var data = storage & 0xFFFFFF;
			uint multiple = multiples[index];
			data = (data / multiple / (uint)lookup.Length * (uint)lookup.Length
				+ ToIndex(item)) * multiple + data % multiple;
			return new ImmutableLegacyPrefixList((storage & 0xFF000000) | data);
		}

		public ImmutableLegacyPrefixList Add(LegacyPrefix item) => Insert(Count, item);
		public ImmutableLegacyPrefixList Clear() => Empty;

		public ImmutableLegacyPrefixList Insert(int index, LegacyPrefix item)
		{
			if (index < 0 || index > Count) throw new ArgumentOutOfRangeException(nameof(index));
			var group = item.GetFieldOrNone();
			if (group == InstructionFields.None) throw new ArgumentException();
			if ((GetGroups() & group) != 0) throw new InvalidOperationException();

			uint data = storage & 0xFFFFFF;
			uint multiple = multiples[index];
			data = (data / multiple * (uint)lookup.Length + ToIndex(item)) * multiple + data % multiple;
			data |= (storage & 0xFF000000) + 0x01000000;
			return new ImmutableLegacyPrefixList(data);
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
			uint multiple = multiples[index];
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

		private static uint ToIndex(LegacyPrefix prefix)
		{
			int index = Array.IndexOf(lookup, prefix);
			if (index < 0) throw new ArgumentException(nameof(prefix));
			return (uint)index;
		}
		#endregion

		public static implicit operator ImmutableLegacyPrefixList(LegacyPrefixList list) => list.ToImmutable();

		LegacyPrefix IList<LegacyPrefix>.this[int index]
		{
			get { return this[index]; }
			set { throw new NotSupportedException(); }
		}

		int IList<LegacyPrefix>.IndexOf(LegacyPrefix prefix) { throw new NotSupportedException(); }
		void IList<LegacyPrefix>.Insert(int index, LegacyPrefix prefix) { throw new NotSupportedException(); }
		void IList<LegacyPrefix>.RemoveAt(int index) { throw new NotSupportedException(); }
		bool ICollection<LegacyPrefix>.IsReadOnly => true;
		void ICollection<LegacyPrefix>.Add(LegacyPrefix prefix) { throw new NotSupportedException(); }
		bool ICollection<LegacyPrefix>.Remove(LegacyPrefix prefix) { throw new NotSupportedException(); }
		void ICollection<LegacyPrefix>.Clear() { throw new NotSupportedException(); }
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
		public bool HasOperandSizeOverride => list.HasOperandSizeOverride;
		public bool HasAddressSizeOverride => list.HasAddressSizeOverride;
		public SegmentRegister? SegmentOverride => list.SegmentOverride;

		public LegacyPrefix this[int index]
		{
			get { return list[index]; }
			set { list = list.SetAt(index, value); }
		}

		public SimdPrefix GetSimdPrefix(OpcodeMap map) => list.GetSimdPrefix(map);
		public LegacyPrefix? GetPrefixFromGroup(InstructionFields group) => list.GetPrefixFromGroup(group);
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
