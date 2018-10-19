using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Asmuth.X86.Encoding.Xed
{
	public sealed class XedRegister
	{
		public XedRegisterTable Table { get; }

		// name class width max-enclosing-reg-64b/32b-mode regid [h]
		public string Name { get; }
		public string Class { get; }
		private object maxEnclosingRegOrName_IA32; // String lazy-resolved to XedRegister
		private object maxEnclosingRegOrName_X64; // String lazy-resolved to XedRegister
		private readonly ushort indexInTable;
		private ushort widthInBits_IA32;
		private ushort widthInBits_X64;
		private readonly byte indexInClassPlusOne;
		public bool IsHighByte { get; }

		internal XedRegister(XedRegisterTable table, in XedDataFiles.RegisterEntry dataFileEntry, int indexInTable)
		{
			this.Table = table;
			this.Name = dataFileEntry.Name;
			this.Class = dataFileEntry.Class;
			this.maxEnclosingRegOrName_IA32 = dataFileEntry.MaxEnclosingRegName_IA32;
			this.maxEnclosingRegOrName_X64 = dataFileEntry.MaxEnclosingRegName_X64;
			this.indexInTable = checked((ushort)indexInTable);
			this.widthInBits_IA32 = (ushort)dataFileEntry.WidthInBits_IA32;
			this.widthInBits_X64 = (ushort)dataFileEntry.WidthInBits_X64;
			this.indexInClassPlusOne = dataFileEntry.ID.HasValue ? (byte)(dataFileEntry.ID.Value + 1) : (byte)0;
			this.IsHighByte = dataFileEntry.IsHighByte;
		}
		
		public string MaxEnclosingRegName_IA32 => GetMaxEnclosingRegName(maxEnclosingRegOrName_IA32);
		public string MaxEnclosingRegName_X64 => GetMaxEnclosingRegName(maxEnclosingRegOrName_X64);
		public XedRegister MaxEnclosingReg_IA32 => ResolveMaxEnclosingReg(ref maxEnclosingRegOrName_IA32);
		public XedRegister MaxEnclosingReg_X64 => ResolveMaxEnclosingReg(ref maxEnclosingRegOrName_X64);
		public int WidthInBits_IA32 => widthInBits_IA32;
		public int WidthInBits_X64 => widthInBits_X64;
		public int? IndexInClass => indexInClassPlusOne == 0 ? null : (int?)(indexInClassPlusOne - 1);
		public int IndexInTable => indexInTable;

		public override string ToString() => Name;

		internal void UpdateMaxEnclosingRegs(string ia32, string x64)
		{
			maxEnclosingRegOrName_IA32 = ia32;
			maxEnclosingRegOrName_X64 = x64;
		}
		
		internal void UpdateWidthInBits(int ia32, int x64)
		{
			widthInBits_IA32 = checked((ushort)ia32);
			widthInBits_X64 = checked((ushort)x64);
		}

		private XedRegister ResolveMaxEnclosingReg(ref object value)
		{
			var reg = value as XedRegister;
			if (reg == null)
			{
				if (string.Equals((string)value, Name, StringComparison.OrdinalIgnoreCase))
					reg = this;
				else if (!Table.ByName.TryGetValue((string)value, out reg))
					throw new KeyNotFoundException();
				value = reg;
			}
			return reg;
		}

		private static string GetMaxEnclosingRegName(object value)
			=> value as string ?? ((XedRegister)value).Name;
	}

	[DebuggerDisplay("Count={Count}")]
	public sealed class XedRegisterTable
	{
		private readonly List<XedRegister> byIndex = new List<XedRegister>();
		private readonly Dictionary<string, XedRegister> byName
			= new Dictionary<string, XedRegister>(StringComparer.OrdinalIgnoreCase);

		public IReadOnlyList<XedRegister> ByIndex => byIndex;
		public IReadOnlyDictionary<string, XedRegister> ByName => byName;
		public int Count => byIndex.Count;

		public XedRegister AddOrUpdate(in XedDataFiles.RegisterEntry dataFileEntry)
		{
			if (byName.TryGetValue(dataFileEntry.Name, out var reg))
			{
				// Update the existing entry. This handles the case of:
				// XMM0  xmm  128 YMM0  0
				// ...
				// XMM0  xmm  128 ZMM0  0
				if (dataFileEntry.Class != reg.Class
					|| dataFileEntry.WidthInBits_IA32 < reg.WidthInBits_IA32
					|| dataFileEntry.WidthInBits_X64 < reg.WidthInBits_X64
					|| dataFileEntry.ID != reg.IndexInClass
					|| dataFileEntry.IsHighByte != reg.IsHighByte)
					throw new InvalidOperationException();

				reg.UpdateWidthInBits(
					dataFileEntry.WidthInBits_IA32,
					dataFileEntry.WidthInBits_X64);
				reg.UpdateMaxEnclosingRegs(
					dataFileEntry.MaxEnclosingRegName_IA32,
					dataFileEntry.MaxEnclosingRegName_X64);
			}
			else
			{
				// New entry
				reg = new XedRegister(this, in dataFileEntry, byIndex.Count);
				byIndex.Add(reg);
				byName.Add(dataFileEntry.Name, reg);
			}

			return reg;
		}
	}
}
