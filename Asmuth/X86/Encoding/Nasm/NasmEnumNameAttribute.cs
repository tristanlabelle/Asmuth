using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Encoding.Nasm
{
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
	internal sealed class NasmEnumNameAttribute : AlternateEnumNameAttribute
	{
		public NasmEnumNameAttribute(string name) : base(name) { }

		public static TEnum? GetEnumerantOrNull<TEnum>(string name) where TEnum : struct
			=> AlternateEnumNames<TEnum, NasmEnumNameAttribute>.GetEnumerantOrNull(name);

		public static string GetNameOrNull<TEnum>(TEnum enumerant) where TEnum : struct
			=> AlternateEnumNames<TEnum, NasmEnumNameAttribute>.GetNameOrNull(enumerant);
	}
}
