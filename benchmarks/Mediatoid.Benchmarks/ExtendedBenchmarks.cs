using BenchmarkDotNet.Attributes;
using Mediatoid;
using Mediatoid.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;

// Ek metrik benchmark'ları: pipeline derinliği, publish fan-out, soğuk başlangıç.
namespace Mediatoid.Benchmarks;

#region Pipeline Depth Benchmarks
internal sealed record DepthPing(string Message) : IRequest<string>;
internal sealed class DepthPingHandler : IRequestHandler<DepthPing, string>
{
    public ValueTask<string> Handle(DepthPing request, CancellationToken ct) => ValueTask.FromResult(request.Message);
}

// Basit pass-through behaviors (iş yükü eklemez)
public sealed class DepthBehavior1<TReq, TRes> : IPipelineBehavior<TReq, TRes> where TReq : IRequest<TRes>
{ public ValueTask<TRes> Handle(TReq r, RequestHandlerContinuation<TRes> c, CancellationToken ct) => c(); }
public sealed class DepthBehavior2<TReq, TRes> : IPipelineBehavior<TReq, TRes> where TReq : IRequest<TRes>
{ public ValueTask<TRes> Handle(TReq r, RequestHandlerContinuation<TRes> c, CancellationToken ct) => c(); }
public sealed class DepthBehavior3<TReq, TRes> : IPipelineBehavior<TReq, TRes> where TReq : IRequest<TRes>
{ public ValueTask<TRes> Handle(TReq r, RequestHandlerContinuation<TRes> c, CancellationToken ct) => c(); }
public sealed class DepthBehavior4<TReq, TRes> : IPipelineBehavior<TReq, TRes> where TReq : IRequest<TRes>
{ public ValueTask<TRes> Handle(TReq r, RequestHandlerContinuation<TRes> c, CancellationToken ct) => c(); }
public sealed class DepthBehavior5<TReq, TRes> : IPipelineBehavior<TReq, TRes> where TReq : IRequest<TRes>
{ public ValueTask<TRes> Handle(TReq r, RequestHandlerContinuation<TRes> c, CancellationToken ct) => c(); }
public sealed class DepthBehavior6<TReq, TRes> : IPipelineBehavior<TReq, TRes> where TReq : IRequest<TRes>
{ public ValueTask<TRes> Handle(TReq r, RequestHandlerContinuation<TRes> c, CancellationToken ct) => c(); }
public sealed class DepthBehavior7<TReq, TRes> : IPipelineBehavior<TReq, TRes> where TReq : IRequest<TRes>
{ public ValueTask<TRes> Handle(TReq r, RequestHandlerContinuation<TRes> c, CancellationToken ct) => c(); }
public sealed class DepthBehavior8<TReq, TRes> : IPipelineBehavior<TReq, TRes> where TReq : IRequest<TRes>
{ public ValueTask<TRes> Handle(TReq r, RequestHandlerContinuation<TRes> c, CancellationToken ct) => c(); }

[MemoryDiagnoser]
[WarmupCount(3)]
[IterationCount(15)]
public class PipelineDepthBenchmarks
{
    private ISender _d0 = default!;
    private ISender _d1 = default!;
    private ISender _d2 = default!;
    private ISender _d4 = default!;
    private ISender _d8 = default!;
    private readonly DepthPing _ping = new("x");

    [GlobalSetup]
    public void Setup()
    {
        _d0 = Build(0);
        _d1 = Build(1);
        _d2 = Build(2);
        _d4 = Build(4);
        _d8 = Build(8);
    }

    private static ISender Build(int depth)
    {
        var sc = new ServiceCollection().AddMediatoid(typeof(PipelineDepthBenchmarks).Assembly);
        // Assembly ordinal taramasına göre behavior tip isimleri sıralanacak.
        // Derinlik kadar tip eklemek için koşullu registration.
        if (depth >= 1) sc.AddTransient(typeof(IPipelineBehavior<,>), typeof(DepthBehavior1<,>));
        if (depth >= 2) sc.AddTransient(typeof(IPipelineBehavior<,>), typeof(DepthBehavior2<,>));
        if (depth >= 3) sc.AddTransient(typeof(IPipelineBehavior<,>), typeof(DepthBehavior3<,>));
        if (depth >= 4) sc.AddTransient(typeof(IPipelineBehavior<,>), typeof(DepthBehavior4<,>));
        if (depth >= 5) sc.AddTransient(typeof(IPipelineBehavior<,>), typeof(DepthBehavior5<,>));
        if (depth >= 6) sc.AddTransient(typeof(IPipelineBehavior<,>), typeof(DepthBehavior6<,>));
        if (depth >= 7) sc.AddTransient(typeof(IPipelineBehavior<,>), typeof(DepthBehavior7<,>));
        if (depth >= 8) sc.AddTransient(typeof(IPipelineBehavior<,>), typeof(DepthBehavior8<,>));
        return sc.BuildServiceProvider().GetRequiredService<ISender>();
    }

    [Benchmark(Baseline = true)] public ValueTask<string> Send_Depth0() => _d0.Send(_ping);
    [Benchmark] public ValueTask<string> Send_Depth1() => _d1.Send(_ping);
    [Benchmark] public ValueTask<string> Send_Depth2() => _d2.Send(_ping);
    [Benchmark] public ValueTask<string> Send_Depth4() => _d4.Send(_ping);
    [Benchmark] public ValueTask<string> Send_Depth8() => _d8.Send(_ping);
}
#endregion

#region Publish Fan-Out Benchmarks
internal sealed record FanOutNotif(int Value) : INotification;
internal sealed class FanOutHandler1 : INotificationHandler<FanOutNotif> { public ValueTask Handle(FanOutNotif n, CancellationToken ct) => ValueTask.CompletedTask; }
internal sealed class FanOutHandler2 : INotificationHandler<FanOutNotif> { public ValueTask Handle(FanOutNotif n, CancellationToken ct) => ValueTask.CompletedTask; }
internal sealed class FanOutHandler3 : INotificationHandler<FanOutNotif> { public ValueTask Handle(FanOutNotif n, CancellationToken ct) => ValueTask.CompletedTask; }
internal sealed class FanOutHandler4 : INotificationHandler<FanOutNotif> { public ValueTask Handle(FanOutNotif n, CancellationToken ct) => ValueTask.CompletedTask; }
internal sealed class FanOutHandler5 : INotificationHandler<FanOutNotif> { public ValueTask Handle(FanOutNotif n, CancellationToken ct) => ValueTask.CompletedTask; }
internal sealed class FanOutHandler6 : INotificationHandler<FanOutNotif> { public ValueTask Handle(FanOutNotif n, CancellationToken ct) => ValueTask.CompletedTask; }
internal sealed class FanOutHandler7 : INotificationHandler<FanOutNotif> { public ValueTask Handle(FanOutNotif n, CancellationToken ct) => ValueTask.CompletedTask; }
internal sealed class FanOutHandler8 : INotificationHandler<FanOutNotif> { public ValueTask Handle(FanOutNotif n, CancellationToken ct) => ValueTask.CompletedTask; }
internal sealed class FanOutHandler9 : INotificationHandler<FanOutNotif> { public ValueTask Handle(FanOutNotif n, CancellationToken ct) => ValueTask.CompletedTask; }
internal sealed class FanOutHandler10 : INotificationHandler<FanOutNotif> { public ValueTask Handle(FanOutNotif n, CancellationToken ct) => ValueTask.CompletedTask; }
internal sealed class FanOutHandler11 : INotificationHandler<FanOutNotif> { public ValueTask Handle(FanOutNotif n, CancellationToken ct) => ValueTask.CompletedTask; }
internal sealed class FanOutHandler12 : INotificationHandler<FanOutNotif> { public ValueTask Handle(FanOutNotif n, CancellationToken ct) => ValueTask.CompletedTask; }
internal sealed class FanOutHandler13 : INotificationHandler<FanOutNotif> { public ValueTask Handle(FanOutNotif n, CancellationToken ct) => ValueTask.CompletedTask; }
internal sealed class FanOutHandler14 : INotificationHandler<FanOutNotif> { public ValueTask Handle(FanOutNotif n, CancellationToken ct) => ValueTask.CompletedTask; }
internal sealed class FanOutHandler15 : INotificationHandler<FanOutNotif> { public ValueTask Handle(FanOutNotif n, CancellationToken ct) => ValueTask.CompletedTask; }
internal sealed class FanOutHandler16 : INotificationHandler<FanOutNotif> { public ValueTask Handle(FanOutNotif n, CancellationToken ct) => ValueTask.CompletedTask; }

[MemoryDiagnoser]
[WarmupCount(3)]
[IterationCount(15)]
public class PublishFanOutBenchmarks
{
    private ISender _h1 = default!;
    private ISender _h2 = default!;
    private ISender _h4 = default!;
    private ISender _h8 = default!;
    private ISender _h16 = default!;
    private readonly FanOutNotif _notif = new(42);

    [GlobalSetup]
    public void Setup()
    {
        _h1 = Build(1);
        _h2 = Build(2);
        _h4 = Build(4);
        _h8 = Build(8);
        _h16 = Build(16);
    }

    private static ISender Build(int count)
    {
        var sc = new ServiceCollection().AddMediatoid(typeof(PublishFanOutBenchmarks).Assembly);
        // Handlers assembly taraması ile eklenecek; burada manual eklemeye gerek yok çünkü sınıflar mevcut assembly'de.
        return sc.BuildServiceProvider().GetRequiredService<ISender>();
    }

    [Benchmark(Baseline = true)] public ValueTask Publish_Handlers1() => _h1.Publish(_notif);
    [Benchmark] public ValueTask Publish_Handlers2() => _h2.Publish(_notif);
    [Benchmark] public ValueTask Publish_Handlers4() => _h4.Publish(_notif);
    [Benchmark] public ValueTask Publish_Handlers8() => _h8.Publish(_notif);
    [Benchmark] public ValueTask Publish_Handlers16() => _h16.Publish(_notif);
}
#endregion

#region Cold Start Benchmarks
internal sealed record ColdPing(string Message) : IRequest<string>;
internal sealed class ColdPingHandler : IRequestHandler<ColdPing, string>
{ public ValueTask<string> Handle(ColdPing r, CancellationToken ct) => ValueTask.FromResult(r.Message); }

[MemoryDiagnoser]
[WarmupCount(3)]
[IterationCount(15)]
public class ColdStartBenchmarks
{
    private readonly ColdPing _req = new("hi");
    private ISender _warmSender = default!;

    [GlobalSetup]
    public void Setup()
    {
        // Isınmış: ServiceProvider + ilk çağrı (cache dolumu)
        var sp = new ServiceCollection().AddMediatoid(typeof(ColdStartBenchmarks).Assembly).BuildServiceProvider();
        _warmSender = sp.GetRequiredService<ISender>();
        // Ön ısınma (handler & behavior invoker cache)
        _ = _warmSender.Send(_req).GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public ValueTask<string> Send_Warm() => _warmSender.Send(_req);

    [Benchmark]
    public ValueTask<string> Send_Cold()
    {
        // Her ölçümde yeni ServiceProvider & ilk çağrı (soğuk başlangıç maliyeti dahil)
        var sp = new ServiceCollection().AddMediatoid(typeof(ColdStartBenchmarks).Assembly).BuildServiceProvider();
        var sender = sp.GetRequiredService<ISender>();
        return sender.Send(_req);
    }
}
#endregion
