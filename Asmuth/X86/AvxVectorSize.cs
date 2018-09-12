using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86
{
    public enum AvxVectorSize
    {
		_128, // XMM
		_256, // YMM
		_512 // ZMM
	}

	public static class AvxVectorSizeEnum
	{
		public static RegisterClass GetRegisterClass(this AvxVectorSize size)
		{
			switch (size)
			{
				case AvxVectorSize._128: return RegisterClass.Xmm;
				case AvxVectorSize._256: return RegisterClass.Ymm;
				case AvxVectorSize._512: return RegisterClass.Zmm;
				default: throw new ArgumentOutOfRangeException(nameof(size));
			}
		}

		public static string GetRegisterClassName(this AvxVectorSize size) => "xyz"[(int)size] + "mm";
		public static int InBytes(this AvxVectorSize size) => 16 << (int)size;
		public static int InBits(this AvxVectorSize size) => InBytes(size) * 8;
	}
}
