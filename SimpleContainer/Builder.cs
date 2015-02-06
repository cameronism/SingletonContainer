using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SimpleContainer
{
	public interface IContainer
	{
		T Resolve<T>();

		/// <summary>
		/// Returns all registrants castable to T
		/// FIXME need to specify or document the order
		/// </summary>
		IList<T> OfType<T>() where T : class;
	}

	public class DependencyCycleException : Exception
	{
		public IReadOnlyList<Type> Cycle { get; private set; }
		public DependencyCycleException(IReadOnlyList<Type> cycle)
		{
			Cycle = cycle;
		}
	}

	public class ContainerNotBuiltException : Exception
	{
	}

	public class ContainerAlreadyBuiltException : Exception
	{
	}

	public class ResolutionFailedException : Exception
	{
	}

	public class RegistrationFailedException : Exception
	{
	}

	public class MissingDependencyException : Exception
	{
		public IReadOnlyList<Type> Missing { get; private set; }

		public MissingDependencyException(IReadOnlyList<Type> missing)
		{
			Missing = missing;
		}
	}

	public class Builder
	{
		#region inner types
		public interface IRegistration
		{
			IRegistration As<T>();

			// TODO look at what autofac calls this
			/// <summary>
			/// Makes current registration only available as type(s) registered with As() calls
			/// </summary>
			/// <returns></returns>
			IRegistration WithoutSelf();
		}

		class Registration : IRegistration
		{
			public object Instance;
			public readonly Type Type;
			readonly Builder _Builder;

			public Registration(Builder builder, Type type)
			{
				this._Builder = builder;
				this.Type = type;
			}

			public IRegistration As<T>()
			{
				if (!typeof(T).IsAssignableFrom(Type))
				{
					throw new RegistrationFailedException();
				}

				lock (_Builder._Gate)
				{
					_Builder.VerifyNotBuilt();
					_Builder._Registrations[typeof(T)] = this;
				}

				return this;
			}

			public IRegistration WithoutSelf()
			{
				lock (_Builder._Gate)
				{
					_Builder.VerifyNotBuilt();
					_Builder._Registrations.Remove(Type);
				}
				return this;
			}

			public KeyValuePair<ConstructorInfo, ParameterInfo[]> Create()
			{
				var ctors = Type.GetConstructors();
				var preferred = ctors.FirstOrDefault(c => c.GetCustomAttributes(typeof(PreferredConstructorAttribute)).Any());
				if (preferred != null)
				{
					ctors = new[] { preferred };
				}

				KeyValuePair<ConstructorInfo, ParameterInfo[]> ctor;
				if (ctors.Length == 1)
				{
					ctor = new KeyValuePair<ConstructorInfo, ParameterInfo[]>(ctors[0], ctors[0].GetParameters());
					if (ctor.Value.Length == 0)
					{
						Instance = ctor.Key.Invoke(null);
						return default(KeyValuePair<ConstructorInfo, ParameterInfo[]>);
					}
					return ctor;
				}

				if (ctors.Length == 0)
				{
					throw new KeyNotFoundException();
				}

				ctor = new KeyValuePair<ConstructorInfo, ParameterInfo[]>(ctors[0], ctors[0].GetParameters());

				for (int i = 1; i < ctors.Length; i++)
				{
					var current = new KeyValuePair<ConstructorInfo, ParameterInfo[]>(ctors[i], ctors[i].GetParameters());
					if (current.Value.Length > ctor.Value.Length)
					{
						ctor = current;
					}
				}

				return ctor;
			}
		}

		class ContainerImp : IContainer
		{
			readonly Builder _Builder;

			public ContainerImp(Builder builder)
			{
				_Builder = builder;
			}

			public T Resolve<T>()
			{
				Registration reg;
				if (!_Builder._Registrations.TryGetValue(typeof(T), out reg))
				{
					throw new ResolutionFailedException();
				}

				return (T)reg.Instance;
			}

			public IList<T> OfType<T>() where T : class
			{
				var matches = new List<T>(_Builder._Unique.Count);
				foreach (var reg in _Builder._Unique)
				{
					var maybe = reg.Instance as T;
					if (maybe != null)
					{
						matches.Add(maybe);
					}
				}
				return matches;
			}
		}

		struct RegistrationConstructor
		{
			public readonly ConstructorInfo Constructor;
			public readonly ParameterInfo[] Parameters;
			public readonly Registration Registration;

			public RegistrationConstructor(ConstructorInfo constructorInfo, ParameterInfo[] parameterInfo, Registration reg)
			{
				Constructor = constructorInfo;
				Parameters = parameterInfo;
				Registration = reg;
			}

			public object[] GetArray(List<object[]> cache)
			{
				var ix = Parameters.Length - 1;
				while (cache.Count <= ix) cache.Add(null);

				var result = cache[ix];
				if (result == null)
				{
					result = new object[Parameters.Length];
					cache[ix] = result;
				}
				return result;
			}
		}
		#endregion

		#region members
		object _Gate = new object();
		Dictionary<Type, Registration> _Registrations = new Dictionary<Type, Registration>();
		LinkedList<Registration> _Unique = new LinkedList<Registration>();
		ContainerImp _Container;
		#endregion

		void VerifyNotBuilt()
		{
			if (_Container != null) throw new ContainerAlreadyBuiltException();
		}

		void VerifyAlreadyBuilt()
		{
			if (_Container == null) throw new ContainerNotBuiltException();
		}

		public IRegistration Register(Type type)
		{
			var reg = new Registration(this, type);
			var node = new LinkedListNode<Registration>(reg);
			lock (_Gate)
			{
				VerifyNotBuilt();
				_Registrations[type] = reg;
				_Unique.AddLast(node);
			}
			return reg;
		}
		public IRegistration Register<T>()
		{
			return Register(typeof(T));
		}
		
		public IContainer Container
		{
			get 
			{
				VerifyAlreadyBuilt();
				return _Container;
			}
		}
		
		public void Dispose()
		{
		}

		bool TryCreate(RegistrationConstructor ctor, object[] parameters)
		{
			for (int i = 0; i < parameters.Length; i++)
			{
				Registration reg;
				if (!_Registrations.TryGetValue(ctor.Parameters[i].ParameterType, out reg) || reg.Instance == null) return false;
				parameters[i] = reg.Instance;
			}

			ctor.Registration.Instance = ctor.Constructor.Invoke(parameters);
			return true;
		}

		Exception GetSpecificError(LinkedList<RegistrationConstructor> theRest)
		{
			var missing = new HashSet<Type>();

			foreach (var ctor in theRest)
			{
				foreach (var param in ctor.Parameters)
				{
					if (!_Registrations.ContainsKey(param.ParameterType))
					{
						missing.Add(param.ParameterType);
					}
				}

			}

			return missing.Any() ? (Exception)
				new MissingDependencyException(missing.ToList()) :
				new DependencyCycleException(theRest.Select(c => c.Registration.Type).ToList());
		}

		void Build(LinkedList<RegistrationConstructor> theRest)
		{
			int count = theRest.Count;
			var node = theRest.First;
			var cache = new List<object[]>(16);

			while (node != null)
			{
				while (node != null)
				{
					var ctor = node.Value;
					if (TryCreate(ctor, ctor.GetArray(cache)))
					{
						_Unique.AddLast(ctor.Registration);

						var toRemove = node;
						node = node.Next;
						theRest.Remove(toRemove);
					}
					else
					{
						node = node.Next;
					}
				}

				if (theRest.Count == count)
				{
					throw GetSpecificError(theRest);
				}

				node = theRest.First;
			}
		}

		public IContainer Build()
		{
			ContainerImp result;
			lock (_Gate)
			{
				VerifyNotBuilt();

				var needMore = new LinkedList<RegistrationConstructor>();

				var node = _Unique.First;
				while (node != null)
				{
					var ctor = node.Value.Create();
					if (ctor.Key == null)
					{
						node = node.Next;
					}
					else
					{
						needMore.AddLast(new RegistrationConstructor(ctor.Key, ctor.Value, node.Value));

						var toRemove = node;
						node = node.Next;
						_Unique.Remove(toRemove);
					}
				}

				if (needMore.Count > 0)
				{
					Build(needMore);
				}

				result = new ContainerImp(this);
				_Container = result;
			}
			return result;
		}
	}
}
