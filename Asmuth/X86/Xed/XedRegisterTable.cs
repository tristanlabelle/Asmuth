using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Asmuth.X86.Xed
{
	public sealed class XedRegister
	{
		// name class width max-enclosing-reg-64b/32b-mode regid [h]
		public string Name { get; }
		public string Class { get; }
		public XedRegister MaxEnclosingReg_IA32 { get; internal set; } // Never null
		public XedRegister MaxEnclosingReg_LongMode { get; internal set; } // Never null
		private readonly ushort widthInBits_IA32;
		private readonly ushort widthInBits_LongMode;
		private readonly byte idPlusOne;
		public bool IsHighByte { get; }

		internal XedRegister(in XedDataFiles.RegisterEntry dataFileEntry,
			XedRegister maxEnclosingReg_IA32, XedRegister maxEnclosingReg_LongMode)
		{
			this.Name = dataFileEntry.Name;
			this.Class = dataFileEntry.Class;
			this.MaxEnclosingReg_IA32 = maxEnclosingReg_IA32 ?? this;
			this.MaxEnclosingReg_LongMode = maxEnclosingReg_LongMode ?? this;
			this.widthInBits_IA32 = (ushort)dataFileEntry.WidthInBits_IA32;
			this.widthInBits_LongMode = (ushort)dataFileEntry.WidthInBits_LongMode;
			this.idPlusOne = dataFileEntry.ID.HasValue ? (byte)(dataFileEntry.ID.Value + 1) : (byte)0;
			this.IsHighByte = dataFileEntry.IsHighByte;
		}
	}

	public sealed class XedRegisterTable
	{
		private readonly List<XedRegister> registers = new List<XedRegister>();
		private readonly Dictionary<string, int> nameToIndices
			= new Dictionary<string, int>();

		public IReadOnlyList<XedRegister> Registers => registers;

		public int FindIndex(string name)
		{
			return nameToIndices.TryGetValue(name, out int index) ? index : -1;
		}

		public XedRegister Find(string name)
		{
			return nameToIndices.TryGetValue(name, out int index)
				? registers[index] : null;
		}

		private void AddRegister(XedRegister register)
		{
			nameToIndices.Add(register.Name, registers.Count);
			registers.Add(register);
		}

		public static XedRegisterTable FromDataFileEntries(
			IEnumerable<XedDataFiles.RegisterEntry> entries)
		{
			var maxEnclosingRegNames = new List<(string, string)>();
			var table = new XedRegisterTable();
			foreach (var entry in entries)
			{
				table.AddRegister(new XedRegister(entry, null, null));
				maxEnclosingRegNames.Add((entry.MaxEnclosingRegName_IA32, entry.MaxEnclosingRegName_LongMode));
			}

			for (int i = 0; i < maxEnclosingRegNames.Count; ++i)
			{
				var register = table.registers[i];
				register.MaxEnclosingReg_IA32 = table.Find(maxEnclosingRegNames[i].Item1);
				register.MaxEnclosingReg_LongMode = table.Find(maxEnclosingRegNames[i].Item2);
				if (register.MaxEnclosingReg_IA32 == null || register.MaxEnclosingReg_LongMode == null)
					throw new ArgumentException($"Unresolved max enclosing registers: {maxEnclosingRegNames[i]}.");
			}

			return table;
		}
	}
}
