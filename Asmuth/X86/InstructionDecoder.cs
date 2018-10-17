using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	public enum InstructionDecodingState : byte
	{
		Initial,
		ExpectPrefixOrMainOpcodeByte,
		ExpectNonLegacyPrefixByte,
		ExpectMainOpcodeByte,
		ExpectModRM,
		ExpectSib,
		ExpectDisplacement,
		ExpectImmediate,
		Completed,
		Error // substate: error enum
	}

	public enum InstructionDecodingError : byte
	{
		VexIn8086Mode,
		LockAndVex,
		SimdPrefixAndVex,
		RexAndVex,
		ConflictingLegacyPrefixes,
		MultipleNonLegacyPrefixes,
		UnknownOpcode
	}

	public sealed class InstructionDecoder
	{
		#region Substates
		private struct ExpectNonLegacyPrefixByteSubstate
		{
			public uint Accumulator;
			public NonLegacyPrefixesForm Form;
			public byte BytesRead;
			public byte ByteCount;
		}

		private struct ExpectDisplacementSubstate
		{
			public uint Accumulator;
			public byte BytesRead;
			public byte ByteCount;
		}
		
		private struct ExpectImmediateSubstate
		{
			public ulong Accumulator;
			public byte BytesRead;
		}

		[StructLayout(LayoutKind.Explicit)]
		private struct Substate
		{
			[FieldOffset(0)] public ExpectNonLegacyPrefixByteSubstate ExpectNonLegacyPrefixByte;
			[FieldOffset(0)] public ExpectDisplacementSubstate ExpectDisplacement;
			[FieldOffset(0)] public ExpectImmediateSubstate ExpectImmediate;
			[FieldOffset(0)] public InstructionDecodingError Error;
		}
		#endregion

		#region Fields
		private static readonly object failedLookupTag = new object();

		private readonly IInstructionDecoderLookup lookup;
		private readonly Instruction.Builder builder = new Instruction.Builder();
		private InstructionDecodingState state;
		private Substate substate;
		private object lookupTag = failedLookupTag;
		private int immediateSizeInBytes = -1;
		#endregion

		#region Constructors
		public InstructionDecoder(CodeSegmentType codeSegmentType, IInstructionDecoderLookup lookup)
		{
			this.lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
			this.builder.CodeSegmentType = codeSegmentType;
		}

		public InstructionDecoder(CodeSegmentType codeSegmentType)
			: this(codeSegmentType, InstructionEncodingTable.Instance) { }
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
				if (state != InstructionDecodingState.Completed)
					throw new InvalidOperationException();
				Debug.Assert(lookupTag != failedLookupTag);
				return lookupTag;
			}
		}
		#endregion

		#region Methods
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
			substate = default;
			builder.Clear();
			builder.CodeSegmentType = codeSegmentType;
			immediateSizeInBytes = -1;
			lookupTag = failedLookupTag;
		}

		public void Reset(CodeSegmentType codeSegmentType)
		{
			Reset();
			builder.CodeSegmentType = codeSegmentType;
		}

		public bool Consume(byte @byte)
		{
			switch (State)
			{
				case InstructionDecodingState.Initial:
					state = InstructionDecodingState.ExpectPrefixOrMainOpcodeByte;
					goto case InstructionDecodingState.ExpectPrefixOrMainOpcodeByte;

				case InstructionDecodingState.ExpectPrefixOrMainOpcodeByte:
					return ConsumePrefixOrMainOpcodeByte(@byte);

				case InstructionDecodingState.ExpectNonLegacyPrefixByte:
					return ConsumeNonLegacyPrefixByte(@byte);

				case InstructionDecodingState.ExpectMainOpcodeByte:
					return ConsumeMainOpcodeByte(@byte);

				case InstructionDecodingState.ExpectModRM:
					return ConsumeModRM((ModRM)@byte);

				case InstructionDecodingState.ExpectSib:
					builder.Sib = (Sib)@byte;
					return AdvanceToDisplacementOrFurther();

				case InstructionDecodingState.ExpectDisplacement:
					return ConsumeDisplacementByte(@byte);

				case InstructionDecodingState.ExpectImmediate:
					return ConsumeImmediateByte(@byte);

				default:
					throw new InvalidOperationException("Invalid decoding state.");
			}
		}

		private bool ConsumePrefixOrMainOpcodeByte(byte @byte)
		{
			// Check if we have a legacy prefix
			var legacyPrefix = LegacyPrefixEnum.TryFromEncodingByte(@byte);
			if (legacyPrefix.HasValue)
			{
				// Allow duplicate legacy prefixes
				// write.exe has 66 66 0F 1F 84 00 00 00 00 00 = nop word ptr [rax+rax+0000000000000000h]
				if (builder.LegacyPrefixes.Contains(legacyPrefix.Value))
					return true;

				if (builder.LegacyPrefixes.ContainsFromGroup(legacyPrefix.Value.GetGroup()))
					return AdvanceToError(InstructionDecodingError.ConflictingLegacyPrefixes);

				builder.LegacyPrefixes = ImmutableLegacyPrefixList.Add(builder.LegacyPrefixes, legacyPrefix.Value);
				return true;
			}

			var nonLegacyPrefixesForm = NonLegacyPrefixesFormEnum.SniffByte(CodeSegmentType, @byte);
			if (nonLegacyPrefixesForm == NonLegacyPrefixesForm.RexAndEscapes)
			{
				builder.NonLegacyPrefixes = new NonLegacyPrefixes((Rex)@byte);
				return AdvanceTo(InstructionDecodingState.ExpectMainOpcodeByte);
			}

			if (nonLegacyPrefixesForm != NonLegacyPrefixesForm.Escapes)
			{
				// VEX prefixes are ambiguous with existing opcodes.
				// We must check whether the following byte is a ModRM.
				Substate newSubstate = default;
				newSubstate.ExpectNonLegacyPrefixByte.Form = nonLegacyPrefixesForm;
				newSubstate.ExpectNonLegacyPrefixByte.Accumulator = @byte;
				newSubstate.ExpectNonLegacyPrefixByte.BytesRead = 1;
				newSubstate.ExpectNonLegacyPrefixByte.ByteCount = (byte)nonLegacyPrefixesForm.GetMinSizeInBytes();

				Debug.Assert(newSubstate.ExpectNonLegacyPrefixByte.ByteCount > 1);
				return AdvanceTo(InstructionDecodingState.ExpectNonLegacyPrefixByte, newSubstate);
			}

			state = InstructionDecodingState.ExpectMainOpcodeByte;
			return ConsumeMainOpcodeByte(@byte);
		}

		private bool ConsumeNonLegacyPrefixByte(byte @byte)
		{
			Debug.Assert(state == InstructionDecodingState.ExpectNonLegacyPrefixByte);

			// The second byte determines whether we are actually parsing a VEX
			// or an instruction and its ModRM
			if (substate.ExpectNonLegacyPrefixByte.BytesRead == 1)
			{
				byte firstByte = (byte)substate.ExpectNonLegacyPrefixByte.Accumulator;
				var finalForm = NonLegacyPrefixesFormEnum.FromBytes(CodeSegmentType, firstByte, @byte);
				if (finalForm == NonLegacyPrefixesForm.Escapes)
				{
					// This was not actually a VEX, but an instruction and its ModRM
					builder.NonLegacyPrefixes = default;
					builder.MainByte = firstByte;

					state = InstructionDecodingState.ExpectModRM;
					return ConsumeModRM((ModRM)@byte);
				}
				else
				{
					Debug.Assert(finalForm == substate.ExpectNonLegacyPrefixByte.Form);
				}
			}

			// Accumulate non-legacy prefix bytes
			substate.ExpectNonLegacyPrefixByte.Accumulator <<= 8;
			substate.ExpectNonLegacyPrefixByte.Accumulator |= @byte;
			substate.ExpectNonLegacyPrefixByte.BytesRead++;
			if (substate.ExpectNonLegacyPrefixByte.BytesRead < substate.ExpectNonLegacyPrefixByte.ByteCount)
				return true; // More bytes to read

			switch (substate.ExpectNonLegacyPrefixByte.Form)
			{
				case NonLegacyPrefixesForm.Vex2:
					builder.NonLegacyPrefixes = new NonLegacyPrefixes(new Vex2(
						(byte)(substate.ExpectNonLegacyPrefixByte.Accumulator >> 8),
						(byte)substate.ExpectNonLegacyPrefixByte.Accumulator));
					break;

				case NonLegacyPrefixesForm.Vex3:
				case NonLegacyPrefixesForm.Xop:
					builder.NonLegacyPrefixes = new NonLegacyPrefixes(new Vex3Xop(
						(byte)(substate.ExpectNonLegacyPrefixByte.Accumulator >> 16),
						(byte)(substate.ExpectNonLegacyPrefixByte.Accumulator >> 8),
						(byte)substate.ExpectNonLegacyPrefixByte.Accumulator));
					break;

				case NonLegacyPrefixesForm.EVex:
					builder.NonLegacyPrefixes = new NonLegacyPrefixes(new EVex(
						(byte)(substate.ExpectNonLegacyPrefixByte.Accumulator >> 24),
						(byte)(substate.ExpectNonLegacyPrefixByte.Accumulator >> 16),
						(byte)(substate.ExpectNonLegacyPrefixByte.Accumulator >> 8),
						(byte)substate.ExpectNonLegacyPrefixByte.Accumulator));
					break;

				default: throw new UnreachableException();
			}

			return AdvanceTo(InstructionDecodingState.ExpectMainOpcodeByte);
		}

		private bool ConsumeMainOpcodeByte(byte @byte)
		{
			if (builder.NonLegacyPrefixes.Form.AllowsEscapes())
			{
				if (builder.NonLegacyPrefixes.OpcodeMap == OpcodeMap.Default && @byte == 0x0F)
				{
					builder.NonLegacyPrefixes = builder.NonLegacyPrefixes.WithOpcodeMap(OpcodeMap.Escape0F);
					return true;
				}

				if (builder.NonLegacyPrefixes.OpcodeMap == OpcodeMap.Escape0F)
				{
					switch (@byte)
					{
						case 0x38: builder.NonLegacyPrefixes = builder.NonLegacyPrefixes.WithOpcodeMap(OpcodeMap.Escape0F38); return true;
						case 0x3A: builder.NonLegacyPrefixes = builder.NonLegacyPrefixes.WithOpcodeMap(OpcodeMap.Escape0F3A); return true;
						default: break;
					}
				}
			}

			builder.MainByte = @byte;

			var lookupResult = lookup.Lookup(CodeSegmentType,
				builder.LegacyPrefixes, builder.NonLegacyPrefixes,
				builder.MainByte, modRM: null, imm8: null);
			if (lookupResult.IsNotFound)
				return AdvanceToError(InstructionDecodingError.UnknownOpcode);

			if (lookupResult.HasImmediateSize)
				immediateSizeInBytes = lookupResult.ImmediateSizeInBytes;

			if (lookupResult.IsSuccess)
				lookupTag = lookupResult.Tag;

			return lookupResult.HasModRM
				? AdvanceTo(InstructionDecodingState.ExpectModRM)
				: AdvanceToImmediateOrEnd();
		}

		private bool ConsumeModRM(ModRM modRM)
		{
			Debug.Assert(state == InstructionDecodingState.ExpectModRM);

			builder.ModRM = modRM;

			// If the main opcode byte wasn't enough to lookup the opcode, repeat the lookup now
			if (lookupTag == failedLookupTag)
			{
				var lookupResult = lookup.Lookup(builder.CodeSegmentType, builder.LegacyPrefixes,
					builder.NonLegacyPrefixes, builder.MainByte, modRM, imm8: null);
				switch (lookupResult.Status)
				{
					case InstructionDecoderLookupStatus.Success:
					case InstructionDecoderLookupStatus.Ambiguous_RequireImm8:
						if (!lookupResult.HasModRM) throw new FormatException();
						immediateSizeInBytes = lookupResult.ImmediateSizeInBytes;
						if (lookupResult.IsSuccess) lookupTag = lookupResult.Tag;
						break;

					case InstructionDecoderLookupStatus.NotFound:
						return AdvanceToError(InstructionDecodingError.UnknownOpcode);

					case InstructionDecoderLookupStatus.Ambiguous_RequireModRM:
					default:
						throw new UnreachableException();
				}
			}
			
			return modRM.ImpliesSib(GetEffectiveAddressSize())
				? AdvanceTo(InstructionDecodingState.ExpectSib)
				: AdvanceToDisplacementOrFurther();
		}

		private bool ConsumeDisplacementByte(byte @byte)
		{
			substate.ExpectDisplacement.Accumulator |= (uint)@byte << (substate.ExpectDisplacement.BytesRead * 8);
			substate.ExpectDisplacement.BytesRead++;
			if (substate.ExpectDisplacement.BytesRead < substate.ExpectDisplacement.ByteCount)
				return true; // More bytes to come

			// Sign-extend
			if (substate.ExpectDisplacement.ByteCount == 1)
				builder.Displacement = unchecked((sbyte)substate.ExpectDisplacement.Accumulator);
			else if (substate.ExpectDisplacement.ByteCount == 2)
				builder.Displacement = unchecked((short)substate.ExpectDisplacement.Accumulator);
			else if (substate.ExpectDisplacement.ByteCount == 4)
				builder.Displacement = unchecked((int)substate.ExpectDisplacement.Accumulator);
			else
				throw new UnreachableException();

			return AdvanceToImmediateOrEnd();
		}

		private bool ConsumeImmediateByte(byte @byte)
		{
			substate.ExpectImmediate.Accumulator |= (ulong)@byte << (substate.ExpectImmediate.BytesRead * 8);
			substate.ExpectImmediate.BytesRead++;
			if (substate.ExpectImmediate.BytesRead < immediateSizeInBytes)
				return true; // More bytes to come

			builder.Immediate = ImmediateData.FromRawStorage(substate.ExpectImmediate.Accumulator, immediateSizeInBytes);

			// If the opcode is disambigued based on the imm8, we must look it up here.
			if (lookupTag == failedLookupTag)
			{
				Debug.Assert(immediateSizeInBytes == 1);

				var lookupResult = lookup.Lookup(builder.CodeSegmentType, builder.LegacyPrefixes,
					builder.NonLegacyPrefixes, builder.MainByte, builder.ModRM, imm8: @byte);
				switch (lookupResult.Status)
				{
					case InstructionDecoderLookupStatus.Success:
						lookupTag = lookupResult.Tag;
						break;

					case InstructionDecoderLookupStatus.NotFound:
						return AdvanceToError(InstructionDecodingError.UnknownOpcode);

					case InstructionDecoderLookupStatus.Ambiguous_RequireModRM:
					case InstructionDecoderLookupStatus.Ambiguous_RequireImm8:
					default:
						throw new UnreachableException();
				}
			}

			return AdvanceTo(InstructionDecodingState.Completed);
		}

		private AddressSize GetEffectiveAddressSize()
		{
			Debug.Assert(state > InstructionDecodingState.ExpectPrefixOrMainOpcodeByte);
			return CodeSegmentType.GetEffectiveAddressSize(builder.LegacyPrefixes);
		}

		private DisplacementSize GetDisplacementSize()
		{
			Debug.Assert(state >= InstructionDecodingState.ExpectModRM);
			Debug.Assert(!builder.ModRM.HasValue
				|| !builder.ModRM.Value.ImpliesSib(GetEffectiveAddressSize())
				|| state >= InstructionDecodingState.ExpectSib);
			if (!builder.ModRM.HasValue) return 0;

			return builder.ModRM.Value.GetDisplacementSize(
				GetEffectiveAddressSize(), builder.Sib.GetValueOrDefault());
		}

		#region AdvanceTo***
		private bool AdvanceTo(InstructionDecodingState newState)
		{
			this.substate = default;
			return AdvanceTo_NoSubstate(newState);
		}

		private bool AdvanceTo(InstructionDecodingState newState, in Substate substate)
		{
			this.substate = substate;
			return AdvanceTo_NoSubstate(newState);
		}

		private bool AdvanceTo_NoSubstate(InstructionDecodingState newState)
		{
			Debug.Assert(newState > State);
			this.state = newState;
			return newState != InstructionDecodingState.Completed
				&& newState != InstructionDecodingState.Error;
		}

		private bool AdvanceToError(InstructionDecodingError error)
		{
			Substate substate = default;
			substate.Error = error;
			return AdvanceTo(InstructionDecodingState.Error, substate);
		}

		private bool AdvanceToDisplacementOrFurther()
		{
			Debug.Assert(State >= InstructionDecodingState.ExpectModRM);
			Debug.Assert(State < InstructionDecodingState.ExpectDisplacement);

			var displacementSize = GetDisplacementSize();
			if (displacementSize != DisplacementSize.None)
			{
				Substate newSubstate = default;
				newSubstate.ExpectDisplacement.ByteCount = (byte)displacementSize.InBytes();
				return AdvanceTo(InstructionDecodingState.ExpectDisplacement, newSubstate);
			}
			else
			{
				return AdvanceToImmediateOrEnd();
			}
		}

		private bool AdvanceToImmediateOrEnd()
		{
			Debug.Assert(State >= InstructionDecodingState.ExpectMainOpcodeByte);
			Debug.Assert(State < InstructionDecodingState.ExpectImmediate);
			
			return immediateSizeInBytes > 0
				? AdvanceTo(InstructionDecodingState.ExpectImmediate, new Substate())
				: AdvanceTo(InstructionDecodingState.Completed);
		} 
		#endregion
		#endregion
	}
}
