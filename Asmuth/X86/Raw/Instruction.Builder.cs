using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	partial struct Instruction
	{
		public sealed class Builder
		{
			public AddressSize DefaultAddressSize { get; set; } = AddressSize._32;
			public LegacyPrefixList LegacyPrefixes { get; } = new LegacyPrefixList();
			public Xex Xex { get; set; }
			public byte MainByte { get; set; }
			public ModRM? ModRM { get; set; }
			public Sib? Sib { get; set; }
			public int Displacement { get; set; }
			public ulong Immediate { get; set; }
			public OperandSize? ImmediateSize { get; set; }

			public Instruction Build() => new Instruction(this);
			public void Build(out Instruction instruction) => instruction = new Instruction(this);

			public void Clear()
			{
				DefaultAddressSize = AddressSize._32;
				LegacyPrefixes.Clear();
				Xex = default(Xex);
				MainByte = 0;
				ModRM = null;
				Sib = null;
				Displacement = 0;
				Immediate = 0;
				ImmediateSize = null;
			}
		}
	}
}
