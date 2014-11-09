using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	[Flags]
	public enum InstructionEncoding : ulong
	{
		// The xex type of the opcode
		XexType_Shift = 0,
		XexType_Legacy = 0 << (int)XexType_Shift,
		XexType_Vex = 1 << (int)XexType_Shift,
		XexType_Xop = 2 << (int)XexType_Shift,
		XexType_EVex = 3 << (int)XexType_Shift,
		XexType_Mask = 3 << (int)XexType_Shift,

		// The instruction's possible operand sizes
		OperandSize_Shift = XexType_Shift + 2,
		OperandSize_Irrelevant = 0 << (int)OperandSize_Shift,
		OperandSize_Fixed8 = 1 << (int)OperandSize_Shift,
		OperandSize_Fixed16 = 2 << (int)OperandSize_Shift,
		OperandSize_Fixed32 = 3 << (int)OperandSize_Shift,
		OperandSize_Fixed64 = 4 << (int)OperandSize_Shift,
		OperandSize_16To64 = 5 << (int)OperandSize_Shift,
		OperandSize_32Or64 = 6 << (int)OperandSize_Shift,
		OperandSize_64Or16 = 7 << (int)OperandSize_Shift,
		OperandSize_Mask = 7 << (int)OperandSize_Shift,

		// The SIMD prefix (through a prefix or VEX/XOP/EVEX.pp)
		SimdPrefix_Shift = OperandSize_Shift + 3,
		SimdPrefix_None = 0 << (int)SimdPrefix_Shift,
		SimdPrefix_66 = 1 << (int)SimdPrefix_Shift,
		SimdPrefix_F2 = 2 << (int)SimdPrefix_Shift,
		SimdPrefix_F3 = 3 << (int)SimdPrefix_Shift,
		SimdPrefix_Mask = 3 << (int)SimdPrefix_Shift,

		// The opcode map (through prefixes, VEX/XOP/EVEX.mm[mmm])
		OpcodeMap_Shift = SimdPrefix_Shift + 2,
		OpcodeMap_OneByte = 0 << (int)OpcodeMap_Shift,
		OpcodeMap_0F = 1 << (int)OpcodeMap_Shift,
		OpcodeMap_0F38 = 2 << (int)OpcodeMap_Shift,
		OpcodeMap_0F3A = 3 << (int)OpcodeMap_Shift,
		OpcodeMap_Xop8 = 8 << (int)OpcodeMap_Shift,
		OpcodeMap_Xop9 = 9 << (int)OpcodeMap_Shift,
		OpcodeMap_Xop10 = 10 << (int)OpcodeMap_Shift,
		OpcodeMap_Mask = 0x1F << (int)OpcodeMap_Shift,

		// What the REX.W field encodes
		RexW_Shift = OpcodeMap_Shift + 5,
		RexW_Ignored = 0 << (int)RexW_Shift,
		RexW_OperandSize = 1 << (int)RexW_Shift,
		RexW_0 = 2 << (int)RexW_Shift,
		RexW_1 = 3 << (int)RexW_Shift,
		RexW_Mask = 3 << (int)RexW_Shift,

		// What the VEX.L / EVEX.L'L fields encode (if applicable)
		VexL_Shift = RexW_Shift + 2,
		VexL_0 = 0 << (int)VexL_Shift,
		VexL_1 = 1 << (int)VexL_Shift,
		VexL_128 = VexL_0,
		VexL_256 = VexL_1,
		VexL_VectorLength_128Or256 = 2 << (int)VexL_Shift,
		VexL_VectorLength_128Or256Or512 = 3 << (int)VexL_Shift,
		VexL_Ignored = 4 << (int)VexL_Shift,
		VexL_RoundingControl = 5 << (int)VexL_Shift,
		VexL_Mask = 7 << (int)VexL_Shift,

		// Which bits encode the opcode itself
		OpcodeFormat_Shift = VexL_Shift + 3,
		OpcodeFormat_FixedByte = 0 << (int)OpcodeFormat_Shift, // Full byte is fixed
		OpcodeFormat_EmbeddedRegister = 1 << (int)OpcodeFormat_Shift, // Low three bits of main byte specify a register
		OpcodeFormat_EmbeddedConditionCode = 2 << (int)OpcodeFormat_Shift, // Low four bits of main byte specify a condition code
		OpcodeFormat_Mask = 3 << (int)OpcodeFormat_Shift,

		// The opcode main byte value
		OpcodeByte_Shift = OpcodeFormat_Shift + 2,
		OpcodeByte_NoEmbeddedRegisterMask = 0xF8UL << (int)OpcodeByte_Shift,
		OpcodeByte_NoEmbeddedConditionCodeMask = 0xF0UL << (int)OpcodeByte_Shift,
		OpcodeByte_Mask = 0xFFUL << (int)OpcodeByte_Shift,

		// How the ModRM (and sib bytes) are used
		ModRM_Shift = OpcodeByte_Shift + 8,
		ModRM_None = 0 << (int)ModRM_Shift,	// NOP
		ModRM_FixedReg = 1 << (int)ModRM_Shift,
		ModRM_FixedModReg = 2 << (int)ModRM_Shift, // FADD ST(i), ST(0) : DC C0+i
		ModRM_Fixed = 3 << (int)ModRM_Shift,
		ModRM_Any = 4 << (int)ModRM_Shift,
		ModRM_Mask = 7 << (int)ModRM_Shift,

		// The opcode extra byte value (ModRM or 3DNow! imm8, not always used)
		OpcodeExtraByte_Shift = ModRM_Shift + 3,
		OpcodeExtraByte_Mask = 0xFFUL << (int)OpcodeExtraByte_Shift,

		// The number of bytes of immediate value
		ImmediateSize_Shift = OpcodeExtraByte_Shift + 8,
		ImmediateSize_0 = 0UL << (int)ImmediateSize_Shift,
		ImmediateSize_8 = 1UL << (int)ImmediateSize_Shift,
		ImmediateSize_16 = 2UL << (int)ImmediateSize_Shift, // RET
		ImmediateSize_24 = 3UL << (int)ImmediateSize_Shift, // ENTER iw, ib
		ImmediateSize_32 = 4UL << (int)ImmediateSize_Shift,
		ImmediateSize_48 = 5UL << (int)ImmediateSize_Shift,
		ImmediateSize_64 = 6UL << (int)ImmediateSize_Shift,
		ImmediateSize_16Or32 = 7UL << (int)ImmediateSize_Shift,
		ImmediateSize_16Or32Or64 = 8UL << (int)ImmediateSize_Shift,
		ImmediateSize_32Or48 = 9UL << (int)ImmediateSize_Shift, // CALL (9A)
		ImmediateSize_Mask = 0xFUL << (int)ImmediateSize_Shift,
	}

	public static class InstructionEncodingEnum
	{
		#region Field Queries
		[Pure]
		public static byte GetOpcodeByte(this InstructionEncoding encoding)
			=> (byte)Bits.MaskAndShiftRight((ulong)encoding, (ulong)InstructionEncoding.OpcodeByte_Mask, (int)InstructionEncoding.OpcodeByte_Shift);

		[Pure]
		public static byte GetOpcodeExtraByte(this InstructionEncoding encoding)
			=> (byte)Bits.MaskAndShiftRight((ulong)encoding, (ulong)InstructionEncoding.OpcodeExtraByte_Mask, (int)InstructionEncoding.OpcodeExtraByte_Shift);

		[Pure]
		public static bool HasModRM(this InstructionEncoding encoding)
			=> (encoding & InstructionEncoding.ModRM_Mask) != InstructionEncoding.ModRM_None;
		#endregion

		#region With***
		[Pure]
		public static InstructionEncoding WithOperandSize(this InstructionEncoding encoding, InstructionEncoding operandSize)
		{
			Contract.Requires((operandSize & ~InstructionEncoding.OperandSize_Mask) == 0);
			return (encoding & ~InstructionEncoding.OperandSize_Mask) | operandSize;
		}

		[Pure]
		public static InstructionEncoding WithOpcodeMap(this InstructionEncoding encoding, InstructionEncoding opcodeMap)
		{
			Contract.Requires((opcodeMap & ~InstructionEncoding.OpcodeMap_Mask) == 0);
			return (encoding & ~InstructionEncoding.OpcodeMap_Mask) | opcodeMap;
		}

		[Pure]
		public static InstructionEncoding WithOpcodeFormat(this InstructionEncoding encoding, InstructionEncoding format)
		{
			Contract.Requires((format & ~InstructionEncoding.OpcodeFormat_Mask) == 0);
			return (encoding & ~InstructionEncoding.OpcodeFormat_Mask) | format;
		}

		[Pure]
		public static InstructionEncoding WithOpcodeByte(this InstructionEncoding encoding, byte @byte)
			=> (encoding & ~InstructionEncoding.OpcodeByte_Mask) | (InstructionEncoding)((ulong)@byte << (int)InstructionEncoding.OpcodeByte_Shift);

		[Pure]
		public static InstructionEncoding WithOpcode(this InstructionEncoding encoding, InstructionEncoding format, byte @byte)
			=> WithOpcodeByte(WithOpcodeFormat(encoding, format), @byte);

		[Pure]
		public static InstructionEncoding WithModRM(this InstructionEncoding encoding, InstructionEncoding format)
		{
			Contract.Requires((format & ~InstructionEncoding.ModRM_Mask) == 0);
			return (encoding & ~InstructionEncoding.ModRM_Mask) | format;
		}

		[Pure]
		public static InstructionEncoding WithModRM(this InstructionEncoding encoding, InstructionEncoding format, byte @byte)
			=> WithOpcodeExtraByte(WithModRM(encoding, format), @byte);

		[Pure]
		public static InstructionEncoding WithOpcodeExtraByte(this InstructionEncoding encoding, byte @byte)
			=> (encoding & ~InstructionEncoding.OpcodeExtraByte_Mask) | (InstructionEncoding)((ulong)@byte << (int)InstructionEncoding.OpcodeExtraByte_Shift);

		[Pure]
		public static InstructionEncoding WithImmediateSize(this InstructionEncoding encoding, InstructionEncoding size)
		{
			Contract.Requires((size & ~InstructionEncoding.ImmediateSize_Mask) == 0);
			return (encoding & ~InstructionEncoding.ImmediateSize_Mask) | size;
		}  
		#endregion

		[Pure]
		public static string ToIntelStyleString(this InstructionEncoding encoding)
		{
			var str = new StringBuilder(30);

			var xexType = encoding & InstructionEncoding.XexType_Mask;
			if (xexType == InstructionEncoding.XexType_Legacy)
			{
				// Prefixes
				switch (encoding & InstructionEncoding.SimdPrefix_Mask)
				{
					case InstructionEncoding.SimdPrefix_None: break;
					case InstructionEncoding.SimdPrefix_66: str.Append("66 "); break;
					case InstructionEncoding.SimdPrefix_F2: str.Append("F2 "); break;
					case InstructionEncoding.SimdPrefix_F3: str.Append("F3 "); break;
					default: throw new UnreachableException();
				}

				if ((encoding & InstructionEncoding.RexW_Mask) == InstructionEncoding.RexW_1)
					str.Append("REX.W ");

				switch (encoding & InstructionEncoding.OpcodeMap_Mask)
				{
					case InstructionEncoding.OpcodeMap_OneByte: break;
					case InstructionEncoding.OpcodeMap_0F: str.Append("0F "); break;
					case InstructionEncoding.OpcodeMap_0F38: str.Append("0F38 "); break;
					case InstructionEncoding.OpcodeMap_0F3A: str.Append("0F3A "); break;
					default: throw new UnreachableException();
				}

				// The opcode itself
				str.AppendFormat(CultureInfo.InvariantCulture, "X2", GetOpcodeByte(encoding));

				// Suffixes
				switch (encoding & InstructionEncoding.OpcodeFormat_Mask)
				{
					case InstructionEncoding.OpcodeFormat_FixedByte: break;
					case InstructionEncoding.OpcodeFormat_EmbeddedRegister: str.Append("+r"); break;
					case InstructionEncoding.OpcodeFormat_EmbeddedConditionCode: str.Append("+cc"); break;
					default: throw new UnreachableException();
				}

				switch (encoding & InstructionEncoding.ModRM_Mask)
				{
					case InstructionEncoding.ModRM_Fixed:
						str.AppendFormat(CultureInfo.InvariantCulture, "X2", GetOpcodeExtraByte(encoding));
						break;

					case InstructionEncoding.ModRM_FixedModReg:
						str.AppendFormat(CultureInfo.InvariantCulture, "X2", GetOpcodeExtraByte(encoding));
						str.Append("+r");
						break;

					case InstructionEncoding.ModRM_FixedReg:
						str.Append('/');
						str.AppendFormat(CultureInfo.InvariantCulture, "D", GetOpcodeExtraByte(encoding) >> 3);
						break;

					case InstructionEncoding.ModRM_Any: str.Append(" /r"); break;
					case InstructionEncoding.ModRM_None: break;
					default: throw new UnreachableException();
				}

				throw new NotImplementedException();
			}
			else
			{
				throw new NotImplementedException();
			}

			return str.ToString();
		}
	}
}
