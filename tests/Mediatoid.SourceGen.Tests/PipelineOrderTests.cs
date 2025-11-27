using System;
using System.Threading;
using System.Threading.Tasks;
using Mediatoid;
using Mediatoid.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mediatoid.SourceGen.Tests;

public sealed record OrderPing(int Value) : IRequest<int>;

public sealed class OrderPingHandler : IRequestHandler<OrderPing, int>
{
    public ValueTask<int> Handle(OrderPing request, CancellationToken cancellationToken)
        => ValueTask.FromResult(request.Value);
}

// İsimsel sıralama: ABehavior dış (outer), ZBehavior iç (inner) — manifest yoksa FullName ordinal sırası.
public sealed class ABehavior<TReq, TRes> : IPipelineBehavior<TReq, TRes> where TReq : IRequest<TRes>
{
    public async ValueTask<TRes> Handle(TReq request, RequestHandlerContinuation<TRes> continuation, CancellationToken cancellationToken)
    {
        var res = await continuation();
        return res switch
        {
            int v => (TRes)(object)(v + 1),
            _ => res
        };
    }
}

public sealed class ZBehavior<TReq, TRes> : IPipelineBehavior<TReq, TRes> where TReq : IRequest<TRes>
{
    public async ValueTask<TRes> Handle(TReq request, RequestHandlerContinuation<TRes> continuation, CancellationToken cancellationToken)
    {
        var res = await continuation();
        return res switch
        {
            int v => (TRes)(object)(v * 2),
            _ => res
        };
    }
}

public class PipelineOrderTests
{
    [Fact]
    public async Task Behaviors_Should_Apply_In_Ordinal_FullName_Order()
    {
        var sp = new ServiceCollection()
            .AddMediatoid(typeof(PipelineOrderTests).Assembly)
            .BuildServiceProvider();

        var sender = sp.GetRequiredService<ISender>();
        var result = await sender.Send(new OrderPing(10));

        // Sıra: A dış → Z iç => ((10 * 2) + 1) = 21
        Assert.Equal(21, result);
    }
}
