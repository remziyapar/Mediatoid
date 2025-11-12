# Mediatoid

Hafif, performans odaklı, extensible bir Mediator kütüphanesi. CQRS için:
- Request/Response (Send)
- Notification (Publish)
- Stream (IAsyncEnumerable)
- Pipeline davranışları (open generic)

Desteklenen TFM: `net8.0`

## Kurulum

CLI:

```bash
dotnet add package Mediatoid
```

NuGet UI: “Mediatoid” paketini ekleyin (Mediatoid.Core transitif gelir).

## Hızlı Başlangıç

Program.cs (kurulum ve çağrılar):
```csharp
using Mediatoid;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection()
    .AddMediatoid(typeof(Program).Assembly) // handler'ları tara
    .BuildServiceProvider();

var sender = services.GetRequiredService<ISender>();

// Request/Response
var id = await sender.Send(new CreateOrder("cust-1", 120m));
Console.WriteLine(id);

// Notification
await sender.Publish(new OrderCreated(id));

// Stream
await foreach (var n in sender.Stream(new ListNumbers(3)))
    Console.WriteLine(n);
```
Request/Response (CreateOrder):
```csharp
public sealed record CreateOrder(string CustomerId, decimal Total) : IRequest<Guid>;

public sealed class CreateOrderHandler : IRequestHandler<CreateOrder, Guid>
{
    public ValueTask<Guid> Handle(CreateOrder request, CancellationToken ct) =>
        ValueTask.FromResult(Guid.NewGuid());
}
```
Notification (OrderCreated):
```csharp
public sealed record OrderCreated(Guid OrderId) : INotification;

public sealed class OrderCreatedHandler : INotificationHandler<OrderCreated>
{
    public ValueTask Handle(OrderCreated notification, CancellationToken ct) =>
        ValueTask.CompletedTask;
}
```
Stream (ListNumbers):
```csharp
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

## Pipeline Davranışları

Open-generic behavior tanımlayın; tarama ile otomatik kaydedilir.

```csharp
using Mediatoid;
using Mediatoid.Pipeline;

public sealed class LoggingBehavior<TRequest,TResponse> : IPipelineBehavior<TRequest,TResponse>
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


## Tasarım İlkeleri
- Minimal Core sözleşmeler
- Tek giriş: ISender (Send/Publish/Stream)
- ValueTask tabanlı asenkron akış
- Deterministik pipeline sırası (kayıt sırasına göre)

## Yol Haritası
- Mediatoid.Behaviors: Logging, Validation, Metrics, Retry/Idempotency
- Mediatoid.SourceGen: Source generator ile sıfır reflection yol
- Mediatoid.AspNetCore: Minimal API helper’lar

## Changelog
Bkz. [CHANGELOG.md](./CHANGELOG.md)

## Lisans
MIT — [LICENSE](./LICENSE)
