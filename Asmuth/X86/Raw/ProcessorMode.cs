using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	// See AMD64 APM Vol2
	//   Figure 1-6. Operating Modes of the AMD64 Architecture
	//   Table 14-4. Processor Operating Modes
	[Flags]
	public enum ProcessorMode : byte
	{
		CR0_PE = 1 << 0, // Real -> Protected
		EFlags_VM = 1 << 1, // Protected -> Virtual
		CR0_PG = 1 << 2, // Protected -> Compatibility
		Efer_Lme = CR0_PG, // Protected -> Compatibility
		Efer_Lma = Efer_Lme, // Protected -> Compatibility
		CS_L = 1 << 3, // Compatibility -> 64-bit
		CS_D = 1 << 4, // 16->32 bits addresses/operands

		// Legacy
		Real = 0,
		Protected = CR0_PE,
		Virtual8086 = CR0_PE | EFlags_VM,
		Real_Default16 = Real,
		Protected_Default16 = Protected,
		Virtual8086_Default16 = Virtual8086,
		Real_Default32 = Real | CS_D,
		Protected_Default32 = Protected | CS_D,
		Virtual8086_Default32 = Virtual8086 | CS_D,

		// Long
		Compatibility = CR0_PE | CR0_PG,
        Compatibility_Default16 = Compatibility,
		Compatibility_Default32 = Compatibility | CS_D,
		SixtyFourBits = CR0_PE | CR0_PG | CS_L
	}

	public static class ProcessorModeEnum
	{
		[Pure]
		public static bool HasFlags(this ProcessorMode mode, ProcessorMode flags)
			=> (mode & flags) == flags;

		[Pure]
		public static bool IsLegacy(this ProcessorMode mode)
			=> !HasFlags(mode, ProcessorMode.Efer_Lma);

		[Pure]
		public static bool IsLong(this ProcessorMode mode)
			=> HasFlags(mode, ProcessorMode.Efer_Lma);

		[Pure]
		public static int GetAddressSizeInBytes(this ProcessorMode mode, bool addressSizePrefix)
		{
			// AMD APM Vol. 3, Table 1-3. Address-Size Overrides
			if (mode == ProcessorMode.SixtyFourBits) return addressSizePrefix ? 4 : 8;
			return (HasFlags(mode, ProcessorMode.CS_D) == addressSizePrefix) ? (byte)2 : (byte)4;
		}
	}
}
