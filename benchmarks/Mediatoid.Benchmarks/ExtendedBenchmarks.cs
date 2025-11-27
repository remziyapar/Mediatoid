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

    // Provider'lar CA2000 için tutulup GlobalCleanup'ta dispose edilir
    private ServiceProvider _sp0 = default!;
    private ServiceProvider _sp1 = default!;
    private ServiceProvider _sp2 = default!;
    private ServiceProvider _sp4 = default!;
    private ServiceProvider _sp8 = default!;

    [GlobalSetup]
    public void Setup()
    {
        _sp0 = BuildDepthProvider(0);
        _sp1 = BuildDepthProvider(1);
        _sp2 = BuildDepthProvider(2);
        _sp4 = BuildDepthProvider(4);
        _sp8 = BuildDepthProvider(8);

        _d0 = _sp0.GetRequiredService<ISender>();
        _d1 = _sp1.GetRequiredService<ISender>();
        _d2 = _sp2.GetRequiredService<ISender>();
        _d4 = _sp4.GetRequiredService<ISender>();
        _d8 = _sp8.GetRequiredService<ISender>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _sp0?.Dispose();
        _sp1?.Dispose();
        _sp2?.Dispose();
        _sp4?.Dispose();
        _sp8?.Dispose();
    }

    private static ServiceProvider BuildDepthProvider(int depth)
    {
        // Bu assembly'i tarama: behaviors tüm derinliklerde otomatik eklenirdi.
        // Bunun yerine nötr bir assembly verip gerekenleri manuel ekliyoruz.
        var sc = new ServiceCollection().AddMediatoid(typeof(object).Assembly);

        // Handler kaydı (manuel)
        sc.AddTransient<IRequestHandler<DepthPing, string>, DepthPingHandler>();

        // Derinlik kadar behavior ekle (deterministik kayıt sırası)
        if (depth >= 1) sc.AddTransient(typeof(IPipelineBehavior<,>), typeof(DepthBehavior1<,>));
        if (depth >= 2) sc.AddTransient(typeof(IPipelineBehavior<,>), typeof(DepthBehavior2<,>));
        if (depth >= 3) sc.AddTransient(typeof(IPipelineBehavior<,>), typeof(DepthBehavior3<,>));
        if (depth >= 4) sc.AddTransient(typeof(IPipelineBehavior<,>), typeof(DepthBehavior4<,>));
        if (depth >= 5) sc.AddTransient(typeof(IPipelineBehavior<,>), typeof(DepthBehavior5<,>));
        if (depth >= 6) sc.AddTransient(typeof(IPipelineBehavior<,>), typeof(DepthBehavior6<,>));
        if (depth >= 7) sc.AddTransient(typeof(IPipelineBehavior<,>), typeof(DepthBehavior7<,>));
        if (depth >= 8) sc.AddTransient(typeof(IPipelineBehavior<,>), typeof(DepthBehavior8<,>));

        return sc.BuildServiceProvider();
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

    private ServiceProvider _sp1 = default!;
    private ServiceProvider _sp2 = default!;
    private ServiceProvider _sp4 = default!;
    private ServiceProvider _sp8 = default!;
    private ServiceProvider _sp16 = default!;

    private readonly FanOutNotif _notif = new(42);

    [GlobalSetup]
    public void Setup()
    {
        _sp1 = BuildFanOutProvider(1);
        _sp2 = BuildFanOutProvider(2);
        _sp4 = BuildFanOutProvider(4);
        _sp8 = BuildFanOutProvider(8);
        _sp16 = BuildFanOutProvider(16);

        _h1 = _sp1.GetRequiredService<ISender>();
        _h2 = _sp2.GetRequiredService<ISender>();
        _h4 = _sp4.GetRequiredService<ISender>();
        _h8 = _sp8.GetRequiredService<ISender>();
        _h16 = _sp16.GetRequiredService<ISender>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _sp1?.Dispose();
        _sp2?.Dispose();
        _sp4?.Dispose();
        _sp8?.Dispose();
        _sp16?.Dispose();
    }

    private static ServiceProvider BuildFanOutProvider(int count)
    {
        var sc = new ServiceCollection().AddMediatoid(typeof(object).Assembly);

        // Kademeli handler listesi; 'count' kadar manuel ekle
        var handlerTypes = new[]
        {
            typeof(FanOutHandler1), typeof(FanOutHandler2), typeof(FanOutHandler3), typeof(FanOutHandler4),
            typeof(FanOutHandler5), typeof(FanOutHandler6), typeof(FanOutHandler7), typeof(FanOutHandler8),
            typeof(FanOutHandler9), typeof(FanOutHandler10), typeof(FanOutHandler11), typeof(FanOutHandler12),
            typeof(FanOutHandler13), typeof(FanOutHandler14), typeof(FanOutHandler15), typeof(FanOutHandler16),
        };

        var toAdd = Math.Min(count, handlerTypes.Length);
        for (var i = 0; i < toAdd; i++)
            sc.AddTransient(typeof(INotificationHandler<FanOutNotif>), handlerTypes[i]);

        return sc.BuildServiceProvider();
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
    private ServiceProvider _warmSp = default!;

    [GlobalSetup]
    public void Setup()
    {
        // Nötr assembly + manuel handler kaydı (open generic depth behaviors sızmasın)
        _warmSp = new ServiceCollection()
            .AddMediatoid(typeof(object).Assembly)
            .AddTransient<IRequestHandler<ColdPing, string>, ColdPingHandler>()
            .BuildServiceProvider();

        _warmSender = _warmSp.GetRequiredService<ISender>();

        // Ön ısınma (handler & behavior invoker cache)
        _ = _warmSender.Send(_req).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup() => _warmSp?.Dispose();

    [Benchmark(Baseline = true)]
    public ValueTask<string> Send_Warm() => _warmSender.Send(_req);

    [Benchmark]
    public async ValueTask<string> Send_Cold()
    {
        await using var sp = new ServiceCollection()
            .AddMediatoid(typeof(object).Assembly)
            .AddTransient<IRequestHandler<ColdPing, string>, ColdPingHandler>()
            .BuildServiceProvider();

        var sender = sp.GetRequiredService<ISender>();
        return await sender.Send(_req).ConfigureAwait(false);
    }
}
#endregion
