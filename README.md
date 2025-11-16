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

**CLI:**

```bash
dotnet add package Mediatoid
```

**NuGet UI:**
“Mediatoid” paketini ekleyin (`Mediatoid.Core` transitif gelir).

## Hızlı Başlangıç

**Program.cs (kurulum ve çağrılar):**

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

**Semantik (v0.2.0 ile donduruldu):**

  - **Kapsam:** Pipeline yalnızca `Send` için uygulanır. `Publish` ve `Stream` için pipeline yoktur.
  - **Sıra:** Deterministik ve kayıt sırasına göre dıştan içe uygulanır.
      - Assembly sırası: `AddMediatoid(asm1, asm2, ...)` parametre sırası.
      - Her assembly içinde: `Type.FullName` alfabetik (Ordinal) sırası.
  - **Lifetime:** Handler/behavior `Transient`, `ISender` `Scoped`.
  - Open/closed generic birlikte desteklenir.
  - Short-circuit mümkündür (continuation çağrılmayabilir).
  - İptal ve hatalar wrap edilmeden yüzeye akar.

**Detaylar:**

  - [Pipeline Semantiği](https://github.com/remziyapar/Mediatoid/blob/main/docs/pipeline-semantics.md)

## Opsiyonel Paketler

### Mediatoid.Behaviors

Logging ve Validation davranışları:

```bash
dotnet add package Mediatoid.Behaviors
dotnet add package FluentValidation
dotnet add package FluentValidation.DependencyInjectionExtensions
```

**Kullanım:**

```csharp
var services = new ServiceCollection()
    .AddMediatoid(typeof(Program).Assembly)
    .AddValidatorsFromAssembly(typeof(Program).Assembly) // FluentValidation kayıtları
    .AddMediatoidBehaviors()                          // Logging + Validation
    .BuildServiceProvider();
```

**Örnek validator:**

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

> Geçersiz istekte `ValidationException` (Mediatoid.Behaviors) fırlatılır ve tüm hatalar aggregate edilir.

### Mediatoid.SourceGen

Handler keşfini build-time’a taşır; runtime reflection ve `MethodInfo.Invoke` maliyetini azaltır.

**Kurulum:**

```bash
dotnet add package Mediatoid.SourceGen
```

Ek konfigurasyon gerekmez; paket eklendiğinde generator devreye girer. Pipeline davranış zinciri (behaviors) hâlâ runtime’da compose edilir (deterministik sıra korunur).

> **Not:**
> Tam pipeline zincirinin (handler + behavior delegate’lerinin compile-time üretimi) sonraki minor sürümde (v0.4.x) eklenmesi planlanmaktadır. Şu an SourceGen yalnızca handler terminal optimizasyonu yapar.

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


Yorumlar:
- Send baseline ~420 ns; Logging eklemek ~+70% süre / ~+67% bellek (ek Stopwatch + ILogger formatı).
- Validation eklemek ~+45% süre / ~+63% bellek (validator çalıştırma + sonuç nesneleri).
- Publish iki handler çağrısında ~315 ns; fan-out handler sayısı ile lineer büyür.
- Stream ilk 10 öğe ~9.7 µs (enumeration + async yield maliyeti).

Not: SourceGen v0.3.* sürümünde handler terminal optimizasyonu sağlar; tam pipeline zincir üretimi geldiğinde baseline çağrı süresi daha da düşebilir.

## Tasarım İlkeleri

  - Minimal Core sözleşmeler
  - Tek giriş: `ISender` (Send/Publish/Stream)
  - `ValueTask` tabanlı asenkron akış
  - Deterministik pipeline sırası
  - Exception wrap yok (doğrudan fırlatma)

## Yol Haritası

  - **Mediatoid.Behaviors:** Logging, Validation, Metrics, Retry/Idempotency
  - **Mediatoid.SourceGen:** Pipeline zinciri üretimi (tam optimizasyon)
  - **Mediatoid.AspNetCore:** Minimal API helper’lar
  - **Publish/Stream için pipeline** (RFC değerlendirme)

## Changelog

Bkz. [CHANGELOG.md](https://github.com/remziyapar/Mediatoid/blob/main/CHANGELOG.md)

## Lisans

MIT — [LICENSE](https://github.com/remziyapar/Mediatoid/blob/main/LICENSE)
