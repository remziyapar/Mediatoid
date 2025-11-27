using Mediatoid;
using Mediatoid.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Mediatoid.SourceGen.Tests;

public sealed record ScPing(string Name) : IRequest<string>;
public sealed class ScPingHandler : IRequestHandler<ScPing, string>
{
    public ValueTask<string> Handle(ScPing request, CancellationToken ct)
        => ValueTask.FromResult($"Hello {request.Name}");
}

// Yalnız ScPing->string için kısa devre
internal sealed class StopBehavior : IPipelineBehavior<ScPing, string>
{
    public ValueTask<string> Handle(ScPing request, RequestHandlerContinuation<string> continuation, CancellationToken ct)
        => ValueTask.FromResult("[stopped]");
}

public sealed class ShortCircuitPipelineTests
{
    [Fact]
    public async Task StopBehavior_Should_ShortCircuit_Response()
    {
        var services = new ServiceCollection()
            .AddMediatoid(typeof(object).Assembly) // nötr tarama
            .AddTransient<IRequestHandler<ScPing, string>, ScPingHandler>()
            .AddTransient<IPipelineBehavior<ScPing, string>, StopBehavior>()
            // Generated pipeline ScPing için A/Z behaviors çözmeye çalışır; closed kayıtlarını ekleyin:
            .AddTransient<IPipelineBehavior<ScPing, string>, ABehavior<ScPing, string>>()
            .AddTransient<IPipelineBehavior<ScPing, string>, ZBehavior<ScPing, string>>();

        using var sp = services.BuildServiceProvider();
        var sender = sp.GetRequiredService<ISender>();

        var res = await sender.Send(new ScPing("Ada"));
        Assert.Equal("[stopped]", res);
    }
}
