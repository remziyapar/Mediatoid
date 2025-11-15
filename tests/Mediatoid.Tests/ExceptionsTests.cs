using Microsoft.Extensions.DependencyInjection;

namespace Mediatoid.Tests;

public sealed record UnknownRequest(string Payload) : IRequest<string>;

public class ExceptionsTests
{
    [Fact]
    public async Task SendWithoutHandlerThrows()
    {
        var sp = new ServiceCollection()
            .AddMediatoid(typeof(ExceptionsTests).Assembly)
            .BuildServiceProvider();

        var sender = sp.GetRequiredService<ISender>();

        Task Act()
        {
            return sender.Send(new UnknownRequest("x")).AsTask();
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(Act);

        Assert.Contains("No handler registered for request type", ex.Message);
    }
}
