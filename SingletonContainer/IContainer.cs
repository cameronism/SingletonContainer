using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SingletonContainer
{
	public interface IContainer
	{
		T Resolve<T>();

		/// <summary>
		/// Returns all registrants castable to T in instantiation order
		/// </summary>
		/// <remarks>Does not require registration as <typeparam name="T">T</typeparam></remarks>
		IList<T> OfType<T>() where T : class;
	}

}
