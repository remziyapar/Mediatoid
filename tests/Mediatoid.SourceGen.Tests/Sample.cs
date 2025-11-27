using Mediatoid.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Mediatoid.SourceGen.Tests;

internal static class Di
{
    public static IServiceProvider Build() =>
        new ServiceCollection()
            .AddMediatoid(typeof(Di).Assembly)
            .BuildServiceProvider();
}

public record Greet(string Name) : IRequest<string>;

public class GreetHandler : IRequestHandler<Greet, string>
{
    private static int _globalCallCount;
    public static int LastTestCallCount { get; private set; }

    public static void Reset()
    {
        LastTestCallCount = 0;
    }

    public ValueTask<string> Handle(Greet request, CancellationToken ct)
    {
        LastTestCallCount++;
        Interlocked.Increment(ref _globalCallCount);
        return ValueTask.FromResult($"Hello {request.Name}");
    }
}

// Merkezi log deposu – request/response + behavior adı ile indekslenir
public static class BehaviorLogStore
{
    private static readonly object _gate = new();
    private static readonly Dictionary<(Type Req, Type Res, string Behavior), List<string>> _logs = new();

    // Teşhis alanı: Her Send çağrısı için korelasyon + kaynak (GEN/RT)
    public static readonly AsyncLocal<(Guid CorrelationId, string Source)> Current = new();

    public static void Add<TReq, TRes>(string behavior, string entry)
    {
        lock (_gate)
        {
            var key = (typeof(TReq), typeof(TRes), behavior);
            if (!_logs.TryGetValue(key, out var list))
            {
                list = new List<string>(8);
                _logs[key] = list;
            }

            var (cid, src) = Current.Value;
            // entry zenginleştirme: A:before [GEN:3f2e...] gibi
            if (cid != Guid.Empty && !string.IsNullOrEmpty(src))
                list.Add($"{entry} [{src}:{cid}]");
            else
                list.Add(entry);
        }
    }

    public static IReadOnlyList<string> Get<TReq, TRes>(string behavior)
    {
        lock (_gate)
        {
            var key = (typeof(TReq), typeof(TRes), behavior);
            return _logs.TryGetValue(key, out var list) ? list : Array.Empty<string>();
        }
    }

    public static void Clear<TReq, TRes>(string behavior)
    {
        lock (_gate)
        {
            var key = (typeof(TReq), typeof(TRes), behavior);
            _logs.Remove(key);
        }
    }

    // Tüm kayıtları temizlemek istersen (isteğe bağlı)
    public static void ClearAll()
    {
        lock (_gate)
        {
            _logs.Clear();
        }
    }
}

public class BehaviorA<TReq, TRes> : IPipelineBehavior<TReq, TRes> where TReq : IRequest<TRes>
{
    public async ValueTask<TRes> Handle(TReq r, RequestHandlerContinuation<TRes> c, CancellationToken ct)
    {
        BehaviorLogStore.Add<TReq, TRes>("A", "A:before");
        var res = await c().ConfigureAwait(false);
        BehaviorLogStore.Add<TReq, TRes>("A", "A:after");
        return res;
    }
}

public class BehaviorB<TReq, TRes> : IPipelineBehavior<TReq, TRes> where TReq : IRequest<TRes>
{
    public async ValueTask<TRes> Handle(TReq r, RequestHandlerContinuation<TRes> c, CancellationToken ct)
    {
        BehaviorLogStore.Add<TReq, TRes>("B", "B:before");
        var res = await c().ConfigureAwait(false);
        BehaviorLogStore.Add<TReq, TRes>("B", "B:after");
        return res;
    }
}
