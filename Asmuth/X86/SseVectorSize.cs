using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86
{
    public enum SseVectorSize
    {
		_128Bits, // XMM
		_256Bits, // YMM
		_512Bits // ZMM
	}

	public static class SseVectorSizeEnum
	{
		public static string GetRegisterClassName(this SseVectorSize size) => "xyz"[(int)size] + "mm";
		public static int InBytes(this SseVectorSize size) => 16 << (int)size;
		public static int InBits(this SseVectorSize size) => InBytes(size) * 8;
	}
}
