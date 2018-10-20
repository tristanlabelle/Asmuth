using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace Asmuth.X86.Encoding
{
	public enum OperandDataLocation : byte
	{
		Constant,
		Register,
		Memory,
		RegisterOrMemory,
		InstructionStream,
	}

	public static class OperandDataLocationEnum
	{
		public static bool IsWritable(this OperandDataLocation location)
			=> location != OperandDataLocation.Constant && location != OperandDataLocation.InstructionStream;

		public static bool? GetIsRegister(this OperandDataLocation location)
		{
			if (location == OperandDataLocation.Register) return true;
			if (location == OperandDataLocation.RegisterOrMemory) return null;
			return false;
		}

		public static bool? GetIsMemory(this OperandDataLocation location)
		{
			if (location == OperandDataLocation.Memory) return true;
			if (location == OperandDataLocation.RegisterOrMemory) return null;
			return false;
		}
	}

	public abstract partial class OperandSpec
	{
		private OperandSpec() { } // Disallow external inheritance

		// Used for NASM's "size match"
		public abstract IntegerSize? ImpliedIntegerOperandSize { get; }
		public abstract OperandDataLocation DataLocation { get; }

		public abstract bool IsValidField(OperandField field);

		public virtual DataType? TryGetDataType(AddressSize addressSize, IntegerSize operandSize) => null;

		public abstract void Format(TextWriter textWriter, in Instruction instruction, OperandField? field, ulong? ip = null);

		public string Format(in Instruction instruction, OperandField? field, ulong? ip = null)
		{
			var stringWriter = new StringWriter();
			Format(stringWriter, in instruction, field, ip);
			return stringWriter.ToString();
		}

		public abstract override string ToString();

		public interface IWithReg
		{
			RegisterClass RegisterClass { get; }
		}
	}
}
