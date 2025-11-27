using System.Reflection;
using Mediatoid;
using Microsoft.Extensions.DependencyInjection;

namespace Mediatoid.Tests;

public sealed record FastPathPing(string Message) : IRequest<string>;

public sealed class FastPathPingHandler : IRequestHandler<FastPathPing, string>
{
    public ValueTask<string> Handle(FastPathPing request, CancellationToken cancellationToken)
        => ValueTask.FromResult(request.Message);
}

public class GeneratedFastPathTests
{
    [Fact]
    public async Task FirstSend_Should_Return_Response()
    {
        var sp = new ServiceCollection()
            .AddMediatoid(typeof(GeneratedFastPathTests).Assembly)
            .BuildServiceProvider();

        var sender = sp.GetRequiredService<ISender>();
        var result = await sender.Send(new FastPathPing("hi"));
        Assert.Equal("hi", result);
    }
}
