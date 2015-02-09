using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SingletonContainer.Exceptions
{
	public abstract class UsageException : Exception
	{
	}

	public class ContainerNotBuiltException : UsageException
	{
	}

	public class ContainerAlreadyBuiltException : UsageException
	{
	}

	public class RegistrationFailedException : UsageException
	{
	}
}
