﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Asmuth.X86
{
    partial struct Register
    {
		public string Name
		{
			get
			{
				switch (Family)
				{
					case RegisterFamily.Gpr: return AsGpr().Name;
					case RegisterFamily.X87: return "st" + Index;
					case RegisterFamily.Mmx: return "mm" + Index;

					case RegisterFamily.Sse:
						if (SizeInBytes == 16) return "xmm" + Index;
						if (SizeInBytes == 32) return "ymm" + Index;
						if (SizeInBytes == 64) return "zmm" + Index;
						throw new UnreachableException();

					case RegisterFamily.AvxOpmask: return "k" + Index;

					case RegisterFamily.Segment: return ((SegmentRegister)Index).GetName();
					case RegisterFamily.Debug: return "dr" + Index;
					case RegisterFamily.Control: return "cr" + Index;

					case RegisterFamily.IP:
					case RegisterFamily.Flags:
						string name = Family == RegisterFamily.IP ? "ip" : "flags";
						if (SizeInBytes == 4) name = "e" + name;
						else if (SizeInBytes == 8) name = "r" + name;
						return name;
				}

				throw new NotImplementedException();
			}
		}

		private static readonly Dictionary<string, Register> nameLookup;

		static Register()
		{
			nameLookup = new Dictionary<string, Register>(StringComparer.InvariantCultureIgnoreCase);
			foreach (var field in typeof(Register).GetTypeInfo().DeclaredFields)
			{
				if (!field.IsStatic || field.FieldType != typeof(Register))
					continue;

				// TODO: Pick FlagsUnsized over Flags16 and remove suffix
				nameLookup.Add(field.Name, (Register)field.GetValue(null));
			}
		}

		public static Register? TryFromName(string name)
			=> nameLookup.TryGetValue(name, out Register reg) ? reg : (Register?)null;
	}
}
