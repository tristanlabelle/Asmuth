using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	[StructLayout(LayoutKind.Sequential, Size = 24)]
	public readonly partial struct Instruction
	{
		[Flags]
		private enum Flags : byte
		{
			CodeSegmentType_Shift = 0,
			CodeSegmentType_16 = 0 << (int)CodeSegmentType_Shift,
			CodeSegmentType_32 = 1 << (int)CodeSegmentType_Shift,
			CodeSegmentType_64 = 2 << (int)CodeSegmentType_Shift,
			CodeSegmentType_Mask = 3 << (int)CodeSegmentType_Shift,

			HasModRM = 1 << 2,

			ImmediateSizeInBytes_Shift = 4,
			ImmediateSizeInBytes_Mask = 0xF << ImmediateSizeInBytes_Shift
		}

		#region Fields
		public const int MaxLength = 15; // See 2.3.11

		private readonly ImmutableLegacyPrefixList legacyPrefixes; // 4 bytes
		private readonly Xex xex; // 4 bytes

		// 4 bytes
		private readonly Flags flags;
		private readonly byte mainByte;
		private readonly ModRM modRM;
		private readonly Sib sib;

		private readonly int displacement; // 4 bytes

		private readonly ulong immediateRawStorage; // 8 bytes
		#endregion

		#region Constructors
		private Instruction(Builder builder)
		{
			Contract.Requires(builder != null);

			legacyPrefixes = builder.LegacyPrefixes;
			xex = builder.Xex; // Validate if redundant with prefixes
			mainByte = builder.OpcodeByte;
			modRM = builder.ModRM.GetValueOrDefault();
			sib = builder.Sib.GetValueOrDefault(); // Validate if necessary
			displacement = builder.Displacement; // Validate with mod/sib
			immediateRawStorage = builder.Immediate.RawStorage; // Truncate to size

			flags = 0;
			flags |= (Flags)((int)builder.CodeSegmentType << (int)Flags.CodeSegmentType_Shift);
			if (builder.ModRM.HasValue) flags |= Flags.HasModRM;
			flags |= (Flags)(builder.Immediate.SizeInBytes << (int)Flags.ImmediateSizeInBytes_Shift);
		}
		#endregion

		#region Properties
		public CodeSegmentType CodeSegmentType => (CodeSegmentType)Bits.MaskAndShiftRight(
			(uint)flags, (uint)Flags.CodeSegmentType_Mask, (int)Flags.CodeSegmentType_Shift);
		public AddressSize EffectiveAddressSize => CodeSegmentType.GetEffectiveAddressSize(
			legacyPrefixes.HasAddressSizeOverride);
		public ImmutableLegacyPrefixList LegacyPrefixes => legacyPrefixes;
		public Xex Xex => xex;
		public OpcodeMap OpcodeMap => xex.OpcodeMap;
		public Opcode OpcodeLookupKey => OpcodeEnum.MakeLookupKey(SimdPrefix, OpcodeMap, MainByte);
		public byte MainByte => mainByte;
		public ModRM? ModRM => (flags & Flags.HasModRM) == Flags.HasModRM ? modRM : (ModRM?)null;
		public bool HasMemoryRM => ModRM.HasValue && ModRM.Value.IsMemoryRM();

		public SimdPrefix SimdPrefix
		{
			get
			{
				if (xex.OpcodeMap == OpcodeMap.Default)
				{
					Contract.Assert(!xex.SimdPrefix.HasValue || xex.SimdPrefix == SimdPrefix.None);
					return SimdPrefix.None;
				}
				return xex.SimdPrefix ?? legacyPrefixes.GetSimdPrefix(xex.OpcodeMap);
			}
		}

		public Sib? Sib
		{
			get
			{
				if ((flags & Flags.HasModRM) == 0) return null;
				if (!modRM.ImpliesSib(EffectiveAddressSize)) return null;
				return sib;
			}
		}

		public DisplacementSize DisplacementSize
		{
			get
			{
				if ((flags & Flags.HasModRM) == 0) return 0;
				return modRM.GetDisplacementSize(sib, EffectiveAddressSize);
			}
		}

		public int Displacement => displacement;
		public int ImmediateSizeInBytes => (int)Bits.MaskAndShiftRight((uint)flags, (uint)Flags.ImmediateSizeInBytes_Mask, (int)Flags.ImmediateSizeInBytes_Shift);
		public Immediate Immediate => Immediate.FromRawStorage(immediateRawStorage, ImmediateSizeInBytes);

		public int SizeInBytes
		{
			get
			{
				int size = legacyPrefixes.Count + xex.SizeInBytes + 1;

				if ((flags & Flags.HasModRM) != 0)
				{
					size++;
					var addressSize = EffectiveAddressSize;
					if (modRM.ImpliesSib(addressSize)) size++;
					size += modRM.GetDisplacementSize(sib, addressSize).InBytes();
				}

				size += ImmediateSizeInBytes;

				return size;
			}
		}
		#endregion

		#region Methods
		public Opcode GetOpcode()
		{
			return default(Opcode)
				.WithSimdPrefix(SimdPrefix)
				.WithRexW(xex.OperandSize64)
				.WithVectorSize(xex.VectorSize)
				.WithMap(OpcodeMap)
				.WithMainByte(MainByte)
				.WithExtraByte((byte)modRM);
		}

		public EffectiveAddress GetRMEffectiveAddress()
		{
			Contract.Requires(ModRM.HasValue);
			var encoding = new EffectiveAddress.Encoding(legacyPrefixes, xex, modRM, Sib, displacement);
			return EffectiveAddress.FromEncoding(CodeSegmentType, encoding);
		}
		#endregion
	}
}
