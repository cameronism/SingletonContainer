# SingletonContainer
Simple IoC container only supporting singleton lifetimes.

**Basic Usage**
```csharp
var builder = new ContainerBuilder();
builder.Register<Dep1>().As<IDep>();
var container = builder.Build();

var instance1 = container.Resolve<IDep>();
```

**OfType&lt;T&gt;() Method**

`IContainer.OfType<T>()` returns all registrants castable to `T` in instantiation order

```csharp
var container = builder.Build();

foreach (var thing in container.OfType<IThing>())
{
  thing.Stuff();
}
```

---

Reverse the order for a reasonable teardown strategy.

```csharp
foreach (var disposable in container.OfType<IDisposable>().Reverse())
{
  disposable.Dispose();
}
```
