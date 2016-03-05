using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
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
		private uint accumulator;
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
					var legacyPrefix = LegacyPrefixEnum.TryFromEncodingByte(@byte);
					if (legacyPrefix.HasValue)
					{
						if (builder.LegacyPrefixes.HasPrefixFromGroup(legacyPrefix.Value.GetGroup()))
							return AdvanceToError(InstructionDecodingError.DuplicateLegacyPrefixGroup);
						
						builder.LegacyPrefixes.Add(legacyPrefix.Value);
						return true;
					}

					var xexType = XexEnums.GetTypeFromByte(@byte);
					if (mode == InstructionDecodingMode.SixtyFourBit && xexType == XexType.RexAndEscapes)
					{
						builder.Xex = new Xex((Rex)@byte);
						return AdvanceTo(InstructionDecodingState.ExpectOpcode);
					}

					if (xexType >= XexType.Vex2)
					{
						if (mode == InstructionDecodingMode._8086)
							return AdvanceToError(InstructionDecodingError.VexIn8086Mode);

						int remainingBytes = xexType.GetMinSizeInBytes() - 1;
						// Hack: We accumulate the xex bytes, but make sure we end up with the type in the most significant byte
						accumulator = @byte | ((uint)xexType << (24 - remainingBytes * 8));
						return AdvanceTo(InstructionDecodingState.ExpectXexByte, substate: (byte)remainingBytes);
					}

					goto case InstructionDecodingState.ExpectOpcode;
				}

				case InstructionDecodingState.ExpectXexByte:
				{
					if ((accumulator >> 24) == (uint)Xop.FirstByte && (@byte & 0x04) == 0)
					{
						// What we just read was not a XOP, but a POP reg/mem
						builder.Xex = default(Xex);
						builder.OpcodeByte = (byte)Xop.FirstByte;
						builder.ModRM = (ModRM)@byte;
						state = InstructionDecodingState.ExpectModRM;
						return AdvanceToSibOrFurther();
					}

					// Accumulate xex bytes
					accumulator = (accumulator << 8) | @byte;
					--substate;
					if (substate > 0) return true; // More bytes to read

					// Thanks to our hack, we always have the type in the most significant byte now
					var xexType = (XexType)(accumulator >> 24);
					switch (xexType)
					{
						case XexType.Vex2: builder.Xex = new Xex((Vex2)accumulator); break;
						case XexType.Vex3: builder.Xex = new Xex((Vex3)accumulator); break;
						case XexType.Xop: builder.Xex = new Xex((Xop)accumulator); break;
						case XexType.EVex: builder.Xex = new Xex((EVex)accumulator); break;
						default: throw new UnreachableException();
					}

					accumulator = 0;
						
					return AdvanceTo(InstructionDecodingState.ExpectOpcode);
				}

				case InstructionDecodingState.ExpectOpcode:
				{
					if (builder.Xex.Type.AllowsEscapes())
					{
						if (builder.Xex.OpcodeMap == OpcodeMap.Default && @byte == 0x0F)
						{
							builder.Xex = builder.Xex.WithOpcodeMap(OpcodeMap.Escape0F);
							return true;
						}

						if (builder.Xex.OpcodeMap == OpcodeMap.Escape0F)
						{
							switch (@byte)
							{
								case 0x38: builder.Xex = builder.Xex.WithOpcodeMap(OpcodeMap.Escape0F38); return true;
								case 0x3A: builder.Xex = builder.Xex.WithOpcodeMap(OpcodeMap.Escape0F3A); return true;
								default: break;
							}
						}
					}

					builder.OpcodeByte = @byte;

					bool hasModRM;
					int immediateSizeInBytes;
					if (!lookup.TryLookup(mode, builder.LegacyPrefixes, builder.Xex, builder.OpcodeByte, out hasModRM, out immediateSizeInBytes))
						return AdvanceToError(InstructionDecodingError.UnknownOpcode);
					builder.ImmediateSizeInBytes = immediateSizeInBytes;
						
					return hasModRM ? AdvanceTo(InstructionDecodingState.ExpectModRM) : AdvanceToImmediateOrEnd();
				}

				case InstructionDecodingState.ExpectModRM:
				{
					builder.ModRM = (ModRM)@byte;
					return AdvanceToSibOrFurther();
				}

				case InstructionDecodingState.ExpectSib:
				{
					builder.Sib = (Sib)@byte;
					return AdvanceToDisplacementOrFurther();
				}

				case InstructionDecodingState.ExpectDisplacement:
				{
					accumulator |= (uint)@byte << (substate * 8);
					substate++;
					var displacementSize = builder.ModRM.Value.GetDisplacementSizeInBytes(
						builder.Sib.GetValueOrDefault(), GetEffectiveAddressSize());
					if (substate < displacementSize) return true; // More bytes to come

					// Sign-extend
					if (displacementSize == 1)
						builder.Displacement = unchecked((sbyte)accumulator);
					else if (displacementSize == 2)
						builder.Displacement = unchecked((short)accumulator);
					else if (displacementSize == 4)
						builder.Displacement = unchecked((int)accumulator);

					return AdvanceToImmediateOrEnd();
				}

				case InstructionDecodingState.ExpectImmediate:
				{
					builder.Immediate |= (ulong)@byte << (substate * 8);
					substate++;
					if (substate < builder.ImmediateSizeInBytes)
						return true; // More bytes to come
					
					return AdvanceTo(InstructionDecodingState.Completed);
				}

				default:
					throw new InvalidOperationException("Invalid decoding state.");
			}
		}

		public void GetInstruction(out Instruction instruction)
		{
			if (state != InstructionDecodingState.Completed)
				throw new InvalidOperationException();
			builder.Build(out instruction);
		}
		
		public Instruction GetInstruction()
		{
			Instruction instruction;
			GetInstruction(out instruction);
			return instruction;
		}

		/// <summary>
		/// Resets this <see cref="InstructionDecoder"/> to the <see cref="InstructionDecodingState.Initial"/> state.
		/// </summary>
		public void Reset()
		{
			if (state == InstructionDecodingState.Initial) return;

			state = InstructionDecodingState.Initial;
			substate = 0;
			accumulator = 0;
			builder.Clear();
			builder.DefaultAddressSize = mode.GetDefaultAddressSize();
		}

		public void Reset(InstructionDecodingMode mode)
		{
			this.mode = mode;
			Reset();
		}

		private AddressSize GetEffectiveAddressSize()
		{
			Contract.Requires(state > InstructionDecodingState.ExpectPrefixOrOpcode);
			return mode.GetEffectiveAddressSize(@override: builder.LegacyPrefixes.HasAddressSizeOverride);
		}

		private int GetDisplacementSizeInBytes()
		{
			Contract.Requires(state > InstructionDecodingState.ExpectSib);
			if (!builder.ModRM.HasValue) return 0;

			return builder.ModRM.Value.GetDisplacementSizeInBytes(
				builder.Sib.GetValueOrDefault(), GetEffectiveAddressSize());
		}

		#region AdvanceTo***
		private bool AdvanceTo(InstructionDecodingState newState, byte substate = 0)
		{
			Contract.Requires(newState > State);
			this.state = newState;
			this.substate = substate;
			return newState != InstructionDecodingState.Completed && newState != InstructionDecodingState.Error;
		}

		private bool AdvanceToError(InstructionDecodingError error)
		{
			return AdvanceTo(InstructionDecodingState.Error, substate: (byte)error);
		}

		private bool AdvanceToSibOrFurther()
		{
			Contract.Requires(State == InstructionDecodingState.ExpectModRM);
			
			return builder.ModRM.Value.ImpliesSib(GetEffectiveAddressSize())
				? AdvanceTo(InstructionDecodingState.ExpectSib)
				: AdvanceToDisplacementOrFurther();
		}

		private bool AdvanceToDisplacementOrFurther()
		{
			Contract.Requires(State >= InstructionDecodingState.ExpectModRM);
			Contract.Requires(State < InstructionDecodingState.ExpectDisplacement);

			var displacementSize = builder.ModRM.Value.GetDisplacementSizeInBytes(
				builder.Sib.GetValueOrDefault(), GetEffectiveAddressSize());
			return displacementSize > 0
				? AdvanceTo(InstructionDecodingState.ExpectDisplacement)
				: AdvanceToImmediateOrEnd();
		}

		private bool AdvanceToImmediateOrEnd()
		{
			Contract.Requires(State >= InstructionDecodingState.ExpectOpcode);
			Contract.Requires(State < InstructionDecodingState.ExpectImmediate);

			return builder.ImmediateSizeInBytes > 0
				? AdvanceTo(InstructionDecodingState.ExpectImmediate)
				: AdvanceTo(InstructionDecodingState.Completed);
		} 
		#endregion
		#endregion
	}
}
