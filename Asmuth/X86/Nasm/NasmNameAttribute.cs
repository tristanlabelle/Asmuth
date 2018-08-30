using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Nasm
{
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
	internal sealed class NasmNameAttribute : Attribute
	{
		public string Name { get; }

		public NasmNameAttribute(string name)
		{
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
		}
	}

	internal static class NasmEnum<TEnum> where TEnum : struct
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
			if (name == null) throw new ArgumentNullException(nameof(name));
			
			namesToEnumerants.TryGetValue(name, out TEnum enumerant);
			return enumerant;
		}

		public static TEnum? GetEnumerantOrNull(string name)
		{
			if (name == null) throw new ArgumentNullException(nameof(name));

			return namesToEnumerants.TryGetValue(name, out TEnum enumerant) ? enumerant : (TEnum?)null;
		}

		public static string GetNameOrNull(TEnum enumerant)
		{
			enumerantToNames.TryGetValue(enumerant, out string name);
			return name;
		}
	}
}
