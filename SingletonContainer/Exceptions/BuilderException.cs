using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SingletonContainer.Exceptions
{
	public abstract class BuilderException : Exception
	{
	}

	public class ContainerNotBuiltException : BuilderException
	{
	}

	public class ContainerAlreadyBuiltException : BuilderException
	{
	}

	public class RegistrationFailedException : BuilderException
	{
	}
}
