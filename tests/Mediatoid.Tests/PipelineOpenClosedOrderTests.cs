using Mediatoid.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Mediatoid.Tests;

public sealed record EchoOC(string Text) : IRequest<string>;

public sealed class EchoOCHandler : IRequestHandler<EchoOC, string>
{
    public ValueTask<string> Handle(EchoOC request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(request.Text);
    }
}

public static class OrderLog
{
    internal static readonly List<string> Entries = [];
    internal static readonly object Gate = new();
}

// Closed-generic – adı alfabetik olarak önce gelir
public sealed class AClosedBehavior : IPipelineBehavior<EchoOC, string>
{
    public async ValueTask<string> Handle(
        EchoOC request,
        RequestHandlerContinuation<string> continuation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(continuation);

        lock (OrderLog.Gate)
        {
            OrderLog.Entries.Add("A:before");
        }

        var res = await continuation();
        lock (OrderLog.Gate)
        {
            OrderLog.Entries.Add("A:after");
        }

        return res;
    }
}

// Open-generic – adı alfabetik olarak sonra gelir
public sealed class ZOpenBehavior<TReq, TRes> : IPipelineBehavior<TReq, TRes>
    where TReq : IRequest<TRes>
{
    public async ValueTask<TRes> Handle(
        TReq request,
        RequestHandlerContinuation<TRes> continuation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(continuation);

        // Sadece EchoOC isteklerinde log yaz (diğer testlerle çakışmayı önle)
        if (request is EchoOC)
        {
            lock (OrderLog.Gate)
            {
                OrderLog.Entries.Add("Z:before");
            }
        }

        var res = await continuation();

        if (request is EchoOC)
        {
            lock (OrderLog.Gate)
            {
                OrderLog.Entries.Add("Z:after");
            }
        }

        return res;
    }
}

public class PipelineOpenClosedOrderTests
{
    private static readonly string[] expected = ["A:before", "Z:before", "Z:after", "A:after"];

    [Fact]
    public async Task ClosedThenOpenBehaviorComposeOutsideIn()
    {
        OrderLog.Entries.Clear();

        var sp = new ServiceCollection()
            .AddMediatoid(typeof(PipelineOpenClosedOrderTests).Assembly)
            .BuildServiceProvider();

        var sender = sp.GetRequiredService<ISender>();
        var result = await sender.Send(new EchoOC("ok"));

        Assert.Equal("ok", result);

        // Snapshot al, paralel testlerden etkilenme
        string[] snapshot;
        lock (OrderLog.Gate)
        {
            snapshot = [.. OrderLog.Entries];
        }

        Assert.Equal(expected, snapshot);
    }
}
