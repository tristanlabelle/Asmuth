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
	public readonly struct ImmutableLegacyPrefixList : IList<LegacyPrefix>, IReadOnlyList<LegacyPrefix>
	{
		private const uint legacyPrefixCount = 11;

		private static readonly uint[] multiples =
		{
			1,
			legacyPrefixCount,
			legacyPrefixCount * legacyPrefixCount,
			legacyPrefixCount * legacyPrefixCount * legacyPrefixCount,
			legacyPrefixCount * legacyPrefixCount * legacyPrefixCount * legacyPrefixCount,
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
		public bool HasLock => Contains(LegacyPrefix.Lock);

		public SegmentRegister? SegmentOverride
		{
			get
			{
				var prefix = GetPrefixFromGroup(LegacyPrefixGroup.SegmentOverride);
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

		// Whether a byte actually is a SIMD prefix depends on the specific opcode
		public SimdPrefix PotentialSimdPrefix
		{
			get
			{
				if (IsEmpty) return SimdPrefix.None;
				switch (this[Count - 1])
				{
					case LegacyPrefix.OperandSizeOverride: return SimdPrefix._66;
					case LegacyPrefix.RepeatNotEqual: return SimdPrefix._F2;
					case LegacyPrefix.RepeatEqual: return SimdPrefix._F3;
					default: return SimdPrefix.None;
				}
			}
		}
		#endregion

		public LegacyPrefix this[int index]
			=> (LegacyPrefix)((storage & 0xFFFFFF) / multiples[index] % legacyPrefixCount);

		#region Methods
		public bool Contains(LegacyPrefix item) => IndexOf(item) >= 0;

		public bool EndsWith(LegacyPrefix item)
		{
			if (Count == 0) return false;
			return this[Count - 1] == item;
		}
		
		public LegacyPrefix? GetPrefixFromGroup(LegacyPrefixGroup group)
		{
			for (int i = 0; i < Count; ++i)
				if (this[i].GetGroup() == group)
					return this[i];
			return null;
		}

		public bool ContainsFromGroup(LegacyPrefixGroup group) => GetPrefixFromGroup(group).HasValue;

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

		#region Static Mutators
		public static ImmutableLegacyPrefixList SetAt(ImmutableLegacyPrefixList list, int index, LegacyPrefix item)
		{
			var group = item.GetGroup();
			for (int i = 0; i < list.Count; ++i)
				if (i != index && list[i].GetGroup() == group)
					throw new ArgumentException();

			var data = list.storage & 0xFFFFFF;
			uint multiple = multiples[index];
			data = (data / multiple / legacyPrefixCount * legacyPrefixCount
				+ (uint)item) * multiple + data % multiple;
			return new ImmutableLegacyPrefixList((list.storage & 0xFF000000) | data);
		}

		public static ImmutableLegacyPrefixList Add(ImmutableLegacyPrefixList list, LegacyPrefix item)
			=> Insert(list, list.Count, item);

		public static ImmutableLegacyPrefixList Insert(ImmutableLegacyPrefixList list, int index, LegacyPrefix item)
		{
			if (index < 0 || index > list.Count) throw new ArgumentOutOfRangeException(nameof(index));
			if (list.ContainsFromGroup(item.GetGroup())) throw new InvalidOperationException();

			uint data = list.storage & 0xFFFFFF;
			uint multiple = multiples[index];
			data = (data / multiple * legacyPrefixCount + (uint)item) * multiple + data % multiple;
			data |= (list.storage & 0xFF000000) + 0x01000000;
			return new ImmutableLegacyPrefixList(data);
		}

		public static ImmutableLegacyPrefixList Remove(ImmutableLegacyPrefixList list, LegacyPrefix item)
		{
			int index = list.IndexOf(item);
			return index < 0 ? list : RemoveAt(list, index);
		}

		public static ImmutableLegacyPrefixList RemoveAt(ImmutableLegacyPrefixList list, int index)
		{
			if (index < 0 || index >= list.Count) throw new ArgumentOutOfRangeException(nameof(index));

			uint newStorage = list.storage & 0xFFFFFF;
			uint multiple = multiples[index];
			newStorage = newStorage / multiple / legacyPrefixCount * multiple + newStorage % multiple;
			newStorage |= (list.storage & 0xFF000000) - 0x01000000;
			return new ImmutableLegacyPrefixList(newStorage);
		}

		public override string ToString()
		{
			var str = new StringBuilder();
			str.Append('[');
			for (int i = 0; i < Count; ++i)
			{
				if (i > 0) str.Append(", ");
				str.Append(this[i].GetMnemonicOrHexValue());
			}
			str.Append(']');
			return str.ToString();
		} 
		#endregion
		#endregion

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
}
