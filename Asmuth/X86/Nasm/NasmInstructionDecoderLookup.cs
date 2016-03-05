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

		public bool TryLookup(
			InstructionDecodingMode mode, ImmutableLegacyPrefixList legacyPrefixes, Xex xex, byte opcode,
			out bool hasModRM, out int immediateSizeInBytes)
		{
			hasModRM = false;
			immediateSizeInBytes = 0;

			bool matched = false;
			foreach (var entry in entries)
			{
				bool entryHasModRM;
				int entryImmediateSize;
				if (entry.Match(mode.GetDefaultAddressSize(), legacyPrefixes, xex, opcode, out entryHasModRM, out entryImmediateSize))
				{
					if (matched)
					{
						// If we match multiple, we should have the same response for each
						if (entryHasModRM != hasModRM) return false;
						if (entryImmediateSize != immediateSizeInBytes) return false;
					}

					hasModRM = entryHasModRM;
					immediateSizeInBytes = entryImmediateSize;
					matched = true;
				}
			}

			return matched;
		}
	}
}
