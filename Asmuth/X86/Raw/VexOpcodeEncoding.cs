using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	/// <summary>
	/// Defines the vex-based encoding of an opcode,
	/// using the syntax from intel's ISA manuals.
	/// </summary>
	[Flags]
	public enum VexOpcodeEncoding : uint
	{
		// 2 bits
		Type_Shift = 0,
		Type_Vex = 0 << (int)Type_Shift,
		Type_Xop = 1 << (int)Type_Shift,
		Type_EVex = 2 << (int)Type_Shift,
		Type_Mask = 3 << (int)Type_Shift,

		// 2 bits
		Vvvv_Shift = 2,
		Vvvv_Invalid = 0 << (int)Vvvv_Shift,
		Vvvv_Nds = 1 << (int)Vvvv_Shift,
		Vvvv_Ndd = 2 << (int)Vvvv_Shift,
		Vvvv_Dds = 3 << (int)Vvvv_Shift,
		Vvvv_Mask = 3 << (int)Vvvv_Shift,

		// 2 bits
		VectorLength_Shift = 4,
		VectorLength_0 = 0 << (int)VectorLength_Shift,
		VectorLength_1 = 1 << (int)VectorLength_Shift,
		VectorLength_2 = 2 << (int)VectorLength_Shift,
		VectorLength_128 = VectorLength_0,
		VectorLength_256 = VectorLength_1,
		VectorLength_512 = VectorLength_2,
		VectorLength_Ignored = 3 << (int)VectorLength_Shift,
		VectorLength_Mask = 3 << (int)VectorLength_Shift,

		// 2 bits
		SimdPrefix_Shift = 6,
		SimdPrefix_None = 0 << (int)SimdPrefix_Shift,
		SimdPrefix_66 = 1 << (int)SimdPrefix_Shift,
		SimdPrefix_F2 = 2 << (int)SimdPrefix_Shift,
		SimdPrefix_F3 = 3 << (int)SimdPrefix_Shift,
		SimdPrefix_Mask = 3 << (int)SimdPrefix_Shift,

		// 2 bits
		Map_Shift = 8,
		Map_0F = 1 << (int)Map_Shift,
		Map_0F38 = 2 << (int)Map_Shift,
		Map_0F3A = 3 << (int)Map_Shift,
		Map_Xop8 = 1 << (int)Map_Shift,
		Map_Xop9 = 2 << (int)Map_Shift,
		Map_Xop10 = 3 << (int)Map_Shift,
		Map_Mask = 3 << (int)Map_Shift,

		// 2 bits
		RexW_Shift = 10,
		RexW_0 = 0 << (int)RexW_Shift,
		RexW_1 = 1 << (int)RexW_Shift,
		RexW_Ignored = 2 << (int)RexW_Shift,
		RexW_Mask = 3 << (int)RexW_Shift,

		// 8 bits
		MainByte_Shift = 12,
		MainByte_Mask = 0xFF << (int)MainByte_Shift,
		
		// 1 bit
		HasModRM = 1 << 20,

		// 2 bits
		ImmediateType_Shift = 21,
		ImmediateType_None = 0 << (int)ImmediateType_Shift,
		ImmediateType_Byte = 1 << (int)ImmediateType_Shift,
		ImmediateType_Is4 = 2 << (int)ImmediateType_Shift,
		ImmediateType_Mask = 3 << (int)ImmediateType_Shift,

		// TODO: Figure out if VexOpcodeEncoding really needs an imm8 value
		// 8 bits
		ImmediateByte_Shift = 23,
		ImmediateByte_Value = 0xFF << (int)ImmediateByte_Shift,
	}

	public static class VexOpcodeEncodingEnum
	{
		public static XexForm GetXexType(this VexOpcodeEncoding encoding)
		{
			switch (encoding & VexOpcodeEncoding.Type_Mask)
			{
				case VexOpcodeEncoding.Type_Vex: return XexForm.Vex3;
				case VexOpcodeEncoding.Type_Xop: return XexForm.Xop;
				case VexOpcodeEncoding.Type_EVex: return XexForm.EVex;
				default: throw new ArgumentException();
			}
		}

		public static byte GetMainByte(this VexOpcodeEncoding encoding)
			=> (byte)((uint)(encoding & VexOpcodeEncoding.MainByte_Mask) >> (int)VexOpcodeEncoding.MainByte_Shift);

		public static byte GetImmediateByte(this VexOpcodeEncoding encoding)
			=> (byte)((uint)(encoding & VexOpcodeEncoding.ImmediateByte_Value) >> (int)VexOpcodeEncoding.ImmediateByte_Shift);

		public static void ToOpcodeEncoding(this VexOpcodeEncoding vexEncoding, out Opcode opcode, out InstructionEncoding encoding)
		{
			opcode = default(Opcode);
			encoding = default(InstructionEncoding);

			var vexType = vexEncoding & VexOpcodeEncoding.Type_Mask;
			switch (vexType)
			{
				case VexOpcodeEncoding.Type_Vex: opcode |= Opcode.XexType_Vex; break;
				case VexOpcodeEncoding.Type_Xop: opcode |= Opcode.XexType_Xop; break;
				case VexOpcodeEncoding.Type_EVex: opcode |= Opcode.XexType_EVex; break;
				default: throw new ArgumentException();
			}

			switch (vexEncoding & VexOpcodeEncoding.SimdPrefix_Mask)
			{
				case VexOpcodeEncoding.SimdPrefix_None: opcode |= Opcode.SimdPrefix_None; break;
				case VexOpcodeEncoding.SimdPrefix_66: opcode |= Opcode.SimdPrefix_66; break;
				case VexOpcodeEncoding.SimdPrefix_F2: opcode |= Opcode.SimdPrefix_F2; break;
				case VexOpcodeEncoding.SimdPrefix_F3: opcode |= Opcode.SimdPrefix_F3; break;
				default: throw new UnreachableException();
			}

			switch (vexEncoding & VexOpcodeEncoding.VectorLength_Mask)
			{
				case VexOpcodeEncoding.VectorLength_0:
					encoding |= InstructionEncoding.VexL_Fixed;
					opcode |= Opcode.VexL_0;
					break;

				case VexOpcodeEncoding.VectorLength_1:
					encoding |= InstructionEncoding.VexL_Fixed;
					opcode |= Opcode.VexL_1;
					break;

				case VexOpcodeEncoding.VectorLength_2:
					encoding |= InstructionEncoding.VexL_Fixed;
					opcode |= Opcode.VexL_1;
					break;

				case VexOpcodeEncoding.VectorLength_Ignored:
					encoding |= InstructionEncoding.VexL_Ignored;
					break;
			}

			switch (vexEncoding & VexOpcodeEncoding.RexW_Mask)
			{
				case VexOpcodeEncoding.RexW_0:
					encoding |= InstructionEncoding.RexW_Fixed;
					break;

				case VexOpcodeEncoding.RexW_1:
					encoding |= InstructionEncoding.RexW_Fixed;
					opcode |= Opcode.RexW;
					break;

				case VexOpcodeEncoding.RexW_Ignored:
					encoding |= InstructionEncoding.RexW_Ignored;
					break;
			}

			if (vexType == VexOpcodeEncoding.Type_Xop)
			{
				switch (vexEncoding & VexOpcodeEncoding.Map_Mask)
				{
					case VexOpcodeEncoding.Map_Xop8: opcode |= Opcode.Map_Xop8; break;
					case VexOpcodeEncoding.Map_Xop9: opcode |= Opcode.Map_Xop9; break;
					case VexOpcodeEncoding.Map_Xop10: opcode |= Opcode.Map_Xop10; break;
					default: throw new ArgumentException();
				}
			}
			else
			{
				switch (vexEncoding & VexOpcodeEncoding.Map_Mask)
				{
					case VexOpcodeEncoding.Map_0F: opcode |= Opcode.Map_0F; break;
					case VexOpcodeEncoding.Map_0F38: opcode |= Opcode.Map_0F38; break;
					case VexOpcodeEncoding.Map_0F3A: opcode |= Opcode.Map_0F3A; break;
					default: throw new ArgumentException();
				}
			}

			opcode = opcode.WithMainByte(vexEncoding.GetMainByte());
			encoding |= InstructionEncoding.OpcodeFormat_FixedByte;

			if ((vexEncoding & VexOpcodeEncoding.HasModRM) != 0)
				encoding |= InstructionEncoding.ModRM_Any;

			switch (vexEncoding & VexOpcodeEncoding.ImmediateType_Mask)
			{
				case VexOpcodeEncoding.ImmediateType_None:
					encoding |= InstructionEncoding.FirstImmediateType_None;
					break;

				case VexOpcodeEncoding.ImmediateType_Byte:
					encoding = encoding.WithFirstImmediateType(ImmediateType.Imm8);
					break;

				case VexOpcodeEncoding.ImmediateType_Is4:
					encoding = encoding.WithFirstImmediateType(ImmediateType.OpcodeExtension);
					break;

				default: throw new ArgumentException();
			}

			// TODO: Vvvv
		}

		public static string ToIntelStyleString(this VexOpcodeEncoding encoding)
		{
			// Encoded length = 12-38:
			// VEX.L0.0F 42
			// EVEX.NDS.512.F3.0F3A.WIG 42 /r /is4 42
			var str = new StringBuilder(38);

			switch (encoding & VexOpcodeEncoding.Type_Mask)
			{
				case VexOpcodeEncoding.Type_Vex: str.Append("VEX"); break;
				case VexOpcodeEncoding.Type_Xop: str.Append("XOP"); break;
				case VexOpcodeEncoding.Type_EVex: str.Append("EVEX"); break;
				default: throw new ArgumentException();
			}

			switch (encoding & VexOpcodeEncoding.Vvvv_Mask)
			{
				case VexOpcodeEncoding.Vvvv_Nds: str.Append(".NDS"); break;
				case VexOpcodeEncoding.Vvvv_Ndd: str.Append(".NDD"); break;
				case VexOpcodeEncoding.Vvvv_Dds: str.Append(".DDS"); break;
				case VexOpcodeEncoding.Vvvv_Invalid: break;
				default: throw new UnreachableException();
			}

			bool isEVex = (encoding & VexOpcodeEncoding.Type_Mask) == VexOpcodeEncoding.Type_EVex;
			switch (encoding & VexOpcodeEncoding.VectorLength_Mask)
			{
				case VexOpcodeEncoding.VectorLength_Ignored: str.Append(".LIG"); break;
				case VexOpcodeEncoding.VectorLength_0: str.Append(isEVex ? ".128" : ".L0"); break;
				case VexOpcodeEncoding.VectorLength_1: str.Append(isEVex ? ".256" : ".L1"); break;
				case VexOpcodeEncoding.VectorLength_2:
					if (!isEVex) throw new ArgumentException();
					str.Append(".512");
					break;
				default: throw new UnreachableException();
			}

			switch (encoding & VexOpcodeEncoding.SimdPrefix_Mask)
			{
				case VexOpcodeEncoding.SimdPrefix_None: break;
				case VexOpcodeEncoding.SimdPrefix_66: str.Append(".66"); break;
				case VexOpcodeEncoding.SimdPrefix_F2: str.Append(".F2"); break;
				case VexOpcodeEncoding.SimdPrefix_F3: str.Append(".F3"); break;
				default: throw new UnreachableException();
			}

			if ((encoding & VexOpcodeEncoding.Type_Mask) == VexOpcodeEncoding.Type_Xop)
			{
				switch (encoding & VexOpcodeEncoding.Map_Mask)
				{
					case VexOpcodeEncoding.Map_Xop8: str.Append(".M8"); break;
					case VexOpcodeEncoding.Map_Xop9: str.Append(".M9"); break;
					case VexOpcodeEncoding.Map_Xop10: str.Append(".M10"); break;
					default: throw new ArgumentException();
				}
			}
			else
			{
				switch (encoding & VexOpcodeEncoding.Map_Mask)
				{
					case VexOpcodeEncoding.Map_0F: str.Append(".0F"); break;
					case VexOpcodeEncoding.Map_0F38: str.Append(".0F38"); break;
					case VexOpcodeEncoding.Map_0F3A: str.Append(".0F3A"); break;
					default: throw new ArgumentException();
				}
			}

			switch (encoding & VexOpcodeEncoding.RexW_Mask)
			{
				case VexOpcodeEncoding.RexW_0: str.Append(".W0"); break;
				case VexOpcodeEncoding.RexW_1: str.Append(".W1"); break;
				case VexOpcodeEncoding.RexW_Ignored: str.Append(".WIG"); break;
				default: throw new ArgumentException();
			}

			str.Append(' ');
			str.Append(GetMainByte(encoding).ToString("X2", CultureInfo.InvariantCulture));

			if ((encoding & VexOpcodeEncoding.HasModRM) == VexOpcodeEncoding.HasModRM)
				str.Append(" /r");

			switch (encoding & VexOpcodeEncoding.ImmediateType_Mask)
			{
				case VexOpcodeEncoding.ImmediateType_None: break;
				case VexOpcodeEncoding.ImmediateType_Byte: str.Append(" /ib"); break;
				case VexOpcodeEncoding.ImmediateType_Is4:
					str.Append(' ');
					str.Append(GetImmediateByte(encoding).ToString("X2", CultureInfo.InvariantCulture));
					break;
				default: throw new ArgumentException();
			}

			return str.ToString();
		}
	}
}
