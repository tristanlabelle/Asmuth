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

		public static XedField ModField { get; } = CreateField("MOD", XedBitsFieldType._2);
		public static XedField RegField { get; } = CreateField("REG", XedBitsFieldType._3);
		public static XedField RMField { get; } = CreateField("RM", XedBitsFieldType._3);
		public static XedField UImm0Field { get; } = CreateField("UIMM8", XedBitsFieldType.FromSize(8));
		public static XedField Base0Field { get; }
		public static XedField OutRegField { get; }
		public static XedField Prefix66Field { get; } = CreateField("PREFIX66", XedBitsFieldType.Bit);
		public static XedField RexField { get; } = CreateField("REX", XedBitsFieldType.Bit);
		public static XedField RexWField { get; } = CreateField("REXW", XedBitsFieldType.Bit);
		public static XedField RexRField { get; } = CreateField("REXR", XedBitsFieldType.Bit);
		public static XedField RexXField { get; } = CreateField("REXX", XedBitsFieldType.Bit);
		public static XedField RexBField { get; } = CreateField("REXB", XedBitsFieldType.Bit);
		
		static XedTestData()
		{
			RegisterTable = new XedRegisterTable();
			RegisterTable.AddOrUpdate(new XedDataFiles.RegisterEntry(
				"XMM0", "xmm", 128, 128, "XMM0", "XMM0", 0, false));
			RegisterFieldType = new XedRegisterFieldType(RegisterTable);

			Base0Field = CreateField("BASE0", RegisterFieldType);
			OutRegField = CreateField("OUTREG", RegisterFieldType);
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

		private static XedField CreateField(string name, XedFieldType type)
		{
			return new XedField(name, type, XedOperandVisibility.Explicit,
				isPrintable: false, isPublic: false,
				decoderUsage: XedFieldUsage.Input, encoderUsage: XedFieldUsage.Input);
		}
	}
}
