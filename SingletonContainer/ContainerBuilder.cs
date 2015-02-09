using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SingletonContainer.Exceptions;

namespace SingletonContainer
{
	public class ContainerBuilder
	{
		#region inner types
		public interface IRegistration
		{
			IRegistration As<T>();

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
			readonly ContainerBuilder _Builder;

			public Registration(ContainerBuilder builder, Type type)
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
						Instance = _Builder.Invoke(ctor.Key, null, Type);
						return default(KeyValuePair<ConstructorInfo, ParameterInfo[]>);
					}
					return ctor;
				}

				// let it throw if ctors.Length < 1

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
			readonly ContainerBuilder _Builder;

			public ContainerImp(ContainerBuilder builder)
			{
				_Builder = builder;
			}

			public T Resolve<T>()
			{
				object instance;
				if (!_Builder._Instances.TryGetValue(typeof(T), out instance) || instance == null)
				{
					throw new ResolutionFailedException();
				}

				return (T)instance;
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
		Dictionary<Type, object> _Instances = new Dictionary<Type, object>();
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

		object Invoke(ConstructorInfo ctor, object[] parameters, Type type)
		{
			try
			{
				return ctor.Invoke(parameters);
			}
			catch (TargetInvocationException e)
			{
				var created = _Unique
					.Select(r => r.Instance)
					.Where(o => o != null)
					.ToList();

				throw new ConstructorFaultedException(created, e.InnerException, type, ctor.GetParameters());
			}
		}

		bool TryCreate(RegistrationConstructor ctor, object[] parameters)
		{
			for (int i = 0; i < parameters.Length; i++)
			{
				Registration reg;
				if (!_Registrations.TryGetValue(ctor.Parameters[i].ParameterType, out reg) || reg.Instance == null) return false;
				parameters[i] = reg.Instance;
			}

			ctor.Registration.Instance = Invoke(ctor.Constructor, parameters, ctor.Registration.Type);
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

			var incomplete = theRest
				.Select(c => new KeyValuePair<Type, ParameterInfo[]>(c.Registration.Type, c.Parameters))
				.ToList();

			var created = _Unique
				.Select(r => r.Instance)
				.Where(o => o != null)
				.ToList();

			return missing.Any() ? (DependencyException)
				new DependencyMissingException(created, missing.ToList(), incomplete) :
				new DependencyCycleException(created, incomplete);
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

				count = theRest.Count;
				node = theRest.First;
			}
		}

		public IContainer Build()
		{
			VerifyNotBuilt();
			var instances = new Dictionary<Type, object>(_Registrations.Count);
			ContainerImp result;

			lock (_Gate)
			{
				// double check
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

				foreach (var kvp in _Registrations)
				{
					instances[kvp.Key] = kvp.Value.Instance;
				}


				result = new ContainerImp(this);
				_Container = result;
				_Instances = instances;
				_Registrations = null;
			}
			return result;
		}
	}
}
