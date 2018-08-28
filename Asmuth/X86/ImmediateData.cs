using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86
{
	// Represents an untyped immediate value associated with an instruction
    public readonly struct ImmediateData : IReadOnlyList<byte>, IList<byte>, IEquatable<ImmediateData>
    {
		public const int MaxSizeInBytes = 8;
		public const int MaxSizeInBits = 64;

		// Bytes 0-7 of immediate stored as 0x7766554433221100
		private readonly ulong rawStorage;
		private readonly byte sizeInBytes;

		public ulong RawStorage => rawStorage;
		public int SizeInBytes => sizeInBytes;
		public int SizeInBits => sizeInBytes * 8;

		private ImmediateData(ulong bytes, int sizeInBytes)
			=> (this.rawStorage, this.sizeInBytes) = (bytes, (byte)sizeInBytes);

		#region Methods
		public byte GetByte(int index)
		{
			if (index >= sizeInBytes) throw new IndexOutOfRangeException();
			return (byte)((rawStorage >> (index * 8)) & 0xFF);
		}

		public byte AsUInt8()
		{
			if (sizeInBytes != 1) throw new InvalidOperationException();
			return (byte)rawStorage;
		}

		public ushort AsUInt16()
		{
			if (sizeInBytes != 2) throw new InvalidOperationException();
			return (ushort)(rawStorage & 0xFFFF);
		}

		public uint AsUInt32()
		{
			if (sizeInBytes != 4) throw new InvalidOperationException();
			return (uint)(rawStorage & 0xFFFFFFFFU);
		}

		public ulong AsUInt64()
		{
			if (sizeInBytes != 8) throw new InvalidOperationException();
			return rawStorage;
		}

		public ulong AsUInt(IntegerSize size)
		{
			if (size == IntegerSize.Byte) return AsUInt8();
			if (size == IntegerSize.Word) return AsUInt16();
			if (size == IntegerSize.Dword) return AsUInt32();
			if (size == IntegerSize.Qword) return AsUInt64();
			throw new ArgumentOutOfRangeException(nameof(size));
		}

		public sbyte AsInt8() => unchecked((sbyte)AsUInt8());
		public short AsInt16() => unchecked((short)AsUInt16());
		public int AsInt32() => unchecked((int)AsUInt32());
		public long AsInt64() => unchecked((long)rawStorage);
		
		public long AsInt(IntegerSize size)
		{
			if (size == IntegerSize.Byte) return AsInt8();
			if (size == IntegerSize.Word) return AsInt16();
			if (size == IntegerSize.Dword) return AsInt32();
			if (size == IntegerSize.Qword) return AsInt64();
			throw new ArgumentOutOfRangeException(nameof(size));
		}

		public (ushort, ushort) AsFarPtr16()
		{
			if (sizeInBytes != 4) throw new InvalidOperationException();
			// Our encoding/endianness means that this is reversed
			return ((ushort)rawStorage, (ushort)(rawStorage >> 16));
		}

		public (ushort, uint) AsFarPtr32()
		{
			if (sizeInBytes != 6) throw new InvalidOperationException();
			// Our encoding/endianness means that this is reversed
			return ((ushort)rawStorage, (uint)(rawStorage >> 16));
		}

		public byte[] AsBytes()
		{
			var array = new byte[sizeInBytes];
			CopyBytes(array, arrayIndex: 0);
			return array;
		}

		public void CopyBytes(byte[] array, int arrayIndex)
		{
			ulong temp = rawStorage;
			for (int i = 0; i < sizeInBytes; ++i)
			{
				array[arrayIndex] = (byte)(temp & 0xFF);
				arrayIndex++;
				temp >>= 8;
			}
		}

		public bool Equals(ImmediateData other)
			=> sizeInBytes == other.sizeInBytes && rawStorage == other.rawStorage;

		public override bool Equals(object obj)
			=> obj is ImmediateData && Equals((ImmediateData)obj);

		public override int GetHashCode()
			=> ((int)sizeInBytes << 25) ^ rawStorage.GetHashCode();

		private int IndexOf(byte item)
		{
			ulong temp = rawStorage;
			for (int i = 0; i < sizeInBytes; ++i)
			{
				if ((byte)(temp & 0xFF) == item) return i;
				temp >>= 8;
			}
			return -1;
		}
		#endregion

		#region Static Methods
		// Technically there are no 5 or 7-byte immediates, but allow them
		public static bool IsValidSize(int size) => unchecked((uint)size) <= 8;

		public static bool Equals(ImmediateData lhs, ImmediateData rhs) => lhs.Equals(rhs);

		public static ImmediateData FromByte(byte value) => new ImmediateData(value, 1);
		public static ImmediateData FromBytes(byte a, byte b)
			=> new ImmediateData((ulong)a | ((ulong)b << 8), 2);
		public static ImmediateData FromBytes(byte a, byte b, byte c, byte d)
			=> new ImmediateData((ulong)a | ((ulong)b << 8) | ((ulong)c << 16) | ((ulong)d << 24), 4);
		public static ImmediateData FromBytes(byte a, byte b, byte c, byte d, byte e, byte f)
			=> new ImmediateData((ulong)a | ((ulong)b << 8) | ((ulong)c << 16) | ((ulong)d << 24)
				| ((ulong)e << 32) | ((ulong)f << 40), 6);
		public static ImmediateData FromBytes(byte a, byte b, byte c, byte d, byte e, byte f, byte g, byte h)
			=> new ImmediateData((ulong)a | ((ulong)b << 8) | ((ulong)c << 16) | ((ulong)d << 24)
				| ((ulong)e << 32) | ((ulong)f << 40) | ((ulong)g << 48) | ((ulong)h << 56), 8);

		public static ImmediateData FromInteger(byte value) => new ImmediateData(value, 1);
		public static ImmediateData FromInteger(sbyte value) => new ImmediateData(unchecked((byte)value), 1);
		public static ImmediateData FromInteger(ushort value) => new ImmediateData(value, 2);
		public static ImmediateData FromInteger(short value) => new ImmediateData(unchecked((ushort)value), 2);
		public static ImmediateData FromInteger(uint value) => new ImmediateData(value, 4);
		public static ImmediateData FromInteger(int value) => new ImmediateData(unchecked((uint)value), 4);
		public static ImmediateData FromInteger(ulong value) => new ImmediateData(value, 8);
		public static ImmediateData FromInteger(long value) => new ImmediateData(unchecked((ulong)value), 8);

		public static ImmediateData FromRawStorage(ulong value, int sizeInBytes)
		{
			if (!IsValidSize(sizeInBytes)) throw new ArgumentOutOfRangeException(nameof(sizeInBytes));
			return new ImmediateData(value & GetMask(sizeInBytes), sizeInBytes);
		}

		private static ulong GetMask(int sizeInBytes)
			=> sizeInBytes == 8 ? ulong.MaxValue : (1UL << (sizeInBytes* 8)) - 1UL;
		#endregion

		public static bool operator ==(ImmediateData lhs, ImmediateData rhs) => Equals(lhs, rhs);
		public static bool operator !=(ImmediateData lhs, ImmediateData rhs) => !Equals(lhs, rhs);

		#region Explicit Members
		int IReadOnlyCollection<byte>.Count => sizeInBytes;
		int ICollection<byte>.Count => sizeInBytes;
		bool ICollection<byte>.IsReadOnly => true;
		byte IReadOnlyList<byte>.this[int index] => GetByte(index);
		void ICollection<byte>.CopyTo(byte[] array, int arrayIndex) => CopyBytes(array, arrayIndex);
		int IList<byte>.IndexOf(byte item) => IndexOf(item);
		bool ICollection<byte>.Contains(byte item) => IndexOf(item) >= 0;
		IEnumerator<byte> IEnumerable<byte>.GetEnumerator() => ((IEnumerable<byte>)AsBytes()).GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => AsBytes().GetEnumerator();

		byte IList<byte>.this[int index] { get => GetByte(index); set => throw new NotSupportedException(); }
		void IList<byte>.Insert(int index, byte item) => throw new NotSupportedException();
		void IList<byte>.RemoveAt(int index) => throw new NotSupportedException();
		void ICollection<byte>.Add(byte item) => throw new NotSupportedException();
		void ICollection<byte>.Clear() => throw new NotSupportedException();
		bool ICollection<byte>.Remove(byte item) => throw new NotSupportedException();
		#endregion
	}
}
