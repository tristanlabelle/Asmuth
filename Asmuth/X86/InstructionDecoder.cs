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
		ExpectPrefixOrOpcode,
		ExpectXexByte,
		ExpectOpcode,
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
		MultipleXex,
		UnknownOpcode
	}

	public sealed class InstructionDecoder
	{
		#region Substates
		private struct ExpectXexByteSubstate
		{
			public uint Accumulator;
			public XexType XexType;
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
			[FieldOffset(0)] public ExpectXexByteSubstate ExpectXexByte;
			[FieldOffset(0)] public ExpectDisplacementSubstate ExpectDisplacement;
			[FieldOffset(0)] public ExpectImmediateSubstate ExpectImmediate;
			[FieldOffset(0)] public InstructionDecodingError Error;
		}
		#endregion

		#region Fields
		private readonly IInstructionDecoderLookup lookup;
		private readonly Instruction.Builder builder = new Instruction.Builder();
		private InstructionDecodingState state;
		private Substate substate;
		private int immediateSizeInBytes;
		private object lookupTag;
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
						// Allow duplicate legacy prefixes
						// write.exe has 66 66 0F 1F 84 00 00 00 00 00 = nop word ptr [rax+rax+0000000000000000h]
						if (builder.LegacyPrefixes.Contains(legacyPrefix.Value))
							return true;

						if (builder.LegacyPrefixes.ContainsFromGroup(legacyPrefix.Value.GetGroup()))
							return AdvanceToError(InstructionDecodingError.ConflictingLegacyPrefixes);
						
						builder.LegacyPrefixes = ImmutableLegacyPrefixList.Add(builder.LegacyPrefixes, legacyPrefix.Value);
						return true;
					}

					var xexType = XexEnums.SniffType(CodeSegmentType, @byte);
					if (xexType == XexType.RexAndEscapes)
					{
						builder.Xex = new Xex((Rex)@byte);
						return AdvanceTo(InstructionDecodingState.ExpectOpcode);
					}

					if (xexType != XexType.Escapes)
					{
						// Vector XEX are ambiguous with existing opcodes.
						// We must check whether the following byte is a ModRM.
						Substate newSubstate = default;
						newSubstate.ExpectXexByte.XexType = xexType;
						newSubstate.ExpectXexByte.Accumulator = @byte;
						newSubstate.ExpectXexByte.BytesRead = 1;
						newSubstate.ExpectXexByte.ByteCount = (byte)xexType.GetMinSizeInBytes();

						Debug.Assert(newSubstate.ExpectXexByte.ByteCount > 1);
						return AdvanceTo(InstructionDecodingState.ExpectXexByte, newSubstate);
					}

					state = InstructionDecodingState.ExpectOpcode;
					goto case InstructionDecodingState.ExpectOpcode;
				}

				case InstructionDecodingState.ExpectXexByte:
				{
					// The second byte determines whether we are actually parsing a XEX
					// or an instruction and its ModRM
					if (substate.ExpectXexByte.BytesRead == 1)
					{
						byte firstByte = (byte)substate.ExpectXexByte.Accumulator;
						var finalXexType = XexEnums.GetType(CodeSegmentType, firstByte, @byte);
						if (finalXexType == XexType.Escapes)
						{
							// This was not actually a XEX, but an instruction and its ModRM
							builder.Xex = default;
							builder.MainByte = firstByte;
							builder.ModRM = (ModRM)@byte;

							if (lookup.TryLookup(builder.CodeSegmentType, builder.LegacyPrefixes,
								builder.Xex, builder.MainByte, builder.ModRM,
								out bool hasModRM, out int immediateSizeInBytes) == null)
							{
								return AdvanceToError(InstructionDecodingError.UnknownOpcode);
							}

							if (!hasModRM) throw new FormatException();

							state = InstructionDecodingState.ExpectModRM;
							return AdvanceToSibOrFurther();
						}
						else
						{
							Debug.Assert(finalXexType == substate.ExpectXexByte.XexType);
						}
					}

					// Accumulate xex bytes
					substate.ExpectXexByte.Accumulator <<= 8;
					substate.ExpectXexByte.Accumulator |= @byte;
					substate.ExpectXexByte.BytesRead++;
					if (substate.ExpectXexByte.BytesRead < substate.ExpectXexByte.ByteCount)
						return true; // More bytes to read

					switch (substate.ExpectXexByte.XexType)
					{
						case XexType.Vex2:
							builder.Xex = new Xex((Vex2)substate.ExpectXexByte.Accumulator);
							break;
						
						case XexType.Vex3:
						case XexType.Xop:
							builder.Xex = new Xex((Vex3Xop)substate.ExpectXexByte.Accumulator);
							break;

						case XexType.EVex:
							builder.Xex = new Xex((EVex)substate.ExpectXexByte.Accumulator);
							break;

						default: throw new UnreachableException();
					}
					
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

					builder.MainByte = @byte;
						
					lookupTag = lookup.TryLookup(CodeSegmentType,
						builder.LegacyPrefixes, builder.Xex, builder.MainByte, modRM: null,
						out bool hasModRM, out immediateSizeInBytes);

					if (lookupTag == null)
					{
						// If we know there is a ModRM, read it and lookup again afterwards.
						if (hasModRM) return AdvanceTo(InstructionDecodingState.ExpectModRM);

						return AdvanceToError(InstructionDecodingError.UnknownOpcode);
					}

					Debug.Assert(immediateSizeInBytes >= 0);
					
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
							builder.LegacyPrefixes, builder.Xex, builder.MainByte, modRM: builder.ModRM,
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

				case InstructionDecodingState.ExpectImmediate:
				{
					substate.ExpectImmediate.Accumulator |= (ulong)@byte << (substate.ExpectImmediate.BytesRead * 8);
					substate.ExpectImmediate.BytesRead++;
					if (substate.ExpectImmediate.BytesRead < immediateSizeInBytes)
						return true; // More bytes to come

					builder.Immediate = Immediate.FromRawStorage(substate.ExpectImmediate.Accumulator, immediateSizeInBytes);
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
			substate = default;
			builder.Clear();
			builder.CodeSegmentType = codeSegmentType;
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
			Debug.Assert(state > InstructionDecodingState.ExpectPrefixOrOpcode);
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
				builder.Sib.GetValueOrDefault(), GetEffectiveAddressSize());
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

		private bool AdvanceToSibOrFurther()
		{
			Debug.Assert(State == InstructionDecodingState.ExpectModRM);
			
			return builder.ModRM.Value.ImpliesSib(GetEffectiveAddressSize())
				? AdvanceTo(InstructionDecodingState.ExpectSib)
				: AdvanceToDisplacementOrFurther();
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
			Debug.Assert(State >= InstructionDecodingState.ExpectOpcode);
			Debug.Assert(State < InstructionDecodingState.ExpectImmediate);
			
			return immediateSizeInBytes > 0
				? AdvanceTo(InstructionDecodingState.ExpectImmediate, new Substate())
				: AdvanceTo(InstructionDecodingState.Completed);
		} 
		#endregion
		#endregion
	}
}
