using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	[StructLayout(LayoutKind.Sequential, Size = 24)]
	public partial struct Instruction
	{
		private enum Flags : byte
		{
			HasModRM = 1 << 0,
			EffectiveAddressSize16 = 1 << 1, // For sib byte and size of displacement

			ImmediateSize_Shift = 4,
			ImmediateSize_Mask = 0xF << ImmediateSize_Shift
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

		private readonly ulong immediate; // 8 bytes
		#endregion

		#region Constructors
		private Instruction(Builder builder)
		{
			legacyPrefixes = builder.LegacyPrefixes;
			xex = builder.Xex; // Validate if redundant with prefixes
			mainByte = builder.MainByte;
			modRM = builder.ModRM.GetValueOrDefault();
			sib = builder.Sib.GetValueOrDefault(); // Validate if necessary
			displacement = builder.Displacement; // Validate with mod/sib
			immediate = builder.Immediate; // Truncate to size

			flags = 0;
			if (builder.ModRM.HasValue) flags |= Flags.HasModRM;
			bool hasAddressSizeOverride = legacyPrefixes.Contains(LegacyPrefix.AddressSizeOverride);
			bool effectiveAddressSize16 = builder.DefaultAddressSize.GetEffective(hasAddressSizeOverride) == AddressSize._16;
			if (effectiveAddressSize16) flags |= Flags.EffectiveAddressSize16;
			if (builder.ImmediateSize.HasValue)
				flags |= (Flags)(builder.ImmediateSize.Value.InBytes() << (int)Flags.ImmediateSize_Shift);
		}
		#endregion

		#region Properties
		public bool HasEffectiveAddressSizeOf16 => (flags & Flags.EffectiveAddressSize16) == Flags.EffectiveAddressSize16;
		public ImmutableLegacyPrefixList LegacyPrefixes => legacyPrefixes;
		public Xex Xex => xex;
		public byte MainByte => mainByte;
		public ModRM? ModRM => (flags & Flags.HasModRM) == Flags.HasModRM ? modRM : (ModRM?)null;

		public Sib? Sib
		{
			get
			{
				if ((flags & Flags.HasModRM) == 0) return null;
				var addressSize = HasEffectiveAddressSizeOf16 ? AddressSize._16 : AddressSize._32;
				if (!modRM.ImpliesSib(addressSize)) return null;
				return sib;
			}
		}

		public int Displacement => displacement;
		public ulong Immediate => immediate;
		public int ImmediateSizeInBytes => (int)Bits.MaskAndShiftRight((uint)flags, (uint)Flags.ImmediateSize_Mask, (int)Flags.ImmediateSize_Shift);

		public int SizeInBytes
		{
			get
			{
				int size = legacyPrefixes.Count + xex.MinimumSizeInBytes + 1;

				if ((flags & Flags.HasModRM) != 0)
				{
					size++;
					var addressSize = HasEffectiveAddressSizeOf16 ? AddressSize._16 : AddressSize._32;
					if (modRM.ImpliesSib(addressSize)) size++;
					size += modRM.GetDisplacementSizeInBytes(sib, addressSize);
				}

				size += ImmediateSizeInBytes;

				return size;
			}
		}

		public InstructionFields Fields
		{
			get
			{
				var fields = LegacyPrefixes.GetGroups() | InstructionFields.Opcode;
				if (xex.Type != XexType.Legacy) fields |= InstructionFields.Xex;

				if ((flags & Flags.HasModRM) != 0)
				{
					fields |= InstructionFields.ModRM;
					var addressSize = HasEffectiveAddressSizeOf16 ? AddressSize._16 : AddressSize._32;
					if (modRM.ImpliesSib(addressSize)) fields |= InstructionFields.Sib;
					if (modRM.GetDisplacementSizeInBytes(sib, addressSize) > 0)
						fields |= InstructionFields.Displacement;
				}

				if (ImmediateSizeInBytes > 0) fields |= InstructionFields.Immediate;

				return fields;
			}
		}
		#endregion
	}
}
