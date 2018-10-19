using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Encoding.Nasm
{
	public sealed class NasmInstructionDecoderLookup : IInstructionDecoderLookup
	{
		private readonly List<NasmInsnsEntry> entries;

		public NasmInstructionDecoderLookup(IEnumerable<NasmInsnsEntry> entries)
		{
			this.entries = entries.ToList();
		}

		public InstructionDecoderLookupResult Lookup(in InstructionPrefixes prefixes,
			byte mainByte, ModRM? modRM, byte? imm8)
		{
			bool hasModRM = false;
			int immediateSizeInBytes = 0;
			NasmInsnsEntry match = null;
			foreach (var entry in entries)
			{
				if (entry.Match(prefixes, mainByte, out bool entryHasModRM, out int entryImmediateSize))
				{
					if (match != null)
					{
						// If we match multiple, we should have the same response for each
						if (entryHasModRM != hasModRM || entryImmediateSize != immediateSizeInBytes)
						{
							Debug.Fail("Ambiguous match");
							return InstructionDecoderLookupResult.NotFound;
						}
					}

					hasModRM = entryHasModRM;
					immediateSizeInBytes = entryImmediateSize;
					match = entry;
				}
			}

			if (match == null) return InstructionDecoderLookupResult.NotFound;

			return InstructionDecoderLookupResult.Success(
				hasModRM, immediateSizeInBytes, match);
		}
	}
}
