using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	public sealed partial class OpcodeEncodingTable<TTag> : IInstructionDecoderLookup
	{
		private readonly struct Entry
		{
			public readonly OpcodeEncoding Opcode;
			public readonly TTag Tag;

			public Entry(OpcodeEncoding opcode, TTag tag)
			{
				this.Opcode = opcode;
				this.Tag = tag;
			}
		}

		private readonly Dictionary<EncodingLookupKey, List<Entry>> buckets
			= new Dictionary<EncodingLookupKey, List<Entry>>();

		public void Add(OpcodeEncoding opcode, TTag tag)
		{
			if (tag == null) throw new ArgumentNullException(nameof(tag));

			var lookupKey = GetLookupKey(opcode);
			buckets .TryGetValue(lookupKey, out List<Entry> bucket);
			if (bucket == null)
			{
				bucket = new List<Entry>();
				buckets.Add(lookupKey, bucket);
			}

			foreach (var existingEntry in bucket)
				if (OpcodeEncoding.AreAmbiguous(opcode, existingEntry.Opcode))
					throw new ArgumentException();

			bucket.Add(new Entry(opcode, tag));
		}

		public void Add(OpcodeEncodingFlags opcodeFlags, byte mainByte, TTag tag)
			=> Add(new OpcodeEncoding(opcodeFlags, mainByte), tag);

		public void Add(OpcodeEncodingFlags opcodeFlags, byte mainByte, ModRM modRM, TTag tag)
			=> Add(new OpcodeEncoding(opcodeFlags, mainByte, modRM), tag);

		public bool Find(in Instruction instruction, out OpcodeEncoding opcode, out TTag tag)
		{
			var key = GetEncodingLookupKey(instruction);

			if (buckets.TryGetValue(key, out var bucket))
			{
				foreach (var entry in bucket)
				{
					if (entry.Opcode.IsMatch(instruction))
					{
						opcode = entry.Opcode;
						tag = entry.Tag;
						return true;
					}
				}
			}

			opcode = default;
			tag = default;
			return false;
		}
		
		object IInstructionDecoderLookup.TryLookup(
			CodeSegmentType codeSegmentType, ImmutableLegacyPrefixList legacyPrefixes,
			Xex xex, byte opcode, ModRM? modRM,
			out bool hasModRM, out int immediateSizeInBytes)
		{
			var lookupKey = GetEncodingLookupKey(legacyPrefixes, xex, opcode);
			
			if (buckets.TryGetValue(lookupKey, out var bucket))
			{
				foreach (var entry in bucket)
				{
					if (!entry.Opcode.IsMatchUpToMainByte(codeSegmentType, legacyPrefixes, xex, opcode))
						continue;
					
					if (modRM.HasValue)
					{
						if (!entry.Opcode.HasModRM)
							throw new ArgumentException("ModRM specified for opcode which takes none.");

						if (!entry.Opcode.AdmitsModRM(modRM.Value))
							continue;
					}
					else if (entry.Opcode.HasModRM && !entry.Opcode.Flags.HasAnyModRM())
					{
						// We need the ModRM bit to disambiguate
						hasModRM = true;
						immediateSizeInBytes = -1;
						return null;
					}
					
					hasModRM = entry.Opcode.HasModRM;
					immediateSizeInBytes = entry.Opcode.ImmediateSizeInBytes;
					return entry.Tag;
				}
			}
		
			// No match found
			hasModRM = false;
			immediateSizeInBytes = -1;
			return null;
		}
	}
}
