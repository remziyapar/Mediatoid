using Mediatoid;
using Mediatoid.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Mediatoid.Tests;

public sealed record EchoOrder(string Text) : IRequest<string>;
public sealed class EchoOrderHandler : IRequestHandler<EchoOrder, string>
{
    public ValueTask<string> Handle(EchoOrder request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(request.Text);
    }
}

public sealed class BehaviorA<TReq, TRes> : IPipelineBehavior<TReq, TRes>
    where TReq : IRequest<TRes>
{
    internal static readonly List<string> Logs = new();

    public async ValueTask<TRes> Handle(
        TReq request,
        RequestHandlerContinuation<TRes> continuation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(continuation);

        Logs.Add("A:before");
        var res = await continuation();
        Logs.Add("A:after");
        return res;
    }
}

public sealed class BehaviorB<TReq, TRes> : IPipelineBehavior<TReq, TRes>
    where TReq : IRequest<TRes>
{
    internal static readonly List<string> Logs = new();

    public async ValueTask<TRes> Handle(
        TReq request,
        RequestHandlerContinuation<TRes> continuation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(continuation);

        Logs.Add("B:before");
        var res = await continuation();
        Logs.Add("B:after");
        return res;
    }
}

public class PipelineOrderTests
{
    private static readonly string[] ExpectedALogs = new[] { "A:before", "A:after" };
    private static readonly string[] ExpectedBLogs = new[] { "B:before", "B:after" };

    [Fact]
    public async Task BehaviorsInvokeInRegistrationOrder()
    {
        BehaviorA<EchoOrder, string>.Logs.Clear();
        BehaviorB<EchoOrder, string>.Logs.Clear();

        var sp = new ServiceCollection()
            .AddMediatoid(typeof(PipelineOrderTests).Assembly)
            .BuildServiceProvider();

        var sender = sp.GetRequiredService<ISender>();
        _ = await sender.Send(new EchoOrder("ok"));

        // Taramada FullName'e göre alfabetik kayıt → A, sonra B
        Assert.Equal(ExpectedALogs, BehaviorA<EchoOrder, string>.Logs);
        Assert.Equal(ExpectedBLogs, BehaviorB<EchoOrder, string>.Logs);
    }
}
