using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth
{
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
	internal abstract class AlternateEnumNameAttribute : Attribute
	{
		public string Name { get; }

		public AlternateEnumNameAttribute(string name)
		{
			this.Name = name ?? throw new ArgumentNullException(nameof(name));
		}
	}

	internal static class AlternateEnumNames<TEnum, TAttribute>
		where TEnum : struct
		where TAttribute : AlternateEnumNameAttribute
	{
		private static readonly Dictionary<string, TEnum> namesToEnumerants;
		private static readonly Dictionary<TEnum, string> enumerantToNames;

		static AlternateEnumNames()
		{
			namesToEnumerants = new Dictionary<string, TEnum>();
			enumerantToNames = new Dictionary<TEnum, string>();
			foreach (var field in typeof(TEnum).GetTypeInfo().DeclaredFields)
			{
				var nasmNameAttribute = field.GetCustomAttribute<TAttribute>();
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

			return namesToEnumerants.TryGetValue(name, out TEnum enumerant)
				? enumerant : (TEnum?)null;
		}

		public static string GetNameOrNull(TEnum enumerant)
		{
			enumerantToNames.TryGetValue(enumerant, out string name);
			return name;
		}
	}
}
