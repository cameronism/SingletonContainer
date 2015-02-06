using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SimpleContainer.Tests
{
	public class BuilderTests
	{
		interface IDep { }
		class Dep1 : IDep {  }

		[Fact]
		public void Register()
		{
			var builder = new Builder();
			var reg = builder.Register<Dep1>();
			Assert.NotNull(reg);

			builder.Build();

			var container = builder.Container;
			Assert.NotNull(container);

			var instance = container.Resolve<Dep1>();
			Assert.NotNull(instance);
		}

		[Fact]
		public void Singletons()
		{
			var builder = new Builder();
			builder.Register<Dep1>();
			builder.Build();
			var container = builder.Container;

			var instance1 = container.Resolve<Dep1>();
			var instance2 = container.Resolve<Dep1>();
			Assert.Same(instance1, instance2);
		}

		[Fact]
		public void Abstractions()
		{
			var builder = new Builder();
			builder.Register<Dep1>().As<IDep>();
			builder.Build();
			var container = builder.Container;

			var instance1 = container.Resolve<Dep1>();
			var instance2 = container.Resolve<IDep>();
			Assert.Same(instance1, instance2);
		}

		[Fact]
		public void RejectBadAbstractions()
		{
			var builder = new Builder();

			Assert.Throws<RegistrationFailedException>(() =>
			{
				builder.Register<Dep1>().As<IList<int>>();
			});
		}

		[Fact]
		public void RegistrationWithoutSelf()
		{
			var builder = new Builder();
			builder.Register<Dep1>().As<IDep>().WithoutSelf();
			builder.Build();
			var container = builder.Container;

			Assert.NotNull(container.Resolve<IDep>());
			Assert.Throws<ResolutionFailedException>(() =>
			{
				container.Resolve<Dep1>();
			});
		}
	}
}
