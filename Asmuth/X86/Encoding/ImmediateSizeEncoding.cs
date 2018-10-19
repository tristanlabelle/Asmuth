using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Encoding
{
	public enum ImmediateVariableSize : byte
	{
		WordOrDword_OperandSize, // imm16/32, rel16/32
		WordOrDwordOrQword_OperandSize, // MOV: B8+r imm16/32/64, JMP: EA ptr16:16/16:32
		WordOrDwordOrQword_AddressSize, // MOV AX,moffs16/32/64
	}

	/// <summary>
	/// Represents the size of an immediate as a fixed base size plus an optional variable size.
	/// </summary>
	public static class ImmediateVariableSizeEnum
	{
		public static int GetMaxSizeInBytes(this ImmediateVariableSize size)
			=> size == ImmediateVariableSize.WordOrDword_OperandSize ? 4 : 8;

		public static bool IsAddressSizeDependent(this ImmediateVariableSize size)
			=> size == ImmediateVariableSize.WordOrDwordOrQword_AddressSize;

		public static bool IsOperandSizeDependent(this ImmediateVariableSize size)
			=> !IsAddressSizeDependent(size);

		public static IEnumerable<int> EnumeratePossibleSizesInBytes(this ImmediateVariableSize size)
		{
			yield return 2;
			yield return 4;
			if (GetMaxSizeInBytes(size) == 8) yield return 8;
		}

		public static int InBytes(this ImmediateVariableSize size, AddressSize addressSize, IntegerSize operandSize)
		{
			if (operandSize == IntegerSize.Byte) throw new ArgumentOutOfRangeException(nameof(operandSize));
			switch (size)
			{
				case ImmediateVariableSize.WordOrDword_OperandSize:
					return operandSize == IntegerSize.Word ? 2 : 4;

				case ImmediateVariableSize.WordOrDwordOrQword_OperandSize:
					return operandSize.InBytes();

				case ImmediateVariableSize.WordOrDwordOrQword_AddressSize:
					return addressSize.InBytes();

				default: throw new ArgumentOutOfRangeException(nameof(size));
			}
		}

		public static int InBits(this ImmediateVariableSize size, AddressSize addressSize, IntegerSize operandSize)
			=> InBytes(size, addressSize, operandSize) * 8;

		public static int InBytes(this ImmediateVariableSize size, in InstructionPrefixes prefixes)
			=> InBytes(size, prefixes.EffectiveAddressSize, prefixes.IntegerOperandSize);

		public static int InBits(this ImmediateVariableSize size, in InstructionPrefixes prefixes)
			=> InBytes(size, prefixes) * 8;
	}

	public readonly struct ImmediateSizeEncoding : IEquatable<ImmediateSizeEncoding>
	{
		// Low nibble: base size
		// High nibble: variable size or null (0)
		private readonly byte data;

		private ImmediateSizeEncoding(int inBytes)
		{
			if (inBytes < 0 || inBytes > 8) throw new ArgumentException();
			this.data = (byte)inBytes;
		}

		public ImmediateSizeEncoding(ImmediateVariableSize variable)
		{
			this.data = (byte)(((byte)variable + 1) << 4);
		}

		private ImmediateSizeEncoding(int baseInBytes, ImmediateVariableSize variable)
		{
			if (baseInBytes < 0 || baseInBytes + variable.GetMaxSizeInBytes() > 8)
				throw new ArgumentException();
			this.data = (byte)(baseInBytes | (((byte)variable + 1) << 4));
		}

		private ImmediateSizeEncoding(int baseInBytes, ImmediateVariableSize? variable)
		{
			var maxSizeInBytes = variable.HasValue
				? baseInBytes + variable.Value.GetMaxSizeInBytes()
				: baseInBytes;
			if (baseInBytes < 0 || maxSizeInBytes > 8) throw new ArgumentException();
			this.data = variable.HasValue ? (byte)(baseInBytes | (((byte)variable + 1) << 4)) : (byte)baseInBytes;
		}

		public bool IsZero => data == 0;
		public bool IsNonZero => data != 0;
		public int BaseInBytes => data & 0xF;
		public int BaseInBits => BaseInBytes * 8;
		public bool IsFixed => (data & 0xF0) == 0;
		public bool IsVariable => (data & 0xF0) != 0;
		public int? FixedInBytes => IsFixed ? (int?)BaseInBytes : null;
		public int? FixedInBits => IsFixed ? (int?)BaseInBits : null;
		public ImmediateVariableSize? Variable => (data & 0xF0) > 0
			? (ImmediateVariableSize?)((data >> 4) - 1) : null;

		public int InBytes(AddressSize addressSize, IntegerSize operandSize)
			=> IsFixed ? BaseInBytes : BaseInBytes + Variable.Value.InBytes(addressSize, operandSize);

		public int InBytes(in InstructionPrefixes prefixes)
			=> IsFixed ? BaseInBytes : BaseInBytes + Variable.Value.InBytes(prefixes);

		public int InBits(AddressSize addressSize, IntegerSize operandSize)
			=> InBytes(addressSize, operandSize) * 8;

		public int InBits(in InstructionPrefixes prefixes) => InBytes(prefixes) * 8;

		public bool Equals(ImmediateSizeEncoding other) => data == other.data;
		public override bool Equals(object obj) => obj is ImmediateSizeEncoding && Equals((ImmediateSizeEncoding)obj);
		public override int GetHashCode() => data;
		public static bool Equals(ImmediateSizeEncoding lhs, ImmediateSizeEncoding rhs) => lhs.Equals(rhs);
		public static bool operator ==(ImmediateSizeEncoding lhs, ImmediateSizeEncoding rhs) => Equals(lhs, rhs);
		public static bool operator !=(ImmediateSizeEncoding lhs, ImmediateSizeEncoding rhs) => !Equals(lhs, rhs);

		public override string ToString()
		{
			if (IsFixed) return "imm" + BaseInBits;
			var str = new StringBuilder(12);
			str.Append("imm");
			foreach (var size in Variable.Value.EnumeratePossibleSizesInBytes())
			{
				if (str.Length > 3) str.Append('/');
				str.Append(BaseInBits + size * 8);
			}
			return str.ToString();
		}

		public static readonly ImmediateSizeEncoding Zero = FromBytes(0);
		public static readonly ImmediateSizeEncoding Byte = FromBytes(1);
		public static readonly ImmediateSizeEncoding Word = FromBytes(2);
		public static readonly ImmediateSizeEncoding Dword = FromBytes(4);
		public static readonly ImmediateSizeEncoding Qword = FromBytes(8);
		public static readonly ImmediateSizeEncoding WordOrDword_OperandSize
			= ImmediateVariableSize.WordOrDword_OperandSize;
		public static readonly ImmediateSizeEncoding WordOrDwordOrQword_OperandSize
			= ImmediateVariableSize.WordOrDwordOrQword_OperandSize;
		public static readonly ImmediateSizeEncoding WordOrDwordOrQword_AddressSize
			= ImmediateVariableSize.WordOrDwordOrQword_AddressSize;

		public static ImmediateSizeEncoding FromBytes(int size) => new ImmediateSizeEncoding(size);
		public static ImmediateSizeEncoding FromBytes(int @base, ImmediateVariableSize variable)
			=> new ImmediateSizeEncoding(@base, variable);
		public static ImmediateSizeEncoding FromBytes(int @base, ImmediateVariableSize? variable)
			=> new ImmediateSizeEncoding(@base, variable);

		public static ImmediateSizeEncoding Combine(ImmediateSizeEncoding lhs, ImmediateSizeEncoding rhs)
		{
			if (lhs.IsVariable && rhs.IsVariable) throw new ArgumentException();
			return new ImmediateSizeEncoding(lhs.BaseInBytes + rhs.BaseInBytes, lhs.Variable ?? rhs.Variable);
		}

		public static implicit operator ImmediateSizeEncoding(ImmediateVariableSize variable)
			=> new ImmediateSizeEncoding(variable);
	}
}
