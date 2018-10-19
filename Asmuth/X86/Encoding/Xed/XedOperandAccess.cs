using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Asmuth.X86.Encoding.Xed
{
	public enum XedOperandAccessMode : byte
	{
		Never,
		Conditional,
		Always
	}

	[StructLayout(LayoutKind.Sequential, Size = 1)]
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
			XedOperandAccessMode readMode = XedOperandAccessMode.Never;
			if (str.StartsWith("cr"))
			{
				readMode = XedOperandAccessMode.Conditional;
				str = str.Substring("cr".Length);
			}
			else if (str.StartsWith("r"))
			{
				readMode = XedOperandAccessMode.Always;
				str = str.Substring("r".Length);
			}

			XedOperandAccessMode writeMode;
			if (str.Length == 0) writeMode = XedOperandAccessMode.Never;
			else if (str == "cw") writeMode = XedOperandAccessMode.Conditional;
			else if (str == "w") writeMode = XedOperandAccessMode.Always;
			else throw new FormatException();

			return new XedOperandAccess(readMode, writeMode);
		}
	}
}
