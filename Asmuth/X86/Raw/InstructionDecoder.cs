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
		private readonly Instruction.Builder builder = new Instruction.Builder();
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
		public InstructionDecodingMode Mode => mode;
		public InstructionDecodingState State => state;
		public InstructionFields Fields => fields;

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
						builder.LegacyPrefixes.Add((LegacyPrefix)@byte);
						return true;
					}

					if (mode == InstructionDecodingMode.SixtyFourBit
						&& ((Rex)@byte & Rex.Reserved_Mask) == Rex.Reserved_Value)
					{
						builder.Xex = new Xex((Rex)@byte);
						fields |= InstructionFields.Xex;
						return AdvanceTo(InstructionDecodingState.ExpectOpcode);
					}

					if (@byte == (byte)Vex2.FirstByte || @byte == (byte)Vex3.FirstByte
						|| @byte == (byte)Xop.FirstByte || @byte == (byte)EVex.FirstByte)
					{
						if (mode == InstructionDecodingMode._8086)
							return AdvanceToError(InstructionDecodingError.VexIn8086Mode);

						builder.Xex = new Xex(XexEnums.GetTypeFromByte(@byte));
						int remainingBytes = builder.Xex.Type.GetByteCount().Value - 1;
						return AdvanceTo(InstructionDecodingState.ExpectXexByte, substate: (byte)remainingBytes);
					}

					goto case InstructionDecodingState.ExpectOpcode;
				}

				case InstructionDecodingState.ExpectXexByte:
				{
					if (builder.Xex.Type == XexType.Xop && (@byte & 0x04) == 0)
					{
						// What we just read was not a XOP, but a POP reg/mem
						builder.Xex = default(Xex);
						builder.MainByte = (byte)Xop.FirstByte;
						builder.ModRM = (ModRM)@byte;
						fields |= InstructionFields.Opcode | InstructionFields.ModRM;
						state = InstructionDecodingState.ExpectModRM;
						return AdvanceToSibOrFurther();
					}

					// Accumulate bytes in the immediate field
					builder.Immediate = (builder.Immediate << 8) | @byte;
					--substate;
					if (substate > 0) return true; // One more byte to read

					switch (builder.Xex.Type)
					{
						case XexType.Vex2: builder.Xex = new Xex((Vex2)builder.Immediate | Vex2.Reserved_Value); break;
						case XexType.Vex3: builder.Xex = new Xex((Vex3)builder.Immediate | Vex3.Reserved_Value); break;
						case XexType.Xop: builder.Xex = new Xex((Xop)builder.Immediate | Xop.Reserved_Value); break;
						case XexType.EVex: builder.Xex = new Xex((EVex)builder.Immediate | EVex.Reserved_Value); break;
						default: throw new UnreachableException();
					}

					builder.Immediate = 0; // Stop using as accumulator

					fields |= InstructionFields.Xex;
					return AdvanceTo(InstructionDecodingState.ExpectOpcode);
				}

				case InstructionDecodingState.ExpectOpcode:
				{
					if (builder.Xex.Type.AllowsEscapes())
					{
						if (builder.Xex.OpcodeMap == OpcodeMap.Default && @byte == 0x0F)
						{
							builder.Xex = builder.Xex.WithOpcodeMap(OpcodeMap.Legacy_0F);
							return true;
						}

						if (builder.Xex.OpcodeMap == OpcodeMap.Legacy_0F)
						{
							switch (@byte)
							{
								case 0x38: builder.Xex = builder.Xex.WithOpcodeMap(OpcodeMap.Legacy_0F38); return true;
								case 0x3A: builder.Xex = builder.Xex.WithOpcodeMap(OpcodeMap.Legacy_0F3A); return true;
								default: break;
							}
						}
					}

					builder.MainByte = @byte;
					fields |= InstructionFields.Opcode;

					bool hasModRM;
					OperandSize? immediateSize;
					if (!lookup.TryLookup(mode, GetOpcode(), out hasModRM, out immediateSize))
						AdvanceToError(InstructionDecodingError.UnknownOpcode);
					builder.ImmediateSize = immediateSize;
						
					return hasModRM ? AdvanceTo(InstructionDecodingState.ExpectModRM) : AdvanceToImmediateOrEnd();
				}

				case InstructionDecodingState.ExpectModRM:
				{
					builder.ModRM = (ModRM)@byte;
					fields |= InstructionFields.ModRM;
					return AdvanceToSibOrFurther();
				}

				case InstructionDecodingState.ExpectSib:
				{
					builder.Sib = (Sib)@byte;
					fields |= InstructionFields.Sib;
					return AdvanceToDisplacementOrFurther();
				}

				case InstructionDecodingState.ExpectDisplacement:
				{
					builder.Displacement = (int)@byte << (substate * 8);
					substate++;
					var displacementSize = builder.ModRM.Value.GetDisplacementSizeInBytes(
						builder.Sib.Value, GetEffectiveAddressSize());
					if (substate < displacementSize) return true; // More bytes to come

					// Sign-extend
					if (displacementSize == 1)
						builder.Displacement = unchecked((sbyte)builder.Displacement);
					else if (displacementSize == 2)
						builder.Displacement = unchecked((short)builder.Displacement);
					
					return AdvanceToImmediateOrEnd();
				}

				case InstructionDecodingState.ExpectImmediate:
				{
					builder.Immediate |= (ulong)@byte << (substate * 8);
					substate++;
					if (substate < builder.ImmediateSize.Value.InBytes())
						return true; // More bytes to come

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
			builder.Clear();
			builder.DefaultAddressSize = mode.GetDefaultAddressSize();
		}

		public void Reset(InstructionDecodingMode mode)
		{
			this.mode = mode;
			Reset();
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
			return mode.GetEffectiveAddressSize(@override: builder.LegacyPrefixes.Contains(LegacyPrefix.AddressSizeOverride));
		}

		private bool AdvanceToSibOrFurther()
		{
			Contract.Requires(State == InstructionDecodingState.ExpectModRM);
			Contract.Requires(Fields.Has(InstructionFields.ModRM));
			
			return builder.ModRM.Value.ImpliesSib(GetEffectiveAddressSize())
				? AdvanceTo(InstructionDecodingState.ExpectSib)
				: AdvanceToDisplacementOrFurther();
		}

		private bool AdvanceToDisplacementOrFurther()
		{
			Contract.Requires(State >= InstructionDecodingState.ExpectModRM);
			Contract.Requires(State < InstructionDecodingState.ExpectDisplacement);

			var displacementSize = builder.ModRM.Value.GetDisplacementSizeInBytes(
				builder.Sib.Value, GetEffectiveAddressSize());
			return displacementSize > 0
				? AdvanceTo(InstructionDecodingState.ExpectDisplacement)
				: AdvanceToImmediateOrEnd();
		}

		private bool AdvanceToImmediateOrEnd()
		{
			Contract.Requires(State >= InstructionDecodingState.ExpectOpcode);
			Contract.Requires(State < InstructionDecodingState.ExpectImmediate);
			
			return builder.ImmediateSize.HasValue
				? AdvanceTo(InstructionDecodingState.ExpectImmediate)
				: AdvanceTo(InstructionDecodingState.Completed);
		}
		#endregion
	}
}
