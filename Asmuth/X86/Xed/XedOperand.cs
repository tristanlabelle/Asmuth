using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Asmuth.X86.Xed
{
	public enum XedOperandVisibility : byte
	{
		[XedEnumName("EXPL")]
		Explicit, // Represented in assembly and encoding
		[XedEnumName("IMPL")]
		Implicit, // Represented in assembly but not in an encoding field
		[XedEnumName("SUPP")]
		Suppressed, // Not represented in encoding nor assembly
		[XedEnumName("ECOND")]
		ECond // ??
	}

	public enum XedOperandAccessMode : byte
	{
		Never,
		Conditional,
		Always
	}

	public readonly struct XedOperandAccess
	{
		private readonly byte readModeAndWriteMode;

		public XedOperandAccess(XedOperandAccessMode readMode, XedOperandAccessMode writeMode)
			=> this.readModeAndWriteMode = (byte)(((byte)readMode << 4) | (byte)writeMode);

		public XedOperandAccessMode ReadMode => (XedOperandAccessMode)(readModeAndWriteMode >> 4);
		public XedOperandAccessMode WriteMode => (XedOperandAccessMode)(readModeAndWriteMode & 0xF);

		public override string ToString()
		{
			string str = string.Empty;
			if (ReadMode == XedOperandAccessMode.Conditional) str += "c";
			if (ReadMode != XedOperandAccessMode.Never) str += "r";
			if (WriteMode == XedOperandAccessMode.Conditional) str += "c";
			if (WriteMode != XedOperandAccessMode.Never) str += "w";
			return str;
		}

		public static readonly XedOperandAccess None = new XedOperandAccess();
		public static readonly XedOperandAccess Read = new XedOperandAccess(XedOperandAccessMode.Always, XedOperandAccessMode.Never);
		public static readonly XedOperandAccess ReadConditional = new XedOperandAccess(XedOperandAccessMode.Conditional, XedOperandAccessMode.Never);
		public static readonly XedOperandAccess Write = new XedOperandAccess(XedOperandAccessMode.Never, XedOperandAccessMode.Always);
		public static readonly XedOperandAccess WriteConditional = new XedOperandAccess(XedOperandAccessMode.Never, XedOperandAccessMode.Conditional);
		public static readonly XedOperandAccess ReadWrite = new XedOperandAccess(XedOperandAccessMode.Always, XedOperandAccessMode.Always);
		public static readonly XedOperandAccess ReadConditionalWriteAlways = new XedOperandAccess(XedOperandAccessMode.Conditional, XedOperandAccessMode.Always);
		public static readonly XedOperandAccess ReadAlwaysWriteConditional = new XedOperandAccess(XedOperandAccessMode.Always, XedOperandAccessMode.Conditional);
		public static readonly XedOperandAccess ReadWriteConditional = new XedOperandAccess(XedOperandAccessMode.Conditional, XedOperandAccessMode.Conditional);

		public static XedOperandAccess Parse(string str)
		{
			throw new NotImplementedException();
		}
	}

	public abstract class XedOperand
	{
		public abstract XedOperandAccess Access { get; }
		public abstract XedOperandWidth? Width { get; }
		public abstract XedOperandVisibility Visibility { get; }
	}

	// TODO:
	public abstract class XedMemoryOperand : XedOperand {}
	public abstract class XedRegisterOperand : XedOperand { }
	public abstract class XedSuppressedMemoryOperand : XedMemoryOperand { }
	public abstract class XedExplicitMemoryOperand : XedMemoryOperand { }
	public abstract class XedImmediateOperand : XedOperand { } // IMM, PTR and RELBR?
}
