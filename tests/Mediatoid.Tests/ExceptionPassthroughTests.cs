using Mediatoid;
using Microsoft.Extensions.DependencyInjection;

public sealed record Boom(string Reason) : IRequest<string>;

public sealed class BoomHandler : IRequestHandler<Boom, string>
{
    public ValueTask<string> Handle(Boom request, CancellationToken cancellationToken)
        => throw new InvalidOperationException("boom");
}

public class ExceptionPassthroughTests
{
    [Fact]
    public async Task Send_Should_Propagate_Handler_Exception_Without_Wrap()
    {
        var sp = new ServiceCollection()
            .AddMediatoid(typeof(ExceptionPassthroughTests).Assembly)
            .BuildServiceProvider();

        var sender = sp.GetRequiredService<ISender>();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => sender.Send(new Boom("e")).AsTask());
    }
}
