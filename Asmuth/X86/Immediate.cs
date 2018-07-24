using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86
{
	// Represents an untyped immediate value associated with an instruction
    public readonly struct Immediate : IReadOnlyList<byte>, IList<byte>
    {
		// Bytes 0-7 of immediate stored as 0x7766554433221100
		private readonly ulong bytes;
		private readonly byte sizeInBytes;

		public ulong RawStorage => bytes;
		public int SizeInBytes => sizeInBytes;
		public int SizeInBits => sizeInBytes * 8;

		private Immediate(ulong bytes, int sizeInBytes)
			=> (this.bytes, this.sizeInBytes) = (bytes, (byte)sizeInBytes);

		#region Methods
		public byte GetByte(int index)
		{
			if (index >= sizeInBytes) throw new IndexOutOfRangeException();
			return (byte)((bytes >> (index * 8)) & 0xFF);
		}

		public byte AsUInt8()
		{
			if (sizeInBytes != 1) throw new InvalidOperationException();
			return (byte)bytes;
		}

		public ushort AsUInt16()
		{
			if (sizeInBytes != 2) throw new InvalidOperationException();
			return (ushort)(bytes & 0xFFFF);
		}

		public uint AsUInt32()
		{
			if (sizeInBytes != 4) throw new InvalidOperationException();
			return (uint)(bytes & 0xFFFFFFFFU);
		}

		public ulong AsUInt64()
		{
			if (sizeInBytes != 8) throw new InvalidOperationException();
			return bytes;
		}

		public sbyte AsSInt8() => unchecked((sbyte)AsUInt8());
		public short AsInt16() => unchecked((short)AsUInt16());
		public int AsInt32() => unchecked((int)AsUInt32());
		public long AsInt64() => unchecked((long)bytes);

		public (ushort, ushort) AsFarPtr16()
		{
			if (sizeInBytes != 4) throw new InvalidOperationException();
			// Our encoding/endianness means that this is reversed
			return ((ushort)bytes, (ushort)(bytes >> 16));
		}

		public (ushort, uint) AsFarPtr32()
		{
			if (sizeInBytes != 6) throw new InvalidOperationException();
			// Our encoding/endianness means that this is reversed
			return ((ushort)bytes, (uint)(bytes >> 16));
		}

		public byte[] AsBytes()
		{
			var array = new byte[sizeInBytes];
			CopyBytes(array, arrayIndex: 0);
			return array;
		}

		public void CopyBytes(byte[] array, int arrayIndex)
		{
			ulong temp = bytes;
			for (int i = 0; i < sizeInBytes; ++i)
			{
				array[arrayIndex] = (byte)(temp & 0xFF);
				arrayIndex++;
				temp >>= 8;
			}
		}

		private int IndexOf(byte item)
		{
			ulong temp = bytes;
			for (int i = 0; i < sizeInBytes; ++i)
			{
				if ((byte)(temp & 0xFF) == item) return i;
				temp >>= 8;
			}
			return -1;
		}
		#endregion

		#region Static Methods
		// 0, 1, 2, 4, 6, 8
		public static bool IsValidSize(int size) => unchecked((uint)size) <= 8 && (size & (size - 1)) == 0;

		public static Immediate FromByte(byte value) => new Immediate(value, 1);
		public static Immediate FromBytes(byte a, byte b)
			=> new Immediate((ulong)a | ((ulong)b << 8), 2);
		public static Immediate FromBytes(byte a, byte b, byte c, byte d)
			=> new Immediate((ulong)a | ((ulong)b << 8) | ((ulong)c << 16) | ((ulong)d << 24), 4);
		public static Immediate FromBytes(byte a, byte b, byte c, byte d, byte e, byte f)
			=> new Immediate((ulong)a | ((ulong)b << 8) | ((ulong)c << 16) | ((ulong)d << 24)
				| ((ulong)e << 32) | ((ulong)f << 40), 6);
		public static Immediate FromBytes(byte a, byte b, byte c, byte d, byte e, byte f, byte g, byte h)
			=> new Immediate((ulong)a | ((ulong)b << 8) | ((ulong)c << 16) | ((ulong)d << 24)
				| ((ulong)e << 32) | ((ulong)f << 40) | ((ulong)g << 48) | ((ulong)h << 56), 8);

		public static Immediate FromInteger(byte value) => new Immediate(value, 1);
		public static Immediate FromInteger(sbyte value) => new Immediate(unchecked((byte)value), 1);
		public static Immediate FromInteger(ushort value) => new Immediate(value, 2);
		public static Immediate FromInteger(short value) => new Immediate(unchecked((ushort)value), 2);
		public static Immediate FromInteger(uint value) => new Immediate(value, 4);
		public static Immediate FromInteger(int value) => new Immediate(unchecked((uint)value), 4);
		public static Immediate FromInteger(ulong value) => new Immediate(value, 8);
		public static Immediate FromInteger(long value) => new Immediate(unchecked((ulong)value), 8);

		public static Immediate FromRawStorage(ulong value, int size)
		{
			if (!IsValidSize(size)) throw new ArgumentOutOfRangeException(nameof(size));
			return new Immediate(value, size);
		}
		#endregion

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
