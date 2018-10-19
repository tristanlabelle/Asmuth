using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Encoding.Xed
{
	public enum XedError : byte
	{
		[XedEnumName("NONE")] None,
		[XedEnumName("BUFFER_TOO_SHORT")] BufferTooShort,
		[XedEnumName("GENERAL_ERROR")] GeneralError,
		[XedEnumName("INVALID_FOR_CHIP")] InvalidForChip,
		[XedEnumName("BAD_REGISTER")] BadRegister,
		[XedEnumName("BAD_LOCK_PREFIX")] BadLockPrefix,
		[XedEnumName("BAD_REP_PREFIX")] BadRepPrefix,
		[XedEnumName("BAD_LEGACY_PREFIX")] BadLegacyPrefix,
		[XedEnumName("BAD_REX_PREFIX")] BadRexPrefix,
		[XedEnumName("BAD_EVEX_UBIT")] BadEVexUBit,
		[XedEnumName("BAD_MAP")] BadMap,
		[XedEnumName("BAD_EVEX_V_PRIME")] BadEVexVPrime,
		[XedEnumName("BAD_EVEX_Z_NO_MASKING")] BadEVexZNoMasking,
		[XedEnumName("NO_OUTPUT_POINTER")] NoOutputPointer,
		[XedEnumName("NO_AGEN_CALL_BACK_REGISTERED")] NoAGenCallBackRegistered,
		[XedEnumName("BAD_MEMOP_INDEX")] BadMemopIndex,
		[XedEnumName("CALLBACK_PROBLEM")] CallbackProblem,
		[XedEnumName("GATHER_REGS")] GatherRegs,
		[XedEnumName("INSTR_TOO_LONG")] InstructionTooLong,
		[XedEnumName("INVALID_MODE")] InvalidMode,
		[XedEnumName("BAD_EVEX_LL")] BadEVexLL,
	}
}
