using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	public sealed class CodeWriter
	{
		private readonly Stream stream;
		private readonly CodeContext context;

		public CodeWriter(Stream stream, CodeContext context)
		{
			Contract.Requires(stream != null && stream.CanWrite);
			this.stream = stream;
			this.context = context;
		}

		public void Mov(EffectiveAddress dest, GprCode src)
		{

		}

		public void Cpuid() => Emit(OpcodeMap.Escape0F, 0xA2);
		public void Nop() => Emit(0x90);
		public void Ret() => Emit(0xC3);

		private void Emit(byte opcode) => stream.WriteByte(opcode);

		private void Emit(OpcodeMap map, byte opcode)
		{
			switch (map)
			{
				case OpcodeMap.Default: break;
				case OpcodeMap.Escape0F: stream.WriteByte(0x0F); break;
				case OpcodeMap.Escape0F38: stream.WriteByte(0x0F); stream.WriteByte(0x38); break;
				case OpcodeMap.Escape0F3A: stream.WriteByte(0x0F); stream.WriteByte(0x3A); break;
				default: throw new ArgumentOutOfRangeException(nameof(map));
			}
		}
	}
}
