using Mediatoid.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Mediatoid.Tests;

public sealed record EchoSc(string Text) : IRequest<string>;

public sealed class EchoScHandler : IRequestHandler<EchoSc, string>
{
    internal static int InvokedCount;

    public ValueTask<string> Handle(EchoSc request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _ = Interlocked.Increment(ref InvokedCount);
        return ValueTask.FromResult($"[{request.Text}]");
    }
}

// Closed-generic, adı alfabetik olarak önde olsun (dış katman)
public sealed class AAAStopperBehavior : IPipelineBehavior<EchoSc, string>
{
    public ValueTask<string> Handle(
        EchoSc request,
        RequestHandlerContinuation<string> continuation,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult("[stopped]");
    }
}

public class PipelineShortCircuitTests
{
    [Fact]
    public async Task BehaviorCanShortCircuitAndSkipHandler()
    {
        EchoScHandler.InvokedCount = 0;

        var sp = new ServiceCollection()
            .AddMediatoid(typeof(PipelineShortCircuitTests).Assembly)
            .BuildServiceProvider();

        var sender = sp.GetRequiredService<ISender>();
        var result = await sender.Send(new EchoSc("hi"));

        Assert.Equal("[stopped]", result);
        Assert.Equal(0, EchoScHandler.InvokedCount);
    }
}
