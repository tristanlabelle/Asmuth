using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Xed
{
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
	internal sealed class XedEnumNameAttribute : AlternateEnumNameAttribute
	{
		public XedEnumNameAttribute(string name) : base(name) { }

		public static TEnum? GetEnumerantOrNull<TEnum>(string name) where TEnum : struct
			=> AlternateEnumNames<TEnum, XedEnumNameAttribute>.GetEnumerantOrNull(name);

		public static string GetNameOrNull<TEnum>(TEnum enumerant) where TEnum : struct
			=> AlternateEnumNames<TEnum, XedEnumNameAttribute>.GetNameOrNull(enumerant);
	}
}
