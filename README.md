# Mediatoid

[![NuGet](https://img.shields.io/nuget/v/Mediatoid.svg)](https://www.nuget.org/packages/Mediatoid)
[![Downloads](https://img.shields.io/nuget/dt/Mediatoid.svg)](https://www.nuget.org/packages/Mediatoid)

[![NuGet (Core)](https://img.shields.io/nuget/v/Mediatoid.Core.svg)](https://www.nuget.org/packages/Mediatoid.Core)
[![Downloads (Core)](https://img.shields.io/nuget/dt/Mediatoid.Core.svg)](https://www.nuget.org/packages/Mediatoid.Core)

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

// Servisleri yapılandır
var services = new ServiceCollection()
    .AddMediatoid(typeof(Program).Assembly) // Handler'ları tara
    .BuildServiceProvider();

// ISender servisini al
var sender = services.GetRequiredService<ISender>();

// Request/Response: Sipariş oluştur
var id = await sender.Send(new CreateOrder("cust-1", 120m));
Console.WriteLine($"Order ID: {id}");

// Notification: Sipariş oluşturuldu bildirimi yayınla
await sender.Publish(new OrderCreated(id));

// Stream: Sayı listesini akış olarak al
await foreach (var number in sender.Stream(new ListNumbers(3)))
{
    Console.WriteLine($"Streamed number: {number}");
}
```
Request/Response (CreateOrder):
```csharp
public sealed record CreateOrder(string CustomerId, decimal Total) : IRequest<Guid>;

public sealed class CreateOrderHandler : IRequestHandler<CreateOrder, Guid>
{
    public ValueTask<Guid> Handle(CreateOrder request, CancellationToken ct)
        => ValueTask.FromResult(Guid.NewGuid());
}
```
Notification (OrderCreated):
```csharp
public sealed record OrderCreated(Guid OrderId) : INotification;

public sealed class OrderCreatedHandler : INotificationHandler<OrderCreated>
{
    public ValueTask Handle(OrderCreated notification, CancellationToken ct)
        => ValueTask.CompletedTask;
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


Semantik (v0.2.0 ile donduruldu):
- Kapsam: Pipeline yalnızca `Send` için uygulanır. `Publish` ve `Stream` için pipeline yoktur.
- Sıra: Deterministik ve kayıt sırasına göre dıştan içe uygulanır.
  - Assembly sırası: `AddMediatoid(asm1, asm2, ...)` parametre sırası.
  - Her assembly içinde: `Type.FullName` alfabetik (Ordinal) sırası.
- Lifetime: Handler/behavior `Transient`, `ISender` `Scoped`.
- Open/closed generic birlikte desteklenir.
- Short-circuit mümkündür (continuation çağrılmayabilir).
- İptal ve hatalar wrap edilmeden yüzeye akar.

Detaylar:
- Bkz. [Pipeline Semantiği](./docs/pipeline-semantics.md)

## Tasarım İlkeleri
- Minimal Core sözleşmeler
- Tek giriş: ISender (Send/Publish/Stream)
- ValueTask tabanlı asenkron akış
- Deterministik pipeline sırası

## Yol Haritası
- Mediatoid.Behaviors: Logging, Validation, Metrics, Retry/Idempotency
- Mediatoid.SourceGen: Source generator ile sıfır reflection yol
- Mediatoid.AspNetCore: Minimal API helper’lar
- Publish/Stream için pipeline (RFC ve tasarım) — sonraki minor sürümde değerlendirilecektir

## Changelog
Bkz. [CHANGELOG.md](./CHANGELOG.md)

## Lisans
MIT — [LICENSE](./LICENSE)
