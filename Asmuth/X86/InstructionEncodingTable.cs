using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86
{
	public sealed partial class InstructionEncodingTable : IInstructionDecoderLookup
	{
		private static readonly object lookupSuccessTag = new object();

		public static InstructionEncodingTable Instance { get; } = new InstructionEncodingTable();

		private static bool Lookup(ushort[] table, byte opcode)
			=> (table[opcode >> 4] & (1 << (opcode & 0b1111))) != 0;

		public static bool HasModRM(OpcodeMap map, byte opcode)
		{
			switch (map)
			{
				case OpcodeMap.Default: return Lookup(opcode_NoEscape_HasModRM, opcode);

				case OpcodeMap.Escape0F:
					if (!Lookup(opcode_Escape0F_HasModRMValid, opcode))
						throw new ArgumentException("Unknown opcode.");
					return Lookup(opcode_Escape0F_HasModRM, opcode);

				default: throw new NotImplementedException();
			}
		}
		
		public object TryLookup(CodeContext codeContext,
			ImmutableLegacyPrefixList legacyPrefixes, Xex xex, byte opcode, byte? modReg,
			out bool hasModRM, out int immediateSizeInBytes)
		{
			if (xex.Type != XexType.Escapes) throw new NotImplementedException();
			
			hasModRM = HasModRM(xex.OpcodeMap, opcode);
			if (!hasModRM && modReg.HasValue) throw new ArgumentException();
			
			throw new NotImplementedException();
		}
	}
}
