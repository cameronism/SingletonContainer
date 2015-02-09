using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SingletonContainer.Exceptions;
using Xunit;
using Xunit.Extensions;

namespace SingletonContainer.Tests
{
	public class ExceptionTests
	{
		class A { public A(int i) { } }
		class B { public B(int i, A a) { } }
		class C<T> { public C(T t) { } }

		class GoBoom
		{
			public GoBoom(BuilderTests.Dep1 d1)
			{
				throw new DirectoryNotFoundException("boom");
			}
		}

		[Theory]
		[InlineData(typeof(ExceptionTests), "SingletonContainer.Tests.ExceptionTests()")]
		[InlineData(typeof(A), "SingletonContainer.Tests.ExceptionTests.A(System.Int32)")]
		[InlineData(typeof(B), "SingletonContainer.Tests.ExceptionTests.B(System.Int32, SingletonContainer.Tests.ExceptionTests.A)")]
		[InlineData(typeof(C<int>), "SingletonContainer.Tests.ExceptionTests.C<System.Int32>(System.Int32)")]
		[InlineData(typeof(C<B>), "SingletonContainer.Tests.ExceptionTests.C<SingletonContainer.Tests.ExceptionTests.B>(SingletonContainer.Tests.ExceptionTests.B)")]
		[InlineData(typeof(List<C<B>>), "System.Collections.Generic.List<SingletonContainer.Tests.ExceptionTests.C<SingletonContainer.Tests.ExceptionTests.B>>()")]
		[InlineData(typeof(List<>), "System.Collections.Generic.List<T>()")]
		[InlineData(typeof(Dictionary<,>), "System.Collections.Generic.Dictionary<TKey, TValue>()")]
		[InlineData(typeof(C<List<B>>), "SingletonContainer.Tests.ExceptionTests.C<System.Collections.Generic.List<SingletonContainer.Tests.ExceptionTests.B>>(System.Collections.Generic.List<SingletonContainer.Tests.ExceptionTests.B>)")]
		public void Describe(Type type, string expected)
		{
			ParameterInfo[] parameters = null;

			// find simplest constructor
			foreach (var ctor in type.GetConstructors())
			{
				var current = ctor.GetParameters();
				if (parameters == null || current.Length < parameters.Length)
				{
					parameters = current;
				}
			}

			if (parameters == null) throw new Exception("bad test " + type.Name);

			Assert.Equal(expected, DependencyException.DescribeConstructor(type, parameters));
		}

		[Fact]
		public void NiceMissingError()
		{
			var builder = new ContainerBuilder();
			builder.Register<C<A>>();

			DependencyMissingException dme = null;
			Assert.Throws<DependencyMissingException>(() =>
			{
				try
				{
					builder.Build();
				}
				catch (DependencyMissingException e)
				{
					dme = e;
					throw;
				}
			});

			var expected = String.Join(Environment.NewLine, new[] 
			{
				"Missing:",
				"  SingletonContainer.Tests.ExceptionTests.A",
				"Incomplete:",
				"  SingletonContainer.Tests.ExceptionTests.C<SingletonContainer.Tests.ExceptionTests.A>(SingletonContainer.Tests.ExceptionTests.A)",
			});
			Assert.Equal(expected, dme.Message);
		}

		[Fact]
		public void NiceCycleError()
		{
			var builder = new ContainerBuilder();
			builder.Register<BuilderTests.DepCycle2>();
			builder.Register<BuilderTests.DepCycle1>();

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

			var expected = String.Join(Environment.NewLine, new[] 
			{
				"Incomplete:",
				"  SingletonContainer.Tests.BuilderTests.DepCycle1(SingletonContainer.Tests.BuilderTests.DepCycle2)",
				"  SingletonContainer.Tests.BuilderTests.DepCycle2(SingletonContainer.Tests.BuilderTests.DepCycle1)",
			});
			Assert.Equal(expected, dce.Message);
		}

		[Fact]
		public void BuilderExceptionProvidesCreated()
		{
			var builder = new ContainerBuilder();
			builder.Register<BuilderTests.DepK>();
			builder.Register<BuilderTests.DepA>();
			builder.Register<BuilderTests.DepI>();
			builder.Register<BuilderTests.DepG>();
			builder.Register<BuilderTests.DepF>();
			builder.Register<BuilderTests.DepE>();
			builder.Register<BuilderTests.DepD>();
			builder.Register<BuilderTests.DepC>();
			builder.Register<BuilderTests.DepB>();
			builder.Register<BuilderTests.DepH>();
			builder.Register<BuilderTests.Dep1>();


			ContainerBuilderException dme = null;
			Assert.Throws<DependencyMissingException>(() =>
			{
				try
				{
					builder.Build();
				}
				catch (DependencyMissingException e)
				{
					dme = e;
					throw;
				}
			});

			var created = dme.Created;
			Assert.NotNull(created);
			Assert.Equal(10, created.Count);
			Assert.DoesNotContain(null, created);

			var names = created.Select(o => o.GetType().Name).ToArray();
			var sorted = names.OrderBy(n => n).ToArray();

			// alphabetical order is correct instantiation order
			Assert.Equal(sorted, names);
		}

		[Fact]
		public void ConstructorFaultedException()
		{
			var builder = new ContainerBuilder();
			builder.Register<GoBoom>();
			builder.Register<BuilderTests.Dep1>();


			ConstructorFaultedException dme = null;
			Assert.Throws<ConstructorFaultedException>(() =>
			{
				try
				{
					builder.Build();
				}
				catch (ConstructorFaultedException e)
				{
					dme = e;
					throw;
				}
			});


			var created = dme.Created;
			Assert.NotNull(created);
			Assert.Equal(1, created.Count);
			Assert.IsType(typeof(BuilderTests.Dep1), created[0]);
			
			Assert.Equal("SingletonContainer.Tests.ExceptionTests.GoBoom(SingletonContainer.Tests.BuilderTests.Dep1)", dme.Message);

			Assert.NotNull(dme.InnerException);
			Assert.IsType(typeof(DirectoryNotFoundException), dme.InnerException);
		}
	}
}
