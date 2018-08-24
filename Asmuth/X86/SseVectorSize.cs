using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86
{
    public enum SseVectorSize
    {
		_128, // XMM
		_256, // YMM
		_512 // ZMM
	}

	public static class SseVectorSizeEnum
	{
		public static RegisterClass GetRegisterClass(this SseVectorSize size)
		{
			switch (size)
			{
				case SseVectorSize._128: return RegisterClass.Xmm;
				case SseVectorSize._256: return RegisterClass.Ymm;
				case SseVectorSize._512: return RegisterClass.Zmm;
				default: throw new ArgumentOutOfRangeException(nameof(size));
			}
		}

		public static string GetRegisterClassName(this SseVectorSize size) => "xyz"[(int)size] + "mm";
		public static int InBytes(this SseVectorSize size) => 16 << (int)size;
		public static int InBits(this SseVectorSize size) => InBytes(size) * 8;
	}
}
