using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Nasm
{
	public sealed class NasmInstructionDecoderLookup : IInstructionDecoderLookup
	{
		private readonly List<NasmInsnsEntry> entries;

		public NasmInstructionDecoderLookup(IEnumerable<NasmInsnsEntry> entries)
		{
			this.entries = entries.ToList();
		}

		public object TryLookup(CodeSegmentType codeSegmentType,
			ImmutableLegacyPrefixList legacyPrefixes, Xex xex, byte opcode, byte? modReg,
			out bool hasModRM, out int immediateSizeInBytes)
		{
			hasModRM = false;
			immediateSizeInBytes = 0;

			NasmInsnsEntry match = null;
			foreach (var entry in entries)
			{
				if (entry.Match(codeSegmentType, legacyPrefixes, xex, opcode,
					out bool entryHasModRM, out int entryImmediateSize))
				{
					if (match != null)
					{
						// If we match multiple, we should have the same response for each
						if (entryHasModRM != hasModRM) return false;
						if (entryImmediateSize != immediateSizeInBytes) return false;
					}

					hasModRM = entryHasModRM;
					immediateSizeInBytes = entryImmediateSize;
					match = entry;
				}
			}

			return match;
		}
	}
}
