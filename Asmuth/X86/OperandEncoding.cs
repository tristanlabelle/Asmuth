using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	[Flags]
	public enum OperandEncoding : ushort
	{
		Index_Shift = 0,
		Index_Unspecified = 0 << Index_Shift,
		Index_0 = 4 << Index_Shift,
		Index_1 = 5 << Index_Shift,
		Index_2 = 6 << Index_Shift,
		Index_3 = 7 << Index_Shift,
		Index_Mask = 7 << Index_Shift,

		Type_Shift = Index_Shift + 3,
		Type_Gpr = 0 << Type_Shift,
		Type_GprOrMem = 1 << Type_Shift,
		Type_Xmm = 2 << Type_Shift,
		Type_XmmOrMem = 3 << Type_Shift,
		Type_Mmx = 4 << Type_Shift,
		Type_MmxOrMem = 5 << Type_Shift,
		Type_Mem = 6 << Type_Shift,
		Type_Segment = 7 << Type_Shift,	// For some forms of MOV
		Type_Debug = 8 << Type_Shift,  // For some forms of MOV
		Type_KMask = 9 << Type_Shift,  // In EVEX encoding
		Type_Mask = 0xF << Type_Shift
	}
}
