using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	public enum AddressSize : byte
	{
		_16,
		_32,
		_64
	}

	public static class AddressSizeEnum
	{
		[Pure]
		public static OperandSize ToOperandSize(this AddressSize size)
			=> (OperandSize)((int)size + 1);

		[Pure]
		public static AddressSize FromDecodingModeAndOverride(
			InstructionDecodingMode decodingMode, bool @override)
		{
			switch (decodingMode)
			{
				case InstructionDecodingMode.IA32_Default16:
					return @override ? AddressSize._32 : AddressSize._16;

				case InstructionDecodingMode.IA32_Default32:
					return @override ? AddressSize._16 : AddressSize._32;

				case InstructionDecodingMode.SixtyFourBit:
					return @override ? AddressSize._32 : AddressSize._64;

				default:
					throw new NotImplementedException();
			}
		}

		[Pure]
		public static int InBytes(this AddressSize size)
			=> (2 << (int)size);

		[Pure]
		public static int InBits(this AddressSize size)
			=> InBytes(size) * 8;
	}
}
