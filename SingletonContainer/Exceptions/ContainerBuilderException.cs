using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SingletonContainer.Exceptions
{
	public class ContainerBuilderException : Exception
	{
		public IReadOnlyList<object> Created { get; private set; }

		public ContainerBuilderException(string message, IReadOnlyList<object> created) : base(message)
		{
			Created = created;
		}
	}
}
