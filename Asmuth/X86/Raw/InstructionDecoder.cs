using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	public enum InstructionDecodingState : byte
	{
		Initial,
		ExpectPrefixOrOpcode,
		ExpectXexByte, // substate: remaining byte count
		ExpectOpcode,
		ExpectModRM,
		ExpectSib,
		ExpectDisplacement, // substate: bytes read
		ExpectImmediate, // substate: bytes read
		Completed,
		Error // substate: error enum
	}

	public enum InstructionDecodingError : byte
	{
		VexIn8086Mode,
		LockAndVex,
		SimdPrefixAndVex,
		RexAndVex,
		DuplicateLegacyPrefixGroup,
		MultipleXex,
		UnknownOpcode
	}

	public sealed class InstructionDecoder
	{
		#region Fields
		private readonly IInstructionDecoderLookup lookup;
		private InstructionDecodingMode mode;

		// State data
		private InstructionDecodingState state;
		private byte substate;
		private InstructionFields fields;

		// Instruction data
		private ImmutableLegacyPrefixList legacyPrefixes;
		private Xex xex;
		private ulong immediate;
		private int displacement;
		private byte mainOpcode;
		private ModRM modRM;
		private Sib sib;
		private byte immediateSize;
		#endregion

		#region Constructors
		public InstructionDecoder(IInstructionDecoderLookup lookup, InstructionDecodingMode mode)
		{
			Contract.Requires(lookup != null);

			this.lookup = lookup;
			this.mode = mode;
		}
		#endregion

		#region Properties
		public InstructionDecodingMode Mode
		{
			get { return mode; }
		}

		public InstructionDecodingState State => state;
		public InstructionFields Fields => fields;
		public ImmutableLegacyPrefixList LegacyPrefixes => legacyPrefixes;

		public InstructionDecodingError? Error
		{
			get
			{
				if (state != InstructionDecodingState.Error) return null;
				return (InstructionDecodingError)state;
			}
		}
		#endregion

		#region Methods
		public bool Feed(byte @byte)
		{
			switch (State)
			{
				case InstructionDecodingState.Initial:
				case InstructionDecodingState.ExpectPrefixOrOpcode:
				{
					state = InstructionDecodingState.ExpectPrefixOrOpcode;

					// Check if we have a legacy prefix
					var legacyPrefixGroupField = LegacyPrefixEnum.GetFieldOrNone((LegacyPrefix)@byte);
					if (legacyPrefixGroupField != InstructionFields.None)
					{
						if ((fields & legacyPrefixGroupField) == legacyPrefixGroupField)
							return AdvanceToError(InstructionDecodingError.DuplicateLegacyPrefixGroup);

						fields |= legacyPrefixGroupField;
						legacyPrefixes.Add((LegacyPrefix)@byte);
						return true;
					}

					if (mode == InstructionDecodingMode.SixtyFourBit
						&& ((Rex)@byte & Rex.Reserved_Mask) == Rex.Reserved_Value)
					{
						xex = new Xex((Rex)@byte);
						fields |= InstructionFields.Xex;
						return AdvanceTo(InstructionDecodingState.ExpectOpcode);
					}

					if (@byte == (byte)Vex2.FirstByte || @byte == (byte)Vex3.FirstByte
						|| @byte == (byte)Xop.FirstByte || @byte == (byte)EVex.FirstByte)
					{
						if (mode == InstructionDecodingMode._8086)
							return AdvanceToError(InstructionDecodingError.VexIn8086Mode);

						xex = new Xex(XexEnums.GetTypeFromByte(@byte));
						int remainingBytes = xex.Type.GetByteCount().Value - 1;
						return AdvanceTo(InstructionDecodingState.ExpectXexByte, substate: (byte)remainingBytes);
					}

					goto case InstructionDecodingState.ExpectOpcode;
				}

				case InstructionDecodingState.ExpectXexByte:
				{
					if (xex.Type == XexType.Xop && (@byte & 0x04) == 0)
					{
						// What we just read was not a XOP, but a POP reg/mem
						xex = default(Xex);
						mainOpcode = (byte)Xop.FirstByte;
						modRM = (ModRM)@byte;
						fields |= InstructionFields.Opcode | InstructionFields.ModRM;
						state = InstructionDecodingState.ExpectModRM;
						return AdvanceToSibOrFurther();
					}

					// Accumulate bytes in the immediate field
					immediate = (immediate << 8) | @byte;
					--substate;
					if (substate > 0) return true; // One more byte to read

					switch (xex.Type)
					{
						case XexType.Vex2: xex = new Xex((Vex2)immediate | Vex2.Reserved_Value); break;
						case XexType.Vex3: xex = new Xex((Vex3)immediate | Vex3.Reserved_Value); break;
						case XexType.Xop: xex = new Xex((Xop)immediate | Xop.Reserved_Value); break;
						case XexType.EVex: xex = new Xex((EVex)immediate | EVex.Reserved_Value); break;
						default: throw new UnreachableException();
					}

					immediate = 0; // Stop using as accumulator

					fields |= InstructionFields.Xex;
					return AdvanceTo(InstructionDecodingState.ExpectOpcode);
				}

				case InstructionDecodingState.ExpectOpcode:
				{
					if (xex.Type.AllowsEscapes())
					{
						if (xex.OpcodeMap == OpcodeMap.Default && @byte == 0x0F)
						{
							xex = xex.WithOpcodeMap(OpcodeMap.Legacy_0F);
							return true;
						}

						if (xex.OpcodeMap == OpcodeMap.Legacy_0F)
						{
							switch (@byte)
							{
								case 0x38: xex = xex.WithOpcodeMap(OpcodeMap.Legacy_0F38); return true;
								case 0x3A: xex = xex.WithOpcodeMap(OpcodeMap.Legacy_0F3A); return true;
								default: break;
							}
						}
					}

					mainOpcode = @byte;
					fields |= InstructionFields.Opcode;

					bool hasModRM;
					int immediateSize;
					if (!lookup.TryLookup(mode, GetOpcode(), out hasModRM, out immediateSize))
						AdvanceToError(InstructionDecodingError.UnknownOpcode);
					this.immediateSize = (byte)immediateSize;
						
					return hasModRM ? AdvanceTo(InstructionDecodingState.ExpectModRM) : AdvanceToImmediateOrEnd();
				}

				case InstructionDecodingState.ExpectModRM:
				{
					modRM = (ModRM)@byte;
					fields |= InstructionFields.ModRM;
					return AdvanceToSibOrFurther();
				}

				case InstructionDecodingState.ExpectSib:
				{
					sib = (Sib)@byte;
					fields |= InstructionFields.Sib;
					return AdvanceToDisplacementOrFurther();
				}

				case InstructionDecodingState.ExpectDisplacement:
				{
					displacement = (int)@byte << (substate * 8);
					substate++;
					var displacementSize = modRM.GetDisplacementSizeInBytes(sib, GetEffectiveAddressSize());
					if (substate < displacementSize) return true; // More bytes to come

					// Sign-extend
					if (displacementSize == 1) displacement = unchecked((sbyte)displacement);
					else if (displacementSize == 2) displacement = unchecked((short)displacement);
					
					return AdvanceToImmediateOrEnd();
				}

				case InstructionDecodingState.ExpectImmediate:
				{
					immediate |= (ulong)@byte << (substate * 8);
					substate++;
					if (substate < immediateSize) return true; // More bytes to come

					return AdvanceTo(InstructionDecodingState.Completed);
				}

				default:
					throw new InvalidOperationException("Invalid decoding state.");
			}
		}

		/// <summary>
		/// Resets this <see cref="InstructionDecoder"/> to the <see cref="InstructionDecodingState.Initial"/> state.
		/// </summary>
		public void Reset()
		{
			if (state == InstructionDecodingState.Initial) return;

			state = InstructionDecodingState.Initial;
			substate = 0;
			fields = 0;

			legacyPrefixes.Clear();
			immediate = 0;
			displacement = 0;
			xex = default(Xex);
			mainOpcode = 0;
			modRM = 0;
			sib = 0;
			immediateSize = 0;
		}

		public void Reset(InstructionDecodingMode mode)
		{
			Reset();
			this.mode = mode;
		}

		private Opcode GetOpcode()
		{
			throw new NotImplementedException();
		}

		private bool AdvanceTo(InstructionDecodingState newState, byte substate = 0)
		{
			Contract.Requires(newState > State);
			this.state = newState;
			this.substate = 0;
			return newState != InstructionDecodingState.Completed && newState != InstructionDecodingState.Error;
		}

		private bool AdvanceToError(InstructionDecodingError error)
		{
			return AdvanceTo(InstructionDecodingState.Error, substate: (byte)error);
		}

		private AddressSize GetEffectiveAddressSize()
		{
			Contract.Requires(state > InstructionDecodingState.ExpectPrefixOrOpcode);
			return mode.GetEffectiveAddressSize(@override: legacyPrefixes.Contains(LegacyPrefix.AddressSizeOverride));
		}

		private bool AdvanceToSibOrFurther()
		{
			Contract.Requires(State == InstructionDecodingState.ExpectModRM);
			Contract.Requires(Fields.Has(InstructionFields.ModRM));
			
			return modRM.ImpliesSib(GetEffectiveAddressSize())
				? AdvanceTo(InstructionDecodingState.ExpectSib)
				: AdvanceToDisplacementOrFurther();
		}

		private bool AdvanceToDisplacementOrFurther()
		{
			Contract.Requires(State >= InstructionDecodingState.ExpectModRM);
			Contract.Requires(State < InstructionDecodingState.ExpectDisplacement);

			var displacementSize = modRM.GetDisplacementSizeInBytes(sib, GetEffectiveAddressSize());
			return displacementSize > 0
				? AdvanceTo(InstructionDecodingState.ExpectDisplacement)
				: AdvanceToImmediateOrEnd();
		}

		private bool AdvanceToImmediateOrEnd()
		{
			Contract.Requires(State >= InstructionDecodingState.ExpectOpcode);
			Contract.Requires(State < InstructionDecodingState.ExpectImmediate);
			
			return immediateSize > 0
				? AdvanceTo(InstructionDecodingState.ExpectImmediate)
				: AdvanceTo(InstructionDecodingState.Completed);
		}
		#endregion
	}
}
