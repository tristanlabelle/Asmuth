using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Encoding
{
	public sealed class OpcodeEncodingTable<TTag> : IInstructionDecoderLookup
	{
		[DebuggerDisplay("{Opcode} => {Tag}")]
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

		private readonly Dictionary<OpcodeLookup.BucketKey, List<Entry>> buckets
			= new Dictionary<OpcodeLookup.BucketKey, List<Entry>>();

		public void Add(OpcodeEncoding opcode, TTag tag)
		{
			if (tag == null) throw new ArgumentNullException(nameof(tag));

			var bucketKey = OpcodeLookup.GetBucketKey(opcode);
			buckets.TryGetValue(bucketKey, out List<Entry> bucket);
			if (bucket == null)
			{
				bucket = new List<Entry>();
				buckets.Add(bucketKey, bucket);
			}

			bool? moreGeneral = null;
			foreach (var existingEntry in bucket)
			{
				var result = OpcodeEncoding.Compare(opcode, existingEntry.Opcode);
				switch (result)
				{
					case SetComparisonResult.Overlapping:
					case SetComparisonResult.Equal:
						throw new ArgumentException(
							$"New opcode '{opcode}' with tag '{tag}' is ambiguous with existing opcode with tag '{existingEntry.Tag}'.",
							nameof(opcode));

					case SetComparisonResult.Disjoint:
						continue;

					case SetComparisonResult.SupersetSubset:
						if (moreGeneral == false) throw new ArgumentException();
						moreGeneral = true;
						break;

					case SetComparisonResult.SubsetSuperset:
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
		
		public bool Find(in Instruction instruction, out OpcodeEncoding opcode, out TTag tag)
		{
			var bucketKey = OpcodeLookup.GetBucketKey(instruction);

			if (buckets.TryGetValue(bucketKey, out var bucket))
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
			in InstructionPrefixes prefixes, byte mainByte, ModRM? modRM, byte? imm8)
		{
			var bucketKey = OpcodeLookup.GetBucketKey(prefixes.Legacy, prefixes.NonLegacy, mainByte);
			
			if (buckets.TryGetValue(bucketKey, out var bucket))
			{
				foreach (var entry in bucket)
				{
					if (!entry.Opcode.IsMatchUpToMainByte(prefixes, mainByte))
						continue;
					
					if (modRM.HasValue)
					{
						if (!entry.Opcode.HasModRM)
							throw new ArgumentException("ModRM specified for opcode which takes none.");

						if (!entry.Opcode.AddressingForm.IsValid(modRM.Value))
							continue;
					}
					else if (entry.Opcode.HasModRM && entry.Opcode.AddressingForm != ModRMEncoding.Any)
					{
						return InstructionDecoderLookupResult.Ambiguous_RequireModRM;
					}

					if (entry.Opcode.Imm8Ext.HasValue)
					{
						if (!imm8.HasValue)
						{
							return InstructionDecoderLookupResult.Ambiguous_RequireImm8(
								hasModRM: entry.Opcode.HasModRM);
						}
						else if (imm8.Value != entry.Opcode.Imm8Ext)
						{
							continue;
						}
					}
					
					return InstructionDecoderLookupResult.Success(entry.Opcode.HasModRM,
						entry.Opcode.ImmediateSize.InBytes(prefixes), entry.Tag);
				}
			}

			return InstructionDecoderLookupResult.NotFound;
		}
	}
}
