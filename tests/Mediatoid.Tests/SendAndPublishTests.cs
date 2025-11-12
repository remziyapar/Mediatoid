using Mediatoid;
using Microsoft.Extensions.DependencyInjection;

namespace Mediatoid.Tests;

public sealed record Ping(string Message) : IRequest<string>;

public sealed class PingHandler : IRequestHandler<Ping, string>
{
    public ValueTask<string> Handle(Ping request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult("PONG:" + request.Message);
    }
}

public sealed record Created(string Id) : INotification;

public sealed class CreatedHandlerA : INotificationHandler<Created>
{
    internal static int Count;

    public ValueTask Handle(Created notification, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref Count);
        return ValueTask.CompletedTask;
    }
}

public sealed class CreatedHandlerB : INotificationHandler<Created>
{
    internal static int Count;

    public ValueTask Handle(Created notification, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref Count);
        return ValueTask.CompletedTask;
    }
}

public class SendAndPublishTests
{
    [Fact]
    public async Task SendShouldInvokeHandlerAndReturnResult()
    {
        var services = new ServiceCollection()
            .AddMediatoid(typeof(SendAndPublishTests).Assembly)
            .BuildServiceProvider();

        var sender = services.GetRequiredService<ISender>();

        var result = await sender.Send(new Ping("HELLO"));

        Assert.Equal("PONG:HELLO", result);
    }

    [Fact]
    public async Task PublishShouldInvokeAllNotificationHandlers()
    {
        CreatedHandlerA.Count = 0;
        CreatedHandlerB.Count = 0;

        var services = new ServiceCollection()
            .AddMediatoid(typeof(SendAndPublishTests).Assembly)
            .BuildServiceProvider();

        var sender = services.GetRequiredService<ISender>();

        await sender.Publish(new Created("id-1"));

        Assert.Equal(1, CreatedHandlerA.Count);
        Assert.Equal(1, CreatedHandlerB.Count);
    }
}
