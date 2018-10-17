using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Asmuth.X86
{
	public readonly struct InstructionPrefixes
	{
		public ImmutableLegacyPrefixList Legacy { get; }
		public NonLegacyPrefixes NonLegacy { get; }
		public CodeSegmentType CodeSegmentType { get; }

		public InstructionPrefixes(CodeSegmentType codeSegmentType,
			ImmutableLegacyPrefixList legacy, NonLegacyPrefixes nonLegacy)
		{
			this.CodeSegmentType = codeSegmentType;
			this.Legacy = legacy;
			this.NonLegacy = nonLegacy;
		}

		public AddressSize EffectiveAddressSize => CodeSegmentType.GetEffectiveAddressSize(Legacy);
		public IntegerSize IntegerOperandSize => CodeSegmentType.GetIntegerOperandSize(Legacy, NonLegacy);
		public OpcodeMap OpcodeMap => NonLegacy.OpcodeMap;
		public VexType VexType => NonLegacy.VexType;
		public AvxVectorSize VectorSize => NonLegacy.VectorSize;

		public SimdPrefix PotentialSimdPrefix
		{
			get
			{
				if (NonLegacy.OpcodeMap == OpcodeMap.Default)
				{
					Debug.Assert(!NonLegacy.SimdPrefix.HasValue || NonLegacy.SimdPrefix == SimdPrefix.None);
					return SimdPrefix.None;
				}
				return NonLegacy.SimdPrefix ?? Legacy.PotentialSimdPrefix;
			}
		}
	}
}
