using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SingletonContainer
{
	[AttributeUsage(AttributeTargets.Constructor, AllowMultiple=false)]
	public class PreferredConstructorAttribute : Attribute
	{
	}
}
