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

**Semantics (as of v0.4.0-preview.2):**

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

> **Note (preview):**
> In 0.4.0-preview.* releases, the SourceGen side is still evolving. Full pipeline chain
> generation (handler + behavior delegates) and diagnostic integration are planned to be
> added gradually in the v0.4.x series. For now, SourceGen primarily provides handler
> terminal optimization and basic dispatch speed-ups.

## Benchmark

```

BenchmarkDotNet v0.13.12, Windows 11 (10.0.26200.7171)
12th Gen Intel Core i7-1255U, 1 CPU, 12 logical and 10 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 8.0.22 (8.0.2225.52707), X64 RyuJIT AVX2
  Job-BDRULP : .NET 8.0.22 (8.0.2225.52707), X64 RyuJIT AVX2

IterationCount=15  WarmupCount=3  

```
| Method              | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------- |-----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| Send_Baseline       |   419.3 ns |   7.64 ns |   6.77 ns |  1.00 |    0.00 | 0.0992 |     624 B |        1.00 |
| Send_WithLogging    |   717.5 ns |   6.95 ns |   6.50 ns |  1.71 |    0.04 | 0.1650 |    1040 B |        1.67 |
| Send_WithValidation |   607.4 ns |   6.92 ns |   6.13 ns |  1.45 |    0.02 | 0.1612 |    1016 B |        1.63 |
| Publish_TwoHandlers |   314.8 ns |  25.97 ns |  21.69 ns |  0.75 |    0.05 | 0.0548 |     344 B |        0.55 |
| Stream_First10      | 9,763.4 ns | 478.12 ns | 447.24 ns | 23.38 |    1.09 | 0.0916 |     651 B |        1.04 |


Notes:
- Send baseline is ~420 ns; adding Logging costs ~+70% time / ~+67% memory (extra Stopwatch + ILogger formatting).
- Adding Validation costs ~+45% time / ~+63% memory (running validators + result objects).
- Publish with two handlers is ~315 ns; fan-out grows linearly with the number of handlers.
- Stream first 10 items is ~9.7 µs (enumeration + async yield overhead).

Note: In SourceGen v0.3.*, only the handler terminal is optimized; once full pipeline-chain
generation lands, baseline call times can be reduced further.

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
