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
			buckets.TryGetValue(lookupKey, out List<Entry> bucket);
			if (bucket == null)
			{
				bucket = new List<Entry>();
				buckets.Add(lookupKey, bucket);
			}

			bool? moreGeneral = null;
			foreach (var existingEntry in bucket)
			{
				var result = OpcodeEncoding.Compare(opcode, existingEntry.Opcode);
				switch (result)
				{
					case OpcodeEncodingComparisonResult.Ambiguous:
					case OpcodeEncodingComparisonResult.Equal:
						throw new ArgumentException();

					case OpcodeEncodingComparisonResult.Different:
						continue;

					case OpcodeEncodingComparisonResult.LhsMoreGeneral:
						if (moreGeneral == false) throw new ArgumentException();
						moreGeneral = true;
						break;

					case OpcodeEncodingComparisonResult.RhsMoreGeneral:
						if (moreGeneral == true) throw new ArgumentException();
						moreGeneral = false;
						break;

					default: throw new UnreachableException();
				}
			}

			if (moreGeneral.GetValueOrDefault(true))
				bucket.Add(new Entry(opcode, tag));
			else
				bucket.Insert(0, new Entry(opcode, tag));
		}

		public void Add(OpcodeEncodingFlags opcodeFlags, byte mainByte, TTag tag)
			=> Add(new OpcodeEncoding(opcodeFlags, mainByte), tag);

		public void Add(OpcodeEncodingFlags opcodeFlags, byte mainByte, ModRM modRM, TTag tag)
			=> Add(new OpcodeEncoding(opcodeFlags, mainByte, modRM), tag);

		public void Add(OpcodeEncodingFlags opcodeFlags, byte mainByte, ModRM modRM, byte imm8, TTag tag)
			=> Add(new OpcodeEncoding(opcodeFlags, mainByte, modRM, imm8), tag);

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
		
		InstructionDecoderLookupResult IInstructionDecoderLookup.Lookup(
			CodeSegmentType codeSegmentType, ImmutableLegacyPrefixList legacyPrefixes,
			Xex xex, byte mainByte, ModRM? modRM, byte? imm8)
		{
			var lookupKey = GetEncodingLookupKey(legacyPrefixes, xex, mainByte);
			
			if (buckets.TryGetValue(lookupKey, out var bucket))
			{
				foreach (var entry in bucket)
				{
					if (!entry.Opcode.IsMatchUpToMainByte(codeSegmentType, legacyPrefixes, xex, mainByte))
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
						return InstructionDecoderLookupResult.Ambiguous_RequireModRM;
					}

					if (entry.Opcode.HasFixedImm8)
					{
						if (!imm8.HasValue)
						{
							return InstructionDecoderLookupResult.Ambiguous_RequireImm8(
								hasModRM: entry.Opcode.HasModRM);
						}
						else if (imm8.Value != entry.Opcode.Imm8)
						{
							continue;
						}
					}
					
					return InstructionDecoderLookupResult.Success(
						entry.Opcode.HasModRM, entry.Opcode.ImmediateSizeInBytes,
						entry.Tag);
				}
			}

			return InstructionDecoderLookupResult.NotFound;
		}
	}
}
