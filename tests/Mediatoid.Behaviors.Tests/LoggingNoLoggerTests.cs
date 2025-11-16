using Mediatoid;
using Mediatoid.Behaviors;
using Microsoft.Extensions.DependencyInjection;

public sealed record NoLogReq(string Text) : IRequest<string>;
public sealed class NoLogReqHandler : IRequestHandler_NoLogReq, IRequestHandler<NoLogReq, string>
{
    public ValueTask<string> Handle(NoLogReq request, CancellationToken cancellationToken)
        => ValueTask.FromResult(request.Text);
}

public interface IRequestHandler_NoLogReq { }

public class LoggingNoLoggerTests
{
    [Fact]
    public async Task LoggingBehavior_Should_Not_Require_ILogger()
    {
        var sp = new ServiceCollection()
            .AddMediatoid(typeof(LoggingNoLoggerTests).Assembly)
            .AddMediatoidBehaviors()
            .BuildServiceProvider();

        var sender = sp.GetRequiredService<ISender>();
        var res = await sender.Send(new NoLogReq("ok"));

        Assert.Equal("ok", res);
    }
}
