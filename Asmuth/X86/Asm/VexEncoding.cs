using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Asm
{
	/// <summary>
	/// Defines the vex-based encoding of an opcode,
	/// using the syntax from intel's ISA manuals.
	/// </summary>
	[Flags]
	public enum VexEncoding : ushort
	{
		// 2 bits
		Type_Shift = 0,
		Type_Vex = 0 << (int)Type_Shift,
		Type_Xop = 1 << (int)Type_Shift,
		Type_EVex = 2 << (int)Type_Shift,
		Type_Mask = 3 << (int)Type_Shift,

		// 2 bits
		NonDestructiveReg_Shift = 2,
		NonDestructiveReg_Invalid = 0 << (int)NonDestructiveReg_Shift,
		NonDestructiveReg_Source = 1 << (int)NonDestructiveReg_Shift,
		NonDestructiveReg_Dest = 2 << (int)NonDestructiveReg_Shift,
		NonDestructiveReg_SecondSource = 3 << (int)NonDestructiveReg_Shift,
		NonDestructiveReg_Mask = 3 << (int)NonDestructiveReg_Shift,

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
		RexW_Ignored = 0 << (int)RexW_Shift,
		RexW_0 = 1 << (int)RexW_Shift,
		RexW_1 = 2 << (int)RexW_Shift,
		RexW_Mask = 3 << (int)RexW_Shift,
	}

	public static class VexEncodingEnum
	{
		public static XexType GetXexType(this VexEncoding encoding)
		{
			switch (encoding & VexEncoding.Type_Mask)
			{
				case VexEncoding.Type_Vex: return XexType.Vex3;
				case VexEncoding.Type_Xop: return XexType.Xop;
				case VexEncoding.Type_EVex: return XexType.EVex;
				default: throw new ArgumentException();
			}
		}
		
		public static OpcodeEncodingFlags AsOpcodeEncodingFlags(this VexEncoding vexEncoding)
		{
			OpcodeEncodingFlags flags = default;

			var vexType = vexEncoding & VexEncoding.Type_Mask;
			switch (vexType)
			{
				case VexEncoding.Type_Vex: flags |= OpcodeEncodingFlags.XexType_Vex; break;
				case VexEncoding.Type_Xop: flags |= OpcodeEncodingFlags.XexType_Xop; break;
				case VexEncoding.Type_EVex: flags |= OpcodeEncodingFlags.XexType_EVex; break;
				default: throw new ArgumentException();
			}

			switch (vexEncoding & VexEncoding.SimdPrefix_Mask)
			{
				case VexEncoding.SimdPrefix_None: flags |= OpcodeEncodingFlags.SimdPrefix_None; break;
				case VexEncoding.SimdPrefix_66: flags |= OpcodeEncodingFlags.SimdPrefix_66; break;
				case VexEncoding.SimdPrefix_F2: flags |= OpcodeEncodingFlags.SimdPrefix_F2; break;
				case VexEncoding.SimdPrefix_F3: flags |= OpcodeEncodingFlags.SimdPrefix_F3; break;
				default: throw new ArgumentException();
			}

			switch (vexEncoding & VexEncoding.VectorLength_Mask)
			{
				case VexEncoding.VectorLength_Ignored: flags |= OpcodeEncodingFlags.VexL_Ignored; break;
				case VexEncoding.VectorLength_0: flags |= OpcodeEncodingFlags.VexL_128; break;
				case VexEncoding.VectorLength_1: flags |= OpcodeEncodingFlags.VexL_256; break;
				case VexEncoding.VectorLength_2: flags |= OpcodeEncodingFlags.VexL_512; break;
				default: throw new ArgumentException();
			}

			switch (vexEncoding & VexEncoding.RexW_Mask)
			{
				case VexEncoding.RexW_Ignored: flags |= OpcodeEncodingFlags.RexW_Ignored; break;
				case VexEncoding.RexW_0: flags |= OpcodeEncodingFlags.RexW_0; break;
				case VexEncoding.RexW_1: flags |= OpcodeEncodingFlags.RexW_1; break;
				default: throw new ArgumentException();
			}

			if (vexType == VexEncoding.Type_Xop)
			{
				switch (vexEncoding & VexEncoding.Map_Mask)
				{
					case VexEncoding.Map_Xop8: flags |= OpcodeEncodingFlags.Map_Xop8; break;
					case VexEncoding.Map_Xop9: flags |= OpcodeEncodingFlags.Map_Xop9; break;
					case VexEncoding.Map_Xop10: flags |= OpcodeEncodingFlags.Map_Xop10; break;
					default: throw new ArgumentException();
				}
			}
			else
			{
				switch (vexEncoding & VexEncoding.Map_Mask)
				{
					case VexEncoding.Map_0F: flags |= OpcodeEncodingFlags.Map_0F; break;
					case VexEncoding.Map_0F38: flags |= OpcodeEncodingFlags.Map_0F38; break;
					case VexEncoding.Map_0F3A: flags |= OpcodeEncodingFlags.Map_0F3A; break;
					default: throw new ArgumentException();
				}
			}

			// VEX.Vvvv / NonDestructiveReg is lost here

			return flags;
		}

		public static string ToIntelStyleString(this VexEncoding encoding)
		{
			// Encoded length = 12-24:
			// VEX.L0.0F 42
			// EVEX.NDS.512.F3.0F3A.WIG
			var str = new StringBuilder(24);

			switch (encoding & VexEncoding.Type_Mask)
			{
				case VexEncoding.Type_Vex: str.Append("VEX"); break;
				case VexEncoding.Type_Xop: str.Append("XOP"); break;
				case VexEncoding.Type_EVex: str.Append("EVEX"); break;
				default: throw new ArgumentException();
			}

			switch (encoding & VexEncoding.NonDestructiveReg_Mask)
			{
				case VexEncoding.NonDestructiveReg_Source: str.Append(".NDS"); break;
				case VexEncoding.NonDestructiveReg_Dest: str.Append(".NDD"); break;
				case VexEncoding.NonDestructiveReg_SecondSource: str.Append(".DDS"); break;
				case VexEncoding.NonDestructiveReg_Invalid: break;
				default: throw new UnreachableException();
			}

			bool isEVex = (encoding & VexEncoding.Type_Mask) == VexEncoding.Type_EVex;
			switch (encoding & VexEncoding.VectorLength_Mask)
			{
				case VexEncoding.VectorLength_Ignored: str.Append(".LIG"); break;
				case VexEncoding.VectorLength_0: str.Append(isEVex ? ".128" : ".L0"); break;
				case VexEncoding.VectorLength_1: str.Append(isEVex ? ".256" : ".L1"); break;
				case VexEncoding.VectorLength_2:
					if (!isEVex) throw new ArgumentException();
					str.Append(".512");
					break;
				default: throw new UnreachableException();
			}

			switch (encoding & VexEncoding.SimdPrefix_Mask)
			{
				case VexEncoding.SimdPrefix_None: break;
				case VexEncoding.SimdPrefix_66: str.Append(".66"); break;
				case VexEncoding.SimdPrefix_F2: str.Append(".F2"); break;
				case VexEncoding.SimdPrefix_F3: str.Append(".F3"); break;
				default: throw new UnreachableException();
			}

			if ((encoding & VexEncoding.Type_Mask) == VexEncoding.Type_Xop)
			{
				switch (encoding & VexEncoding.Map_Mask)
				{
					case VexEncoding.Map_Xop8: str.Append(".M8"); break;
					case VexEncoding.Map_Xop9: str.Append(".M9"); break;
					case VexEncoding.Map_Xop10: str.Append(".M10"); break;
					default: throw new ArgumentException();
				}
			}
			else
			{
				switch (encoding & VexEncoding.Map_Mask)
				{
					case VexEncoding.Map_0F: str.Append(".0F"); break;
					case VexEncoding.Map_0F38: str.Append(".0F38"); break;
					case VexEncoding.Map_0F3A: str.Append(".0F3A"); break;
					default: throw new ArgumentException();
				}
			}

			switch (encoding & VexEncoding.RexW_Mask)
			{
				case VexEncoding.RexW_Ignored: str.Append(".WIG"); break;
				case VexEncoding.RexW_0: str.Append(".W0"); break;
				case VexEncoding.RexW_1: str.Append(".W1"); break;
				default: throw new ArgumentException();
			}

			return str.ToString();
		}
	}
}
