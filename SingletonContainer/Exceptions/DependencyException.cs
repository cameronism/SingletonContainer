using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SingletonContainer.Exceptions
{
	public abstract class DependencyException : ContainerBuilderException
	{
		public IReadOnlyList<Type> Incomplete { get; private set; }
		readonly IReadOnlyList<KeyValuePair<Type, ParameterInfo[]>> _Params;

		protected DependencyException(IReadOnlyList<object> created, IReadOnlyList<KeyValuePair<Type, ParameterInfo[]>> incomplete, string message = null) : base(GetMessage(incomplete, message), created)
		{
			var theList = new Type[incomplete.Count];
			for (int i = 0; i < incomplete.Count; i++)
			{
				theList[i] = incomplete[i].Key;
			}
			Incomplete = theList;
			_Params = incomplete;
		}

		static string GetMessage(IReadOnlyList<KeyValuePair<Type, ParameterInfo[]>> incomplete, string message)
		{
			var lines = new List<string>(incomplete.Count + 2) { null };

			bool haveMessage = !String.IsNullOrEmpty(message);
			if (haveMessage) lines.Add(null);

			lines.AddRange(incomplete.Select(kvp => "  " + DescribeConstructor(kvp.Key, kvp.Value)));
			lines.Sort(StringComparer.Ordinal);
			lines[haveMessage ? 1 : 0] = "Incomplete:";
			if (haveMessage)
			{
				lines[0] = message;
			}
			return String.Join(Environment.NewLine, lines);
		}

		protected static string GetTypeName(Type type)
		{
			var name = type.FullName;
			if (name == null) return type.Name;

			var ix = name.IndexOf('`');
			if (ix != -1)
			{
				name = 
					name.Substring(0, ix) + 
					"<" +
					String.Join(", ", type.GetGenericArguments().Select(GetTypeName)) +
					">";
			}

			return name.Replace('+', '.');
		}

		internal static string DescribeConstructor(Type type, ParameterInfo[] parameters)
		{
			return
				GetTypeName(type) +
				"(" +
				String.Join(", ", parameters.Select(pi => GetTypeName(pi.ParameterType))) +
				")";
		}
	}

	public class DependencyCycleException : DependencyException
	{
		public DependencyCycleException(IReadOnlyList<object> created, IReadOnlyList<KeyValuePair<Type, ParameterInfo[]>> incomplete) : base(created, incomplete)
		{
		}
	}

	public class DependencyMissingException : DependencyException
	{
		public IReadOnlyList<Type> Missing { get; private set; }

		public DependencyMissingException(IReadOnlyList<object> created, IReadOnlyList<Type> missing, IReadOnlyList<KeyValuePair<Type, ParameterInfo[]>> incomplete) : base(created, incomplete, GetMessage(missing))
		{
			Missing = missing;
		}

		static string GetMessage(IReadOnlyList<Type> missing)
		{
			var lines = new List<string>(missing.Count + 1) { null };

			lines.AddRange(missing.Select(t => "  " + GetTypeName(t)));
			lines.Sort(StringComparer.Ordinal);
			lines[0] = "Missing:";
			return String.Join(Environment.NewLine, lines);
		}
	}
}
