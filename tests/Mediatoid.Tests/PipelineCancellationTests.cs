using Mediatoid.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Mediatoid.Tests;

public sealed record CancelReq(string Text) : IRequest<string>;

public sealed class CancelReqHandler : IRequestHandler<CancelReq, string>
{
    public ValueTask<string> Handle(CancelReq request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(request.Text);
    }
}

public sealed class CCCCancelFirst<TReq, TRes> : IPipelineBehavior<TReq, TRes>
    where TReq : IRequest<TRes>
{
    public ValueTask<TRes> Handle(
        TReq request,
        RequestHandlerContinuation<TRes> continuation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        cancellationToken.ThrowIfCancellationRequested();
        return continuation();
    }
}

public class PipelineCancellationTests
{
    [Fact]
    public async Task CancellationTokenPropagatesThroughPipeline()
    {
        var sp = new ServiceCollection()
            .AddMediatoid(typeof(PipelineCancellationTests).Assembly)
            .BuildServiceProvider();

        var sender = sp.GetRequiredService<ISender>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await sender.Send(new CancelReq("x"), cts.Token));
    }
}
