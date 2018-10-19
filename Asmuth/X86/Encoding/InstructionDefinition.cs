using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Encoding
{
	public enum OpcodePrefixSupport : byte
	{
		None = 0,
		Repeat = 1 << 0,
		Lock = 1 << 1,
		LockImplicit = 1 << 2,
		XAcquire = 1 << 3,
		XRelease = 1 << 4,
		BranchHint = 1 << 5,
		SegmentOverride = 1 << 6,
	}

	public static class OpcodePrefixSupportEnum
	{
		public static bool HasFlag(this OpcodePrefixSupport support, OpcodePrefixSupport flag)
			=> (support & flag) == flag;

		public static void Validate(this OpcodePrefixSupport support)
		{
			if (support.HasFlag(OpcodePrefixSupport.LockImplicit) && !support.HasFlag(OpcodePrefixSupport.Lock))
				throw new ArgumentException("Implicit LOCK prefix support implies LOCK prefix support.");

			if (support.HasFlag(OpcodePrefixSupport.Repeat) && support.HasFlag(OpcodePrefixSupport.Lock))
				throw new ArgumentException("REP and LOCK prefixes are mutually exclusive.");

			if ((support.HasFlag(OpcodePrefixSupport.XAcquire) || support.HasFlag(OpcodePrefixSupport.XRelease))
				&& !support.HasFlag(OpcodePrefixSupport.Lock))
				throw new ArgumentException("XAcquire/XRelease prefix support imply LOCK prefix support.");

			if (support.HasFlag(OpcodePrefixSupport.BranchHint) && support != OpcodePrefixSupport.BranchHint)
				throw new ArgumentException("Branch hint prefix support cannot be combined with other prefixes.");
		}
	}

	public enum OpcodeEffectiveOperandSize
	{
		Byte,
		Word,
		Dword,
		Qword,
		WordOrDword,
		WordOrDwordOrQword,
		WordOrDwordOrQword_X64DefaultQword
	}

	public readonly struct OperandDefinition
	{
		public OperandSpec Spec { get; }
		public OperandField? Field { get; }
		public AccessType Access { get; }

		public OperandDefinition(OperandSpec spec, OperandField? field, AccessType access)
		{
			if (!spec.DataLocation.IsWritable() && (access & AccessType.Write) == AccessType.Write)
				throw new ArgumentException();
			this.Spec = spec ?? throw new ArgumentNullException(nameof(spec));
			this.Field = field;
			this.Access = access;
		}

		public override string ToString() => Spec.ToString();
	}

	public sealed class InstructionDefinition
	{
		public struct Data
		{
			public string Mnemonic;
			public IReadOnlyList<OperandDefinition> Operands;
			public OpcodeEncoding Encoding;
			public OpcodePrefixSupport PrefixSupport;
			public OpcodeEffectiveOperandSize? EffectiveOperandSize;
			public CpuidFeatureFlags RequiredFeatureFlags;
			public EFlags? AffectedFlags;
		}

		#region Fields
		private readonly Data data;
		#endregion

		#region Constructor
		public InstructionDefinition(in Data data)
		{
			if (data.Mnemonic == null || data.Operands == null)
				throw new ArgumentException("Some data fields are null.", nameof(data));
			this.data = data;
			this.data.Operands = data.Operands.ToArray();

			// Deduce some supported prefixes from the operands
			foreach (var operand in this.data.Operands)
			{
				if (operand.Spec.DataLocation == OperandDataLocation.Register
					|| operand.Spec.DataLocation == OperandDataLocation.RegisterOrMemory)
				{
					this.data.PrefixSupport |= OpcodePrefixSupport.SegmentOverride;
				}
			}

			this.data.PrefixSupport.Validate();
		}
		#endregion

		#region Properties
		public string Mnemonic => data.Mnemonic;
		public IReadOnlyList<OperandDefinition> Operands => data.Operands;
		public OpcodeEncoding Encoding => data.Encoding;
		public OpcodePrefixSupport PrefixSupport => data.PrefixSupport;
		public OpcodeEffectiveOperandSize? EffectiveOperandSize => data.EffectiveOperandSize;
		public CpuidFeatureFlags RequiredFeatureFlags => data.RequiredFeatureFlags;
		public EFlags? AffectedFlags => data.AffectedFlags;
		#endregion

		#region Methods
		public void FormatOperandList(TextWriter textWriter, in Instruction instruction, ulong? ip = null)
		{
			bool firstOperand = true;
			foreach (var operand in Operands)
			{
				textWriter.Write(firstOperand ? ' ' : ',');
				operand.Spec.Format(textWriter, in instruction, operand.Field, ip);
				firstOperand = false;
			}
		}

		public override string ToString()
		{
			var stringBuilder = new StringBuilder(Mnemonic.Length + Operands.Count * 6);

			stringBuilder.Append(Mnemonic);

			bool firstOperand = true;
			foreach (var operand in Operands)
			{
				stringBuilder.Append(firstOperand ? " " : ", ");
				stringBuilder.Append(operand.Spec);
				firstOperand = false;
			}

			stringBuilder.Append(": ");
			stringBuilder.Append(Encoding.ToString());

			return stringBuilder.ToString();
		}
		#endregion
	}
}
