using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SingletonContainer.Exceptions
{
	public abstract class DependencyException : Exception
	{
		public IReadOnlyList<Type> Incomplete { get; private set; }
		protected DependencyException(IReadOnlyList<Type> incomplete)
		{
			Incomplete = incomplete;
		}
	}

	public class DependencyCycleException : DependencyException
	{
		public DependencyCycleException(IReadOnlyList<Type> incomplete) : base(incomplete)
		{
		}
	}

	public class DependencyMissingException : DependencyException
	{
		public IReadOnlyList<Type> Missing { get; private set; }

		public DependencyMissingException(IReadOnlyList<Type> missing, IReadOnlyList<Type> incomplete) : base(incomplete)
		{
			Missing = missing;
		}
	}
}
