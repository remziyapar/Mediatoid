using Mediatoid.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Mediatoid.Tests;

public sealed record PingTL(string Text) : IRequest<string>;

public sealed class PingTLHandler : IRequestHandler<PingTL, string>
{
    public ValueTask<string> Handle(PingTL request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ValueTask.FromResult(request.Text);
    }
}

public sealed class InstanceTracker<TReq, TRes> : IPipelineBehavior<TReq, TRes>
    where TReq : IRequest<TRes>
{
    private static readonly object Gate = new();
    internal static HashSet<Guid> InstanceIds = [];

    private readonly Guid _id = Guid.NewGuid();

    public async ValueTask<TRes> Handle(
        TReq request,
        RequestHandlerContinuation<TRes> continuation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(continuation);

        lock (Gate)
        {
            _ = InstanceIds.Add(_id);
        }

        return await continuation();
    }
}

public class PipelineTransientLifetimeTests
{
    [Fact]
    public async Task BehaviorsAreResolvedAsTransientPerSendCall()
    {
        // reset
        InstanceTracker<PingTL, string>.InstanceIds = [];

        var sp = new ServiceCollection()
            .AddMediatoid(typeof(PipelineTransientLifetimeTests).Assembly)
            .BuildServiceProvider();

        var sender = sp.GetRequiredService<ISender>();
        _ = await sender.Send(new PingTL("a"));
        _ = await sender.Send(new PingTL("b"));

        Assert.Equal(2, InstanceTracker<PingTL, string>.InstanceIds.Count);
    }
}
