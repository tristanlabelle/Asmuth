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
			public CodeSegmentType CodeSegmentType { get; set; } = CodeSegmentType.IA32_Default32;
			public ImmutableLegacyPrefixList LegacyPrefixes { get; set; }
			public NonLegacyPrefixes NonLegacyPrefixes { get; set; }

			public InstructionPrefixes AllPrefixes
			{
				get => new InstructionPrefixes(CodeSegmentType, LegacyPrefixes, NonLegacyPrefixes);
				set
				{
					CodeSegmentType = value.CodeSegmentType;
					LegacyPrefixes = value.Legacy;
					NonLegacyPrefixes = value.NonLegacy;
				}
			}

			public byte MainByte { get; set; }
			public ModRM? ModRM { get; set; }
			public Sib? Sib { get; set; }
			public int Displacement { get; set; }
			public ImmediateData Immediate { get; set; }

			public Instruction Build() => new Instruction(this);
			public void Build(out Instruction instruction) => instruction = new Instruction(this);

			public void Clear()
			{
				CodeSegmentType = CodeSegmentType.IA32_Default32;
				LegacyPrefixes = ImmutableLegacyPrefixList.Empty;
				NonLegacyPrefixes = default;
				MainByte = 0;
				ModRM = null;
				Sib = null;
				Displacement = 0;
				Immediate = default;
			}
		}
	}
}
