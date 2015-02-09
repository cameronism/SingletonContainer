using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SingletonContainer.Exceptions
{
	public abstract class ContainerBuilderException : Exception
	{
		public IReadOnlyList<object> Created { get; private set; }

		protected ContainerBuilderException(string message, IReadOnlyList<object> created, Exception innerException = null) : base(message, innerException)
		{
			Created = created;
		}
	}

	public class ConstructorFaultedException : ContainerBuilderException
	{
		public ConstructorFaultedException(IReadOnlyList<object> created, Exception failure, Type attempted, ParameterInfo[] attemtedParams)
			: base(DependencyException.DescribeConstructor(attempted, attemtedParams), created, failure)
		{
		}
	}
}
