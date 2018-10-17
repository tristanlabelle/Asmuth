using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	/// <summary>
	/// Provides an <see cref="InstructionDecoder"/> with the means to query encoding information from an instruction database.
	/// </summary>
	public interface IInstructionDecoderLookup
	{
		InstructionDecoderLookupResult Lookup(in InstructionPrefixes prefixes,
			byte mainByte, ModRM? modRM, byte? imm8);
	}

	public readonly struct InstructionDecoderLookupResult
	{
		private readonly object tag;
		private readonly InstructionDecoderLookupStatus status;
		private readonly byte immediateSizeInBytes;
		private readonly bool hasModRM;

		private InstructionDecoderLookupResult(InstructionDecoderLookupStatus status,
			bool hasModRM = false, byte immediateSizeInBytes = 0, object tag = null)
		{
			this.status = status;
			this.hasModRM = hasModRM;
			this.immediateSizeInBytes = immediateSizeInBytes;
			this.tag = tag;
		}

		public InstructionDecoderLookupStatus Status => status;
		public bool IsNotFound => status == InstructionDecoderLookupStatus.NotFound;
		public bool IsSuccess => status == InstructionDecoderLookupStatus.Success;
		public bool HasImmediateSize => status >= InstructionDecoderLookupStatus.Ambiguous_RequireImm8;

		public bool HasModRM => IsNotFound ? throw new InvalidOperationException() : hasModRM;

		public int ImmediateSizeInBytes => HasImmediateSize
			? immediateSizeInBytes : throw new InvalidOperationException();

		public object Tag => IsSuccess ? tag : throw new InvalidOperationException();

		public static readonly InstructionDecoderLookupResult NotFound
			= new InstructionDecoderLookupResult(InstructionDecoderLookupStatus.NotFound);

		public static readonly InstructionDecoderLookupResult Ambiguous_RequireModRM
			= new InstructionDecoderLookupResult(InstructionDecoderLookupStatus.Ambiguous_RequireModRM, hasModRM: true);

		public static InstructionDecoderLookupResult Ambiguous_RequireImm8(bool hasModRM)
			=> new InstructionDecoderLookupResult(InstructionDecoderLookupStatus.Ambiguous_RequireImm8, hasModRM, immediateSizeInBytes: 1);

		public static InstructionDecoderLookupResult Success(
			bool hasModRM, int immediateSizeInBytes, object tag = null)
		{
			if (unchecked((uint)immediateSizeInBytes) > ImmediateData.MaxSizeInBytes)
				throw new ArgumentOutOfRangeException(nameof(immediateSizeInBytes));
			return new InstructionDecoderLookupResult(InstructionDecoderLookupStatus.Success,
				hasModRM, (byte)immediateSizeInBytes, tag);
		}
	}
	
	public enum InstructionDecoderLookupStatus : byte
	{
		NotFound,
		Ambiguous_RequireModRM,
		Ambiguous_RequireImm8,
		Success
	}
}
