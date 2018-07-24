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

		// State data
		private InstructionDecodingState state;
		private byte substate;
		private uint accumulator;
		private readonly Instruction.Builder builder = new Instruction.Builder();
		private ulong immediateRawStorage;
		private int immediateSizeInBytes;
		private object lookupTag;
		#endregion

		#region Constructors
		public InstructionDecoder(IInstructionDecoderLookup lookup, CodeSegmentType codeSegmentType)
		{
			Contract.Requires(lookup != null);

			this.lookup = lookup;
			this.builder.CodeSegmentType = codeSegmentType;
		}

		public InstructionDecoder(CodeSegmentType codeSegmentType)
			: this(InstructionEncodingTable.Instance, codeSegmentType) { }
		#endregion

		#region Properties
		public CodeSegmentType CodeSegmentType => builder.CodeSegmentType;
		public InstructionDecodingState State => state;

		public InstructionDecodingError? Error
		{
			get
			{
				if (state != InstructionDecodingState.Error) return null;
				return (InstructionDecodingError)state;
			}
		}

		public object LookupTag
		{
			get
			{
				Contract.Requires(State > InstructionDecodingState.ExpectOpcode && State != InstructionDecodingState.Error);
				return lookupTag;
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
						if (builder.LegacyPrefixes.ContainsFromGroup(legacyPrefix.Value.GetGroup()))
							return AdvanceToError(InstructionDecodingError.DuplicateLegacyPrefixGroup);
						
						builder.LegacyPrefixes = ImmutableLegacyPrefixList.Add(builder.LegacyPrefixes, legacyPrefix.Value);
						return true;
					}

					var xexType = XexEnums.GetTypeFromByte(@byte);
					if (CodeSegmentType.IsLongMode() && xexType == XexType.RexAndEscapes)
					{
						builder.Xex = new Xex((Rex)@byte);
						return AdvanceTo(InstructionDecodingState.ExpectOpcode);
					}

					if (xexType >= XexType.Vex2)
					{
						int remainingBytes = xexType.GetMinSizeInBytes() - 1;
						// Hack: We accumulate the xex bytes, but make sure we end up with the type in the most significant byte
						accumulator = @byte | ((uint)xexType << (24 - remainingBytes * 8));
						return AdvanceTo(InstructionDecodingState.ExpectXexByte, substate: (byte)remainingBytes);
					}

					goto case InstructionDecodingState.ExpectOpcode;
				}

				case InstructionDecodingState.ExpectXexByte:
				{
					if ((accumulator >> 24) == (uint)Vex3Xop.FirstByte_Xop && (@byte & 0x04) == 0)
					{
						// What we just read was not a XOP, but a POP reg/mem
						builder.Xex = default;
						builder.OpcodeByte = (byte)Vex3Xop.FirstByte_Xop;
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
						
						case XexType.Vex3:
						case XexType.Xop:
							builder.Xex = new Xex((Vex3Xop)accumulator);
							break;

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
						
					lookupTag = lookup.TryLookup(CodeSegmentType,
						builder.LegacyPrefixes, builder.Xex, builder.OpcodeByte, modReg: null,
						out bool hasModRM, out immediateSizeInBytes);

					if (lookupTag == null)
					{
						// If we know there is a ModRM, read it and lookup again afterwards.
						if (hasModRM) return AdvanceTo(InstructionDecodingState.ExpectModRM);

						return AdvanceToError(InstructionDecodingError.UnknownOpcode);
					}

					Contract.Assert(immediateSizeInBytes >= 0);
					
					return hasModRM
						? AdvanceTo(InstructionDecodingState.ExpectModRM)
						: AdvanceToImmediateOrEnd();
				}

				case InstructionDecodingState.ExpectModRM:
				{
					builder.ModRM = (ModRM)@byte;

					// If we don't have a lookup tag yet, we needed the ModRM to complete the lookup.
					if (lookupTag == null)
					{
						lookupTag = lookup.TryLookup(CodeSegmentType,
							builder.LegacyPrefixes, builder.Xex, builder.OpcodeByte, modReg: builder.ModRM?.GetReg(),
							out bool hasModRM, out immediateSizeInBytes);
						if (!hasModRM) throw new NotSupportedException("Contradictory lookup result.");

						if (lookupTag == null || immediateSizeInBytes < 0)
							return AdvanceToError(InstructionDecodingError.UnknownOpcode);
					}

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
					var displacementSize = builder.ModRM.Value.GetDisplacementSize(
						builder.Sib.GetValueOrDefault(), GetEffectiveAddressSize());
					if (substate < displacementSize.InBytes()) return true; // More bytes to come

					// Sign-extend
					if (displacementSize == DisplacementSize._8)
						builder.Displacement = unchecked((sbyte)accumulator);
					else if (displacementSize == DisplacementSize._16)
						builder.Displacement = unchecked((short)accumulator);
					else if (displacementSize == DisplacementSize._32)
						builder.Displacement = unchecked((int)accumulator);

					return AdvanceToImmediateOrEnd();
				}

				case InstructionDecodingState.ExpectImmediate:
				{
					immediateRawStorage |= (ulong)@byte << (substate * 8);
					substate++;
					if (substate < immediateSizeInBytes)
						return true; // More bytes to come

					builder.Immediate = Immediate.FromRawStorage(immediateRawStorage, immediateSizeInBytes);
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
			GetInstruction(out Instruction instruction);
			return instruction;
		}

		/// <summary>
		/// Resets this <see cref="InstructionDecoder"/> to the <see cref="InstructionDecodingState.Initial"/> state.
		/// </summary>
		public void Reset()
		{
			if (state == InstructionDecodingState.Initial) return;

			var codeSegmentType = builder.CodeSegmentType;

			state = InstructionDecodingState.Initial;
			substate = 0;
			accumulator = 0;
			builder.Clear();
			builder.CodeSegmentType = codeSegmentType;
			immediateRawStorage = 0;
			immediateSizeInBytes = 0;
			lookupTag = null;
		}

		public void Reset(CodeSegmentType codeSegmentType)
		{
			Reset();
			builder.CodeSegmentType = codeSegmentType;
		}

		private AddressSize GetEffectiveAddressSize()
		{
			Contract.Requires(state > InstructionDecodingState.ExpectPrefixOrOpcode);
			return CodeSegmentType.GetEffectiveAddressSize(builder.LegacyPrefixes);
		}

		private DisplacementSize GetDisplacementSize()
		{
			Contract.Requires(state > InstructionDecodingState.ExpectSib);
			if (!builder.ModRM.HasValue) return 0;

			return builder.ModRM.Value.GetDisplacementSize(
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
			
			return GetDisplacementSize() > DisplacementSize._0
				? AdvanceTo(InstructionDecodingState.ExpectDisplacement)
				: AdvanceToImmediateOrEnd();
		}

		private bool AdvanceToImmediateOrEnd()
		{
			Contract.Requires(State >= InstructionDecodingState.ExpectOpcode);
			Contract.Requires(State < InstructionDecodingState.ExpectImmediate);
			
			return immediateSizeInBytes > 0
				? AdvanceTo(InstructionDecodingState.ExpectImmediate)
				: AdvanceTo(InstructionDecodingState.Completed);
		} 
		#endregion
		#endregion
	}
}
