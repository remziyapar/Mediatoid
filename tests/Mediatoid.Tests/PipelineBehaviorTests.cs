using Mediatoid;
using Mediatoid.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Mediatoid.Tests;

// Basit istek/yanıt
public sealed record Echo(string Text) : IRequest<string>;

public sealed class EchoHandler : IRequestHandler<Echo, string>
{
    public ValueTask<string> Handle(Echo request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult($"[{request.Text}]");
    }
}

// Log amaçlı behavior (open generic)
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    internal static readonly List<string> Logs = new();

    public async ValueTask<TResponse> Handle(
        TRequest request,
        RequestHandlerContinuation<TResponse> continuation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(continuation);

        Logs.Add($"before:{typeof(TRequest).Name}");
        var response = await continuation();
        Logs.Add($"after:{typeof(TRequest).Name}");
        return response;
    }
}

public class PipelineBehaviorTests
{
    [Fact]
    public async Task SendShouldInvokePipelineBehaviorAroundHandler()
    {
        LoggingBehavior<Echo, string>.Logs.Clear();

        var services = new ServiceCollection()
            .AddMediatoid(typeof(PipelineBehaviorTests).Assembly)
            .BuildServiceProvider();

        var sender = services.GetRequiredService<ISender>();

        var result = await sender.Send(new Echo("hi"));

        Assert.Equal("[hi]", result);
        Assert.Equal(2, LoggingBehavior<Echo, string>.Logs.Count);
        Assert.Equal("before:Echo", LoggingBehavior<Echo, string>.Logs[0]);
        Assert.Equal("after:Echo", LoggingBehavior<Echo, string>.Logs[1]);
    }
}
