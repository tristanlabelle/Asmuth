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
		Initial, // No substate
		ExpectPrefixOrOpcode,   // No substate
		ExpectXexByte,	// Substate: remaining bytes to read
		ExpectOpcode,   // Substate: 1 if primary opcode was read
		ExpectModRM, // No substate
		ExpectSib,	 // No substate
		ExpectDisplacement,	// Substate: remaining bytes to read
		ExpectImmediate, // Substate: remaining bytes to read
		Completed,	 // No substate
		Error	// No substate
	}

	public enum InstructionEncodingError : byte
	{
		DuplicateLegacyPrefixGroup,
		MultipleXex,
		UnknownOpcode
	}

	public sealed class InstructionDecoder
	{
		#region Fields
		private readonly IInstructionDecoderLookup lookup;

		// State data
		private ProcessorMode mode;
		private InstructionDecodingState state;
		private byte substate;
		private InstructionFields fields;
		private InstructionEncodingError error;

		// Instruction data
		private LegacyPrefixList legacyPrefixes;
		private Xex xex;
		private ulong immediate;
		private int displacement;
		private byte mainOpcode;
		private ModRM modRM;
		private Sib sib;
		#endregion

		#region Constructors
		public InstructionDecoder(IInstructionDecoderLookup lookup, ProcessorMode mode)
		{
			Contract.Requires(lookup != null);

			this.lookup = lookup;
			this.mode = mode;
		}
		#endregion

		#region Properties
		public ProcessorMode Mode
		{
			get { return mode; }
			set
			{
				Contract.Requires(State == InstructionDecodingState.Initial);
				mode = value;
			}
		}

		public InstructionDecodingState State => state;
		public InstructionFields Fields => fields;
		public LegacyPrefixList LegacyPrefixes => legacyPrefixes;

		public InstructionEncodingError Error
		{
			get
			{
				Contract.Requires(State == InstructionDecodingState.Error);
				return error;
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
							return AdvanceToError(InstructionEncodingError.DuplicateLegacyPrefixGroup);

						fields |= legacyPrefixGroupField;
						legacyPrefixes.Add((LegacyPrefix)@byte);
						return true;
					}

					if (((Rex)@byte & Rex.Reserved_Mask) == Rex.Reserved_Value)
					{
						xex = new Xex((Rex)@byte);
						fields |= InstructionFields.Xex;
						return AdvanceTo(InstructionDecodingState.ExpectXexByte);
					}

					if (@byte == (byte)Vex2.FirstByte || @byte == (byte)Vex3.FirstByte
						|| @byte == (byte)Xop.FirstByte || @byte == (byte)EVex.FirstByte)
					{
						xex = new Xex(XexEnums.GetTypeFromByte(@byte));
						int remainingBytes = ((XexForm)@byte).GetByteCount().Value - 1;
						return AdvanceTo(InstructionDecodingState.ExpectXexByte, substate: (byte)remainingBytes);
					}

					goto case InstructionDecodingState.ExpectOpcode;
				}

				case InstructionDecodingState.ExpectXexByte:
				{
					if (xex.Type == XexForm.Xop && (@byte & 0x04) == 0)
					{
						// What we just read was not a XOP, but a POP reg/mem
						xex = default(Xex);
						mainOpcode = (byte)Xop.FirstByte;
						modRM = (ModRM)@byte;
						fields |= InstructionFields.Opcode | InstructionFields.ModRM;
						state = InstructionDecodingState.ExpectModRM;
						return AdvanceToSibOrFurther();
					}

					immediate = (immediate << 8) | @byte;
					--substate;
					if (substate > 0) return true;   // One more byte to read

					switch (xex.Type)
					{
						case XexForm.Vex2: xex = new Xex((Vex2)immediate | Vex2.Reserved_Value); break;
						case XexForm.Vex3: xex = new Xex((Vex3)immediate | Vex3.Reserved_Value); break;
						case XexForm.Xop: xex = new Xex((Xop)immediate | Xop.Reserved_Value); break;
						case XexForm.EVex: xex = new Xex((EVex)immediate | EVex.Reserved_Value); break;
						default: throw new UnreachableException();
					}

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
					if (!lookup.TryHasModRM(mode, GetOpcode(), out hasModRM))
						AdvanceToError(InstructionEncodingError.UnknownOpcode);

					if (hasModRM) return AdvanceTo(InstructionDecodingState.ExpectModRM);

					return TryReadSuffixInfo(hasModRM: false) ? AdvanceToImmediateOrEnd() : AdvanceToError(InstructionEncodingError.UnknownOpcode);
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
					throw new NotImplementedException();
					return AdvanceToImmediateOrEnd();
				}

				case InstructionDecodingState.ExpectImmediate:
				{
					throw new NotImplementedException();
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
			state = InstructionDecodingState.Initial;
			substate = 0;
			fields = 0;
			error = 0;

			legacyPrefixes.Clear();
			immediate = 0;
			displacement = 0;
			xex = default(Xex);
			mainOpcode = 0;
			modRM = 0;
			sib = 0;
		}

		private Opcode GetOpcode()
		{
			throw new NotImplementedException();
		}

		private bool AdvanceTo(InstructionDecodingState newState, byte substate = 0)
		{
			Contract.Requires(newState > State);
			this.state = newState;
			this.substate = substate;
			return newState != InstructionDecodingState.Completed && newState != InstructionDecodingState.Error;
		}

		private bool AdvanceToError(InstructionEncodingError error)
		{
			this.error = error;
			return AdvanceTo(InstructionDecodingState.Error);
		}

		private bool AdvanceToSibOrFurther()
		{
			Contract.Requires(State == InstructionDecodingState.ExpectModRM);
			Contract.Requires(Fields.Has(InstructionFields.ModRM));

			if (!TryReadSuffixInfo(hasModRM: true))
				return AdvanceToError(InstructionEncodingError.UnknownOpcode);
			if (modRM.ImpliesSib() && sib == (Sib)0xFF)
				return AdvanceTo(InstructionDecodingState.ExpectSib);
			return AdvanceToDisplacementOrFurther();
		}

		private bool TryReadSuffixInfo(bool hasModRM)
		{
			InstructionSuffixFlags suffixFlags;
			int immediateSize;
			if (!lookup.TryGetInstructionSuffixInfo(mode, GetOpcode(), out suffixFlags, out immediateSize))
				return false;

			// Store state in fields we have yet to read.
			if ((suffixFlags & InstructionSuffixFlags.AllowSib) != 0) sib = (Sib)0xFF;
			if ((suffixFlags & InstructionSuffixFlags.AllowDisplacement) != 0)
			{
				displacement = 1;
				if ((suffixFlags & InstructionSuffixFlags.DefaultsTo16BitsAddressing) != 0)
					displacement = 3;
			}
			immediate = (byte)immediateSize;
			return true;
		}

		private bool AdvanceToDisplacementOrFurther()
		{
			Contract.Requires(State >= InstructionDecodingState.ExpectModRM);
			Contract.Requires(State < InstructionDecodingState.ExpectDisplacement);

			if (displacement != 0) // Check if the opcode allows displacements
			{
				int displacementSize = modRM.GetDisplacementSizeInBytes(addressing32: displacement == 1);
				displacement = 0;
				if (displacementSize > 0) return AdvanceTo(InstructionDecodingState.ExpectDisplacement, substate: (byte)displacementSize);
			}

			return AdvanceToImmediateOrEnd();
		}

		private bool AdvanceToImmediateOrEnd()
		{
			Contract.Requires(State >= InstructionDecodingState.ExpectOpcode);
			Contract.Requires(State < InstructionDecodingState.ExpectImmediate);

			// The size of the immediate was temporarily stored in immediate
			if (immediate > 0)
			{
				byte immediateSize = (byte)immediate;
				immediate = 0;
				return AdvanceTo(InstructionDecodingState.ExpectImmediate, substate: immediateSize);
            }

			return AdvanceTo(InstructionDecodingState.Completed);
		}
		#endregion
	}
}
