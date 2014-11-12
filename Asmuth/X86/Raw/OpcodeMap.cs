using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	[Flags]
	public enum OpcodeMap : byte
	{
		Value_Shift = 0,
		Value_Mask = 0x1F,

		Type_Shift = 5,
		Type_Legacy = 1 << Type_Shift,
		Type_Vex = 1 << Type_Shift,
		Type_Xop = 2 << Type_Shift,
		Type_Mask = 3 << Type_Shift,

		// Enumerants
		Default = Type_Legacy | 0, // AKA one-byte
		Legacy_0F = Type_Legacy | 1,
		Legacy_0F38 = Type_Legacy | 2,
		Legacy_0F3A = Type_Legacy | 3,

		Vex_0F = Type_Vex | 1,
		Vex_0F38 = Type_Vex | 2,
		Vex_0F3A = Type_Vex | 3,

		Xop_8 = Type_Xop | 8,
		Xop_9 = Type_Xop | 9,
		Xop_10 = Type_Xop | 10,
	}

	public static class OpcodeMapEnum
	{
		[Pure]
		public static OpcodeMap WithValue(this OpcodeMap map, byte value)
		{
			Contract.Requires(value < 0x20);
			return (map & ~OpcodeMap.Value_Mask) | (OpcodeMap)value;
		}

		[Pure]
		public static byte GetValue(this OpcodeMap map)
			=> (byte)((uint)(map & OpcodeMap.Value_Mask) >> (int)OpcodeMap.Value_Shift);

		[Pure]
		public static OpcodeMap GetType(this OpcodeMap map)
			=> map & OpcodeMap.Type_Mask;
		
		[Pure]
		public static OpcodeMap TryAsVex(this OpcodeMap map)
		{
			// 0F, 0F38, 0F3A can be expressed both as VEX and with escape bytes
			var value = map.GetValue();
			return ((map & OpcodeMap.Type_Mask) == OpcodeMap.Type_Legacy && value >= 1 && value <= 3)
				? OpcodeMap.Type_Vex.WithValue(value) : map;
		}

		[Pure]
		public static OpcodeMap TryAsLegacy(this OpcodeMap map)
		{
			// 0F, 0F38, 0F3A can be expressed both as VEX and with escape bytes
			var value = map.GetValue();
			return ((map & OpcodeMap.Type_Mask) == OpcodeMap.Type_Vex && value >= 1 && value <= 3)
				? OpcodeMap.Type_Legacy.WithValue(value) : map;

		}
	}
}
