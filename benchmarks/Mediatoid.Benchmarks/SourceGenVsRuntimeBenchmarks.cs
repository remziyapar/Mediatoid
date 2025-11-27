using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Mediatoid;
using Mediatoid.Pipeline;
using System.Threading;
using System.Threading.Tasks;

namespace Mediatoid.Benchmarks;

public sealed record SGReq(int X) : IRequest<int>;
public sealed class SGReqHandler : IRequestHandler<SGReq, int>
{
    public ValueTask<int> Handle(SGReq r, CancellationToken ct) => ValueTask.FromResult(r.X);
}

public sealed class PassBehavior<TReq, TRes> : IPipelineBehavior<TReq, TRes> where TReq : IRequest<TRes>
{
    public ValueTask<TRes> Handle(TReq r, RequestHandlerContinuation<TRes> c, CancellationToken ct) => c();
}

[MemoryDiagnoser, WarmupCount(3), IterationCount(15)]
public class SourceGenVsRuntimeBenchmarks
{
    private ISender _sgSender = default!;
    private ISender _rtSender = default!;
    private readonly SGReq _req = new(5);

    [GlobalSetup]
    public void Setup()
    {
        // SourceGen (root işaretli assembly) -> pipeline invoker build-time üretilmiş
        _sgSender = new ServiceCollection()
            .AddMediatoid(typeof(SourceGenVsRuntimeBenchmarks).Assembly)
            .AddTransient(typeof(IPipelineBehavior<,>), typeof(PassBehavior<,>))
            .BuildServiceProvider()
            .GetRequiredService<ISender>();

        // Runtime fallback: Root attribute olmayan ayrı bir dynamic assembly (örnek: behavior yine eklenecek)
        _rtSender = new ServiceCollection()
            .AddMediatoid(typeof(SourceGenVsRuntimeBenchmarks).Assembly) // Aynı assembly - test projesinde Root yok varsayımıyla fark
            .AddTransient(typeof(IPipelineBehavior<,>), typeof(PassBehavior<,>))
            .BuildServiceProvider()
            .GetRequiredService<ISender>();
    }

    [Benchmark(Baseline = true)]
    public ValueTask<int> Send_SourceGen() => _sgSender.Send(_req);

    [Benchmark]
    public ValueTask<int> Send_RuntimeCompose() => _rtSender.Send(_req);
}
