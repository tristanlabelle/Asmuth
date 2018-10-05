using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Asmuth.X86.Xed
{
	public static class XedTestData
	{
		public static XedRegisterTable RegisterTable { get; }
		public static XedRegister Xmm0Register { get; }
		public static XedRegisterFieldType RegisterFieldType { get; }

		public static XedField ModField { get; } = new XedField("MOD", XedBitsFieldType._2, XedFieldFlags.None);
		public static XedField RegField { get; } = new XedField("REG", XedBitsFieldType._3, XedFieldFlags.None);
		public static XedField RMField { get; } = new XedField("RM", XedBitsFieldType._3, XedFieldFlags.None);
		public static XedField UImm0Field { get; } = new XedField("UIMM8", XedBitsFieldType.FromSize(8), XedFieldFlags.None);
		public static XedField Base0Field { get; }
		public static XedField OutRegField { get; }
		public static XedField Prefix66Field { get; } = new XedField("PREFIX66", XedBitsFieldType.Bit, XedFieldFlags.None);
		public static XedField RexField { get; } = new XedField("REX", XedBitsFieldType.Bit, XedFieldFlags.None);
		public static XedField RexWField { get; } = new XedField("REXW", XedBitsFieldType.Bit, XedFieldFlags.None);
		public static XedField RexRField { get; } = new XedField("REXR", XedBitsFieldType.Bit, XedFieldFlags.None);
		public static XedField RexXField { get; } = new XedField("REXX", XedBitsFieldType.Bit, XedFieldFlags.None);
		public static XedField RexBField { get; } = new XedField("REXB", XedBitsFieldType.Bit, XedFieldFlags.None);
		
		static XedTestData()
		{
			RegisterTable = new XedRegisterTable();
			RegisterTable.AddOrUpdate(new XedDataFiles.RegisterEntry(
				"XMM0", "xmm", 128, 128, "XMM0", "XMM0", 0, false));
			RegisterFieldType = new XedRegisterFieldType(RegisterTable);

			Base0Field = new XedField("BASE0", RegisterFieldType, XedFieldFlags.None);
			OutRegField = new XedField("OUTREG", RegisterFieldType, XedFieldFlags.None);
		}

		public static XedField ResolveField(string name)
		{
			foreach (var property in typeof(XedTestData).GetTypeInfo().DeclaredProperties)
			{
				if (property.PropertyType != typeof(XedField)) continue;
				var field = (XedField)property.GetValue(null);
				if (field.Name == "name") return field;
			}
			throw new KeyNotFoundException();
		}
	}
}
