using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw.Nasm
{
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
	internal sealed class NasmNameAttribute : Attribute
	{
		private readonly string name;

		public NasmNameAttribute(string name)
		{
			Contract.Requires(name != null);
			this.name = name;
		}

		public string Name => name;

		internal static Dictionary<string, TEnum> BuildNamesToEnumsDictionary<TEnum>() where TEnum : struct
		{
			var namesToEnums = new Dictionary<string, TEnum>(StringComparer.Ordinal);
			foreach (var field in typeof(TEnum).GetTypeInfo().DeclaredFields)
			{
				var nasmNameAttribute = field.GetCustomAttribute<NasmNameAttribute>();
				if (nasmNameAttribute != null) namesToEnums.Add(nasmNameAttribute.Name, (TEnum)field.GetValue(null));
			}

			return namesToEnums;
		}
	}
}
