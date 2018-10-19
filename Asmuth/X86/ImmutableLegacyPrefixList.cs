using System;
using System.Collections;
using System.Collections.Generic;
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
		// The Intel Instruction Set Reference Manual says
		// "Instruction prefixes are divided into four groups, each with a set of allowable prefix codes.
		// For each instruction, it is only useful to include up to one prefix code from each of the four groups
		// (Groups 1, 2, 3, 4). Groups 1 through 4 may be placed in any order relative to each other."

		// Multiple prefixes of the same group are legal, merely not "useful", although this
		// is contradicted by the "XAquire/XRelease Lock" sequence, both of which are from intel's group 1.
		// A prefix can even be repeated, such as in "66 66 90" (a wide NOP sequence seen in the wild).
		// When multiple prefixes are not supported, this should not be a decode failure but higher-level
		// failure (#UD when executed).

		// Hence we need to support an arbitrary list of prefixes, of at least length 4, though this
		// may be only bounded by the maximum instruction length of 15 bytes (so 14 legacy prefixes).
		// We allocate 32 bits to storing this list, and use one nibble per prefix, leaving the
		// top one to use as a count.
		public const int Capacity = 7;
		
		private const int countShift = 28;
		private const uint countUnit = 1U << countShift;
		private const uint itemsMask = countUnit - 1;
		private const uint countMask = ~itemsMask;

		private readonly uint data; // Top bits for count, rest for items stored right to left

		private ImmutableLegacyPrefixList(uint storage) { this.data = storage; }

		#region Properties
		private uint itemsData => data & itemsMask;
		public uint countData => data & countMask;
		public int Count => (int)(data >> countShift);
		public bool IsEmpty => Count == 0;
		public bool HasOperandSizeOverride => Contains(LegacyPrefix.OperandSizeOverride);
		public bool HasAddressSizeOverride => Contains(LegacyPrefix.AddressSizeOverride);
		public bool HasLock => Contains(LegacyPrefix.Lock);

		public SegmentRegister? SegmentOverride
		{
			get
			{
				var prefix = GetLastPrefixFromGroup(LegacyPrefixGroup.SegmentOverride);
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
				// According to the VS disassembler,
				// The last SIMD prefix will win for decoding,
				// although an invalid instruction interrupt will certainly be produced.
				for (int i = Count - 1; i >= 0; --i)
				{
					var prefix = this[i];
					if (prefix == LegacyPrefix.OperandSizeOverride) return SimdPrefix._66;
					if (prefix == LegacyPrefix.RepeatNotEqual) return SimdPrefix._F2;
					if (prefix == LegacyPrefix.RepeatEqual) return SimdPrefix._F3;
				}
				return SimdPrefix.None;
			}
		}
		#endregion

		public LegacyPrefix this[int index]
		{
			get
			{
				if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
				return (LegacyPrefix)((data >> (index * 4)) & 0xF);
			}
		}

		#region Methods
		public bool Contains(LegacyPrefix item) => IndexOf(item) >= 0;
		
		public LegacyPrefix? GetLastPrefixFromGroup(LegacyPrefixGroup group)
		{
			for (int i = Count - 1; i >= 0; --i)
			{
				var prefix = this[i];
				if (prefix.GetGroup() == group)
					return prefix;
			}
			return null;
		}

		public bool ContainsFromGroup(LegacyPrefixGroup group)
			=> GetLastPrefixFromGroup(group).HasValue;

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
		
		public static readonly ImmutableLegacyPrefixList Empty;

		#region Static Mutators
		public static ImmutableLegacyPrefixList SetAt(ImmutableLegacyPrefixList list, int index, LegacyPrefix item)
		{
			if ((int)item > 0xF) throw new ArgumentOutOfRangeException(nameof(item));

			var data = list.data;
			data &= ~(0xFU << (index * 4)); // Clear item
			data |= (uint)item << (index * 4); // Set item

			return new ImmutableLegacyPrefixList(data);
		}

		public static ImmutableLegacyPrefixList Add(ImmutableLegacyPrefixList list, LegacyPrefix item)
			=> Insert(list, list.Count, item);

		public static ImmutableLegacyPrefixList Insert(ImmutableLegacyPrefixList list, int index, LegacyPrefix item)
		{
			if ((uint)index > (uint)list.Count) throw new ArgumentOutOfRangeException(nameof(index));
			if (list.Count == Capacity) throw new InvalidOperationException();
			
			var itemsData = list.itemsData;
			var shift = index * 4;
			itemsData = ((itemsData >> shift) << (shift + 4)) // Preserve items to the left and shift left
				| ((uint)item << shift) // Add new item
				| (itemsData & ((1U << shift) - 1)); // Preserve items to the right

			return new ImmutableLegacyPrefixList((list.countData + countUnit) | itemsData);
		}

		public static ImmutableLegacyPrefixList Remove(ImmutableLegacyPrefixList list, LegacyPrefix item)
		{
			int index = list.IndexOf(item);
			return index < 0 ? list : RemoveAt(list, index);
		}

		public static ImmutableLegacyPrefixList RemoveAt(ImmutableLegacyPrefixList list, int index)
		{
			if ((uint)index >= (uint)list.Count) throw new ArgumentOutOfRangeException(nameof(index));

			var shift = index * 4;
			var itemsData = list.itemsData;
			itemsData = ((itemsData >> (shift + 4)) << shift) // Preserve items to the left and shift right
				| (itemsData & ((1U << shift) - 1)); // Preserve items to the right

			return new ImmutableLegacyPrefixList((list.countData - countUnit) | itemsData);
		}

		public override string ToString()
		{
			var str = new StringBuilder();
			str.Append('[');
			for (int i = 0; i < Count; ++i)
			{
				if (i > 0) str.Append(", ");
				str.Append(this[i].GetMnemonic());
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
