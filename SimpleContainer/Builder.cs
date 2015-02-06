using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleContainer
{
	public interface IContainer
	{
		T Resolve<T>();

		/// <summary>
		/// Returns all registrants castable to T
		/// </summary>
		IList<T> OfType<T>();
	}

	public class DependencyCycleException : Exception
	{

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
			readonly Builder _Builder;
			readonly Type _Type;

			public Registration(Builder builder, Type type)
			{
				this._Builder = builder;
				this._Type = type;
			}

			public IRegistration As<T>()
			{
				if (!typeof(T).IsAssignableFrom(_Type))
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
					_Builder._Registrations.Remove(_Type);
				}
				return this;
			}

			public void Create()
			{
				if (Instance == null) Instance = Activator.CreateInstance(_Type);
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
				_Builder.VerifyAlreadyBuilt();

				Registration reg;
				lock (_Builder._Gate)
				{
					_Builder._Registrations.TryGetValue(typeof(T), out reg);
				}

				if (reg == null) throw new ResolutionFailedException();

				return (T)reg.Instance;
			}


			public IList<T> OfType<T>()
			{
				_Builder.VerifyAlreadyBuilt();

				var items = new Dictionary<Registration, T>(_Builder._Registrations.Count);

			}
		}
		#endregion

		#region members
		object _Gate = new object();
		Dictionary<Type, Registration> _Registrations = new Dictionary<Type, Registration>();
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

		public IRegistration Register<T>()
		{
			var reg = new Registration(this, typeof(T));
			lock (_Gate)
			{
				VerifyNotBuilt();
				_Registrations[typeof(T)] = reg;
			}
			return reg;
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

		public void Build()
		{
			lock (_Gate)
			{
				if (_Container != null)
				{
					throw new ContainerAlreadyBuiltException();
				}

				_Container = new ContainerImp(this);

				foreach (var reg in _Registrations.Values)
				{
					reg.Create();
				}
			}
		}
	}
}
