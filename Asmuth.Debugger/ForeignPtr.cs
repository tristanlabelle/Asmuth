using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.Debugger
{
	/// <summary>
	/// A pointer in a foreign address space, not to be confounded with <see cref="IntPtr"/>.
	/// </summary>
	public struct ForeignPtr : IComparable<ForeignPtr>, IEquatable<ForeignPtr>, IFormattable
	{
		public ulong Address { get; }

		public ForeignPtr(ulong address) { this.Address = address; }
		public ForeignPtr(uint address) { this.Address = address; }
		public ForeignPtr(IntPtr address) { this.Address = unchecked((ulong)address); }
		public ForeignPtr(UIntPtr address) { this.Address = (ulong)address; }

		public bool Equals(ForeignPtr other) => Address == other.Address;
		public override bool Equals(object obj) => obj is ForeignPtr && Equals((ForeignPtr)obj);
		public override int GetHashCode() => Address.GetHashCode();

		public int CompareTo(ForeignPtr other) => Address.CompareTo(other.Address);

		public override string ToString() => "0x" + Address.ToString("{X8}", CultureInfo.InvariantCulture);

		public static bool Equals(ForeignPtr first, ForeignPtr second) => first.Equals(second);
		public static bool operator ==(ForeignPtr lhs, ForeignPtr rhs) => Equals(lhs, rhs);
		public static bool operator !=(ForeignPtr lhs, ForeignPtr rhs) => Equals(lhs, rhs);

		public static int Compare(ForeignPtr first, ForeignPtr second) => first.CompareTo(second);

		public string ToString(string format, IFormatProvider formatProvider) => Address.ToString(format, formatProvider);

		public static bool operator <(ForeignPtr lhs, ForeignPtr rhs) => lhs.Address < rhs.Address;
		public static bool operator <=(ForeignPtr lhs, ForeignPtr rhs) => lhs.Address <= rhs.Address;
		public static bool operator >(ForeignPtr lhs, ForeignPtr rhs) => lhs.Address > rhs.Address;
		public static bool operator >=(ForeignPtr lhs, ForeignPtr rhs) => lhs.Address >= rhs.Address;

		public static ForeignPtr operator+(ForeignPtr ptr, int offset)
			=> new ForeignPtr(ptr.Address + unchecked((ulong)(long)offset));

		public static ForeignPtr operator -(ForeignPtr ptr, int offset)
			=> new ForeignPtr(ptr.Address - unchecked((ulong)(long)offset));

		public static ForeignPtr operator +(ForeignPtr ptr, long offset)
			=> new ForeignPtr(ptr.Address + unchecked((ulong)offset));

		public static ForeignPtr operator -(ForeignPtr ptr, long offset)
			=> new ForeignPtr(ptr.Address - unchecked((ulong)offset));
	}
}
