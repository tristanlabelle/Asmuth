using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	public enum ConditionCode : byte
	{
		Overflow = 0x0,
		NotOverflow = 0x1,
		Below = 0x2,
		Carry = 0x2,
		AboveOrEqual = 0x3,
		NotCarry = 0x3,
		Equal = 0x4,
		Zero = 0x4,
		NotEqual = 0x5,
		NotZero = 0x5,
		BelowOrEqual = 0x6,
		NotAbove = 0x6,
		NotBelowOrEqual = 0x7,
		Above = 0x7,

		Sign = 0x8,
		NotSign = 0x9,
		Parity = 0xA,
		ParityEven = 0xA,
		ParityOdd = 0xB,
		Less = 0xC,
		NotGreaterOrEqual = 0xC,
		GreaterOrEqual = 0xD,
		NotLess = 0xD,
		LessOrEqual = 0xE,
		NotGreater = 0xE,
		Greater = 0xF,
		NotLessOrEqual = 0xF
	}

	public static class ConditionCodeEnum
	{
		private static readonly ushort[] testedEFlags = new[]
		{
			(ushort)EFlags.Overflow, // Overflow
			(ushort)EFlags.Overflow, // Not overflow
			(ushort)EFlags.Carry, // Below
			(ushort)EFlags.Carry, // Above or equal
			(ushort)EFlags.Zero, // Equal
			(ushort)EFlags.Zero, // Not equal
			(ushort)(EFlags.Carry | EFlags.Zero), // Below or equal
			(ushort)(EFlags.Carry | EFlags.Zero), // Above
			(ushort)EFlags.Sign, // Sign
			(ushort)EFlags.Sign, // Not sign
			(ushort)EFlags.Parity, // Parity even
			(ushort)EFlags.Parity, // Parity odd
			(ushort)EFlags.Sign, // Less ?
			(ushort)EFlags.Sign, // Greater or equal ?
			(ushort)(EFlags.Zero | EFlags.Sign | EFlags.Overflow), // Less or equal
			(ushort)(EFlags.Zero | EFlags.Sign | EFlags.Overflow), // Greater
		};

		private const ushort unsignedComparisonMask
			= (1 << (int)ConditionCode.Above)
			| (1 << (int)ConditionCode.Below)
			| (1 << (int)ConditionCode.AboveOrEqual)
			| (1 << (int)ConditionCode.BelowOrEqual);
		private const ushort signedComparisonMask
			= (1 << (int)ConditionCode.Greater)
			| (1 << (int)ConditionCode.Less)
			| (1 << (int)ConditionCode.GreaterOrEqual)
			| (1 << (int)ConditionCode.LessOrEqual);

		private static readonly string[] suffixes = new[]
		{
			"O", "NO", "B", "AE", "E", "NE", "BE", "A",
			"S", "NS", "P", "NP", "L", "GE", "LE", "G"
		};

		[Pure]
		public static ConditionCode Negate(this ConditionCode code)
			=> (ConditionCode)((byte)code ^ 1);

		[Pure]
		public static EFlags GetTestedEflags(this ConditionCode code)
			=> (EFlags)testedEFlags[(int)code];

		[Pure]
		public static bool IsUnsignedComparison(this ConditionCode code)
			=> (unsignedComparisonMask & (1 << (int)code)) != 0;

		[Pure]
		public static bool IsSignedComparison(this ConditionCode code)
			=> (signedComparisonMask & (1 << (int)code)) != 0;

		[Pure]
		public static string GetMnemonicSuffix(this ConditionCode code)
			=> suffixes[(int)code];
	}
}
