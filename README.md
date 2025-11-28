# Mediatoid

[![NuGet](https://img.shields.io/nuget/v/Mediatoid.svg)](https://www.nuget.org/packages/Mediatoid)
[![Downloads](https://img.shields.io/nuget/dt/Mediatoid.svg)](https://www.nuget.org/packages/Mediatoid)
[![NuGet (Core)](https://img.shields.io/nuget/v/Mediatoid.Core.svg)](https://www.nuget.org/packages/Mediatoid.Core)
[![Downloads (Core)](https://img.shields.io/nuget/dt/Mediatoid.Core.svg)](https://www.nuget.org/packages/Mediatoid.Core)
[![NuGet (Behaviors)](https://img.shields.io/nuget/v/Mediatoid.Behaviors.svg)](https://www.nuget.org/packages/Mediatoid.Behaviors)
[![Downloads (Behaviors)](https://img.shields.io/nuget/dt/Mediatoid.Behaviors.svg)](https://www.nuget.org/packages/Mediatoid.Behaviors)
[![NuGet (SourceGen)](https://img.shields.io/nuget/v/Mediatoid.SourceGen.svg)](https://www.nuget.org/packages/Mediatoid.SourceGen)
[![Downloads (SourceGen)](https://img.shields.io/nuget/dt/Mediatoid.SourceGen.svg)](https://www.nuget.org/packages/Mediatoid.SourceGen)

Lightweight, performance-focused and extensible mediator library. Supports CQRS:

- Request/Response (Send)
- Notification (Publish)
- Stream (IAsyncEnumerable)
- Pipeline behaviors (open generic)

Supported TFM: `net8.0`

## Installation

**CLI:**

```bash
dotnet add package Mediatoid
```

**NuGet UI:**
Add the "Mediatoid" package (`Mediatoid.Core` comes transitively).

## Quick Start

**Program.cs (registration and calls):**

```csharp
using Mediatoid;
using Microsoft.Extensions.DependencyInjection;

// Configure services
var services = new ServiceCollection()
    .AddMediatoid(typeof(Program).Assembly) // Scan handlers
    .BuildServiceProvider();

// Resolve ISender
var sender = services.GetRequiredService<ISender>();

// Request/Response: create an order
var id = await sender.Send(new CreateOrder("cust-1", 120m));
Console.WriteLine($"Order ID: {id}");

// Notification: publish order created event
await sender.Publish(new OrderCreated(id));

// Stream: consume a list of numbers as a stream
await foreach (var number in sender.Stream(new ListNumbers(3)))
{
    Console.WriteLine($"Streamed number: {number}");
}
```

**Request/Response (CreateOrder):**

```csharp
public sealed record CreateOrder(string CustomerId, decimal Total) : IRequest<Guid>;

public sealed class CreateOrderHandler : IRequestHandler<CreateOrder, Guid>
{
    public ValueTask<Guid> Handle(CreateOrder request, CancellationToken ct) =>
        ValueTask.FromResult(Guid.NewGuid());
}
```

**Notification (OrderCreated):**

```csharp
public sealed record OrderCreated(Guid OrderId) : INotification;

public sealed class OrderCreatedHandler : INotificationHandler<OrderCreated>
{
    public ValueTask Handle(OrderCreated notification, CancellationToken ct) =>
        ValueTask.CompletedTask;
}
```

**Stream (ListNumbers):**

```csharp
using System.Runtime.CompilerServices;

public sealed record ListNumbers(int Count) : IStreamRequest<int>;

public sealed class ListNumbersHandler : IStreamRequestHandler<ListNumbers, int>
{
    public async IAsyncEnumerable<int> Handle(ListNumbers request, [EnumeratorCancellation] CancellationToken ct)
    {
        for (var i = 1; i <= request.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            yield return i;
            await Task.Yield();
        }
    }
}
```

## Pipeline Behaviors

Define an open-generic behavior; it will be registered automatically via scanning.

```csharp
using Mediatoid;
using Mediatoid.Pipeline;

public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> Handle(
        TRequest request,
        RequestHandlerContinuation<TResponse> continuation,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Before {typeof(TRequest).Name}");
        var res = await continuation();
        Console.WriteLine($"After {typeof(TRequest).Name}");
        return res;
    }
}
```

**Semantics (as of v0.4.0):**

  - **Scope:**
      - `Send`: Request/Response pipeline (`IPipelineBehavior<TRequest,TResponse>`).
      - `Publish`: Notification pipeline (`INotificationBehavior<TNotification>`).
      - `Stream`: Stream pipeline (`IStreamBehavior<TRequest,TItem>`).
  - **Order:** Deterministic; applied outer-to-inner by registration order.
      - Assembly order: parameter order in `AddMediatoid(asm1, asm2, ...)`.
      - Within an assembly: alphabetical `Type.FullName` (ordinal).
  - **Lifetime:** Handlers/behaviors are `Transient`, `ISender` is `Scoped`.
  - Open and closed generics are both supported.
  - Short-circuit is possible (continuation may not be invoked).
  - Cancellation and exceptions flow through without wrapping.

**Details:**

- [Architecture and Roadmap](https://github.com/remziyapar/Mediatoid/blob/main/docs/architecture-and-roadmap.md)
- [Pipeline Semantics](https://github.com/remziyapar/Mediatoid/blob/main/docs/pipeline-semantics.md)

## Optional Packages

### Mediatoid.Behaviors

Provides out-of-the-box Logging and Validation behaviors:

```bash
dotnet add package Mediatoid.Behaviors
dotnet add package FluentValidation
dotnet add package FluentValidation.DependencyInjectionExtensions
```

**Usage:**

```csharp
var services = new ServiceCollection()
    .AddMediatoid(typeof(Program).Assembly)
    .AddValidatorsFromAssembly(typeof(Program).Assembly) // FluentValidation
    .AddMediatoidBehaviors()                          // Logging + Validation
    .BuildServiceProvider();
```

**Example validator:**

```csharp
using FluentValidation;

public sealed class CreateOrderValidator : AbstractValidator<CreateOrder>
{
    public CreateOrderValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.Total).GreaterThan(0);
    }
}
```

> For invalid requests, `ValidationException` (from Mediatoid.Behaviors) is thrown and all errors are aggregated.

### Mediatoid.SourceGen

Moves handler discovery to build time; reduces runtime reflection and `MethodInfo.Invoke` overhead.

**Installation:**

```bash
dotnet add package Mediatoid.SourceGen
```

Mark one or more roots with `MediatoidRootAttribute` so the generator knows where to start scanning for handlers. You can mark an assembly or a specific type:

```csharp
using Mediatoid.Core;

[assembly: MediatoidRoot] // mark this assembly as a Mediatoid root

// or on a type:
[MediatoidRoot]
public sealed class ApplicationRoot { }
```

Once the package is referenced and at least one root is marked, the generator is activated. The pipeline behavior chain is still composed at runtime (deterministic order is preserved).

> **Note (roadmap):**
> In the v0.4.x line, the SourceGen side will continue to evolve. Full pipeline chain
> generation (handler + behavior delegates) and deeper diagnostic integration are planned
> as incremental improvements. Today, SourceGen primarily provides handler terminal
> optimization and basic dispatch speed-ups.

### NativeAOT and Trimming

Mediatoid works fine on .NET 8 in normal JIT deployments. The current
runtime implementation (`Mediator` + reflection-based discovery) uses
APIs like `Assembly.GetTypes()`, `MethodInfo.MakeGenericMethod` and
`Type.MakeGenericType`, which do not yet have a fully annotated
NativeAOT/trimming story.

- **Status in 0.4.0:**
    - NativeAOT and aggressive trimming are **experimental** only.
    - Small demo apps may work, but the library does **not** yet claim
        full AOT safety or zero-warning trimming.
- **Planned for 0.5.0+:**
    - Move more of the discovery/registration logic into SourceGen so the
        runtime reflection path can be minimized or skipped in AOT builds.
    - Introduce configuration/guards so AOT builds can rely primarily on
        generated dispatch rather than runtime `MakeGenericMethod`/
        `MakeGenericType`.

If you want to experiment with NativeAOT today, treat it as
best-effort and always validate your own publish output (warnings,
linker/AOT logs) for your specific app.

## Benchmark

The following numbers are from `Mediatoid.Benchmarks` on a local
machine (Release, .NET 8, BenchmarkDotNet). They are **ballpark
figures**, not a public SLA; always run the benchmarks in your own
environment if you care about exact numbers.

**SourceGen vs runtime compose (Send):**

| Method              | Mean      | Allocated |
|-------------------- |----------:|----------:|
| Send_SourceGen      | ~12.6 µs  |   8.1 KB  |
| Send_RuntimeCompose | ~13.6 µs  |   8.1 KB  |

- SourceGen is roughly **1.07× faster** than the runtime compose
    path for the simple Send scenario measured here.

**Publish fan-out (notification handlers):**

| Handlers | Mean (ns) | Allocated |
|---------:|----------:|----------:|
| 1        |    ~515   |   ~480 B  |
| 2        |    ~574   |   ~512 B  |
| 4        |    ~583   |   ~576 B  |
| 8        |    ~642   |   ~704 B  |
| 16       |    ~710   |   ~960 B  |

- Publish cost grows roughly **linearly** with the number of
    handlers, with small per-handler allocation overhead.

For full details (hardware, .NET version, raw output), see the
generated reports under `BenchmarkDotNet.Artifacts/results` or run:

```powershell
dotnet run -c Release -p .\benchmarks\Mediatoid.Benchmarks\Mediatoid.Benchmarks.csproj
```

## Samples

You can find runnable sample applications under the `samples` folder:

- `samples/BasicUsage` – minimal Send/Publish/Stream usage.
- `samples/BehaviorsDemo` – using Logging and Validation behaviors.
- `samples/SourceGenDemo` – using `Mediatoid.SourceGen` with `[MediatoidRoot]`.

## Design Principles

    - Minimal core contracts
    - Single entry point: `ISender` (Send/Publish/Stream)
    - `ValueTask`-based async flows
    - Deterministic pipeline order
    - No exception wrapping (propagate original exception)

## Roadmap

    - **Mediatoid.Behaviors:** Logging, Validation, Metrics, Retry/Idempotency
    - **Mediatoid.SourceGen:** Pipeline chain generation (full optimization)
    - **Mediatoid.AspNetCore:** Minimal API helpers

## Changelog

See [CHANGELOG.md](https://github.com/remziyapar/Mediatoid/blob/main/CHANGELOG.md)

## License

MIT — [LICENSE](https://github.com/remziyapar/Mediatoid/blob/main/LICENSE)
