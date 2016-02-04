using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	partial struct Instruction
	{
		public sealed class Builder
		{
			private byte immediateSizeInBytes;

			public AddressSize DefaultAddressSize { get; set; } = AddressSize._32;
			public LegacyPrefixList LegacyPrefixes { get; } = new LegacyPrefixList();
			public Xex Xex { get; set; }
			public byte OpcodeByte { get; set; }
			public ModRM? ModRM { get; set; }
			public Sib? Sib { get; set; }
			public int Displacement { get; set; }
			public ulong Immediate { get; set; }

			public int ImmediateSizeInBytes
			{
				get { return immediateSizeInBytes; }
				set
				{
					Contract.Requires(value >= 0 && value <= 8);
					immediateSizeInBytes = (byte)value;
				}
			}

			public Instruction Build() => new Instruction(this);
			public void Build(out Instruction instruction) => instruction = new Instruction(this);

			public void Clear()
			{
				DefaultAddressSize = AddressSize._32;
				LegacyPrefixes.Clear();
				Xex = default(Xex);
				OpcodeByte = 0;
				ModRM = null;
				Sib = null;
				Displacement = 0;
				Immediate = 0;
				immediateSizeInBytes = 0;
			}
		}
	}
}
