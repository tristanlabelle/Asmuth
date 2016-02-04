using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Nasm
{
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
	internal sealed class NasmNameAttribute : Attribute
	{
		private readonly string name;

		public NasmNameAttribute(string name)
		{
			Contract.Requires(name != null);
			this.name = name;
		}

		public string Name => name;
	}

	internal sealed class NasmEnum<TEnum> where TEnum : struct
	{
		private static readonly Dictionary<string, TEnum> namesToEnumerants;
		private static readonly Dictionary<TEnum, string> enumerantToNames;

		static NasmEnum()
		{
			namesToEnumerants = new Dictionary<string, TEnum>();
			enumerantToNames = new Dictionary<TEnum, string>();
			foreach (var field in typeof(TEnum).GetTypeInfo().DeclaredFields)
			{
				var nasmNameAttribute = field.GetCustomAttribute<NasmNameAttribute>();
				if (nasmNameAttribute != null)
				{
					var enumerant = (TEnum)field.GetValue(null);
					namesToEnumerants.Add(nasmNameAttribute.Name, enumerant);
					enumerantToNames.Add(enumerant, nasmNameAttribute.Name);
				}
			}
		}

		public static TEnum GetEnumerantOrDefault(string name)
		{
			Contract.Requires(name != null);

			TEnum enumerant;
			namesToEnumerants.TryGetValue(name, out enumerant);
			return enumerant;
		}

		public static TEnum? GetEnumerantOrNull(string name)
		{
			Contract.Requires(name != null);

			TEnum enumerant;
			return namesToEnumerants.TryGetValue(name, out enumerant) ? enumerant : (TEnum?)null;
		}

		public static string GetNameOrNull(TEnum enumerant)
		{
			string name;
			enumerantToNames.TryGetValue(enumerant, out name);
			return name;
		}
	}
}
