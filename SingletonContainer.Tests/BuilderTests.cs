using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SingletonContainer.Exceptions;
using Xunit;

namespace SingletonContainer.Tests
{
	public class BuilderTests
	{
		interface IDep { }
		internal class Dep1 : IDep {  }
		internal class Dep2 : IDep {  }

		internal class Dep3 { public Dep3(Dep1 d1) { } }

		internal class DepCycle1 { public DepCycle1(DepCycle2 d2) { } }
		internal class DepCycle2 { public DepCycle2(DepCycle1 d1) { } }

		internal class DepA { public DepA(Dep1 d) { } }
		internal class DepB { public DepB(DepA d) { } }
		internal class DepC { public DepC(DepB d) { } }
		internal class DepD { public DepD(DepC d) { } }
		internal class DepE { public DepE(DepD d) { } }
		internal class DepF { public DepF(DepE d) { } }
		internal class DepG { public DepG(DepF d) { } }
		internal class DepH { public DepH(DepG d) { } }
		internal class DepI { public DepI(DepH d) { } }
		internal class DepJ { public DepJ(DepI d) { } }
		internal class DepK { public DepK(DepJ d) { } }

		class DepCycle3 { public DepCycle3(DepCycle4 d, Dep1 d1) { } }
		class DepCycle4 { public DepCycle4(DepCycle3 d) { } }
		class DepMultipleCtor1
		{
			[PreferredConstructor]
			public DepMultipleCtor1(Dep1 d1) { }
			public DepMultipleCtor1(Dep1 d, Dep2 d2) { }
		}
		class DepMultipleCtor2
		{
			public DepMultipleCtor2(Dep1 d1)
			{
				throw new Exception("Wrong constructor selected");
			}
			public DepMultipleCtor2(Dep1 d, Dep2 d2) { }
		}

		[Fact]
		public void Register()
		{
			var builder = new ContainerBuilder();
			var reg = builder.Register<Dep1>();
			Assert.NotNull(reg);

			var container = builder.Build();

			Assert.NotNull(container);

			var instance = container.Resolve<Dep1>();
			Assert.NotNull(instance);
		}

		[Fact]
		public void Singletons()
		{
			var builder = new ContainerBuilder();
			builder.Register<Dep1>();
			var container = builder.Build();

			var instance1 = container.Resolve<Dep1>();
			var instance2 = container.Resolve<Dep1>();
			Assert.Same(instance1, instance2);
		}

		[Fact]
		public void Abstractions()
		{
			var builder = new ContainerBuilder();
			builder.Register<Dep1>().As<IDep>();
			var container = builder.Build();

			var instance1 = container.Resolve<Dep1>();
			var instance2 = container.Resolve<IDep>();
			Assert.Same(instance1, instance2);
		}

		[Fact]
		public void RejectBadAbstractions()
		{
			var builder = new ContainerBuilder();

			Assert.Throws<RegistrationFailedException>(() =>
			{
				builder.Register<Dep1>().As<IList<int>>();
			});
		}

		[Fact]
		public void RegistrationWithoutSelf()
		{
			var builder = new ContainerBuilder();
			builder.Register<Dep1>().As<IDep>().WithoutSelf();
			var container = builder.Build();

			Assert.NotNull(container.Resolve<IDep>());
			Assert.Throws<ResolutionFailedException>(() =>
			{
				container.Resolve<Dep1>();
			});
		}

		[Fact]
		public void OfType()
		{
			var builder = new ContainerBuilder();
			builder.Register<Dep1>().As<IDep>();
			builder.Register<Dep2>();
			var container = builder.Build();

			var ideps = container.OfType<IDep>();

			Assert.Equal(2, ideps.Count);
			Assert.DoesNotContain(null, ideps);
			Assert.NotSame(ideps[0], ideps[1]);

			Assert.Equal(0, container.OfType<IEnumerable<int>>().Count);
		}

		[Fact]
		public void Dependencies()
		{
			var builder = new ContainerBuilder();
			builder.Register<Dep3>();
			builder.Register<Dep1>();
			var container = builder.Build();

			var d1 = container.Resolve<Dep1>();
			var d3 = container.Resolve<Dep3>();
		}

		[Fact]
		public void DependencyCycle()
		{
			var builder = new ContainerBuilder();
			builder.Register<DepCycle1>();
			builder.Register<DepCycle2>();

			Assert.Throws<DependencyCycleException>(() => builder.Build());
		}

		[Fact]
		public void DependencyChain()
		{
			var builder = new ContainerBuilder();
			builder.Register<DepK>();
			builder.Register<DepA>();
			builder.Register<DepJ>();
			builder.Register<DepI>();
			builder.Register<DepG>();
			builder.Register<DepF>();
			builder.Register<DepE>();
			builder.Register<DepD>();
			builder.Register<DepC>();
			builder.Register<DepB>();
			builder.Register<DepH>();
			builder.Register<Dep1>();

			var container = builder.Build();
			var objects = container.OfType<object>();

			Assert.Equal(12, objects.Count);
			Assert.DoesNotContain(null, objects);
		}

		[Fact]
		public void OrderOfType()
		{
			var builder = new ContainerBuilder();
			builder.Register<DepK>();
			builder.Register<DepA>();
			builder.Register<DepJ>();
			builder.Register<DepI>();
			builder.Register<DepG>();
			builder.Register<DepF>();
			builder.Register<DepE>();
			builder.Register<DepD>();
			builder.Register<DepC>();
			builder.Register<DepB>();
			builder.Register<DepH>();
			builder.Register<Dep1>();

			var container = builder.Build();
			var objects = container.OfType<object>();
			var names = objects.Select(o => o.GetType().Name).ToList();

			Assert.Equal(
				names.OrderBy(n => n),
				names);
		}

		[Fact]
		public void MissingDependency()
		{
			var builder = new ContainerBuilder();
			builder.Register<DepB>();
			builder.Register<Dep1>();

			DependencyMissingException mde = null;

			Assert.Throws<DependencyMissingException>(() =>
			{
				try
				{
					builder.Build();
				}
				catch (DependencyMissingException e)
				{
					mde = e;
					throw;
				}
			});

			Assert.Equal(new[] { typeof(DepA) }, mde.Missing, EqualityComparer<Type>.Default);
		}

		[Fact]
		public void CycleExceptionDetails()
		{
			var builder = new ContainerBuilder();
			builder.Register<DepCycle4>();
			builder.Register<DepCycle3>();
			builder.Register<Dep1>();

			DependencyCycleException dce = null;

			Assert.Throws<DependencyCycleException>(() =>
			{
				try
				{
					builder.Build();
				}
				catch (DependencyCycleException e)
				{
					dce = e;
					throw;
				}
			});

			Assert.Equal(
				new[] { typeof(DepCycle3), typeof(DepCycle4) },
				dce.Incomplete.OrderBy(t => t.Name));
		}

		[Fact]
		public void PreferredConstructor()
		{
			var builder = new ContainerBuilder();
			builder.Register<DepMultipleCtor1>();
			builder.Register<Dep1>();

			Assert.DoesNotThrow(() => builder.Build());
		}

		[Fact]
		public void MostParamsConstructor()
		{
			var builder = new ContainerBuilder();
			builder.Register<DepMultipleCtor2>();
			builder.Register<Dep1>();
			builder.Register<Dep2>();

			Assert.DoesNotThrow(() => builder.Build());
		}

		[Fact]
		public void BuilderReturnsContainer()
		{
			var builder = new ContainerBuilder();
			builder.Register<DepMultipleCtor2>();
			builder.Register<Dep1>();
			builder.Register<Dep2>();

			Assert.Same(builder.Build(), builder.Container);
		}

		[Fact]
		public void BuildCanBeCalledOnce()
		{
			var builder = new ContainerBuilder();
			builder.Register<DepMultipleCtor2>();
			builder.Register<Dep1>();
			builder.Register<Dep2>();

			Assert.Same(builder.Build(), builder.Container);

			Assert.Throws<ContainerAlreadyBuiltException>(() => builder.Build());
		}

		[Fact]
		public void BuildMustBeCalledBeforeContainer()
		{
			var builder = new ContainerBuilder();
			builder.Register<DepMultipleCtor2>();
			builder.Register<Dep1>();
			builder.Register<Dep2>();

			Assert.Throws<ContainerNotBuiltException>(() => builder.Container);

			Assert.Same(builder.Build(), builder.Container);

		}
	}
}
