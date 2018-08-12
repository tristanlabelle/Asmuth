using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	partial struct Instruction
	{
		public sealed class Builder
		{
			public CodeSegmentType CodeSegmentType { get; set; } = CodeSegmentType._32Bits;
			public ImmutableLegacyPrefixList LegacyPrefixes { get; set; }
			public Xex Xex { get; set; }
			public byte MainByte { get; set; }
			public ModRM? ModRM { get; set; }
			public Sib? Sib { get; set; }
			public int Displacement { get; set; }
			public ImmediateData Immediate { get; set; }

			public Instruction Build() => new Instruction(this);
			public void Build(out Instruction instruction) => instruction = new Instruction(this);

			public void Clear()
			{
				CodeSegmentType = CodeSegmentType._32Bits;
				LegacyPrefixes = ImmutableLegacyPrefixList.Empty;
				Xex = default;
				MainByte = 0;
				ModRM = null;
				Sib = null;
				Displacement = 0;
				Immediate = default;
			}
		}
	}
}
