using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Mediatoid;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Mediatoid.SourceGen.Tests;

public sealed record SgPing(string Message) : IRequest<string>;
public sealed class SgPingHandler : IRequestHandler<SgPing, string>
{
    public ValueTask<string> Handle(SgPing request, CancellationToken cancellationToken)
        => ValueTask.FromResult(request.Message);
}

public class GeneratedDispatchTests
{
    [Fact]
    public void GeneratedRegistry_Should_Contain_Handler_Map()
    {
        var regType = AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(a => a.GetType("Mediatoid.Generated.MediatoidGeneratedRegistry", false))
            .FirstOrDefault(t => t is not null);

        Assert.NotNull(regType);

        var mapsField = regType!.GetField("Maps", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(mapsField);

        var arr = mapsField!.GetValue(null) as Array;
        Assert.NotNull(arr);
        Assert.True(arr!.Length > 0);

        var hasPing = arr.Cast<object>().Any(o =>
        {
            var t = o.GetType();
            var svc = (Type)t.GetField("Service")!.GetValue(o)!;
            var impl = (Type)t.GetField("Implementation")!.GetValue(o)!;
            return svc.IsGenericType &&
                   svc.GetGenericTypeDefinition() == typeof(IRequestHandler<,>) &&
                   svc.GetGenericArguments()[0] == typeof(SgPing) &&
                   impl == typeof(SgPingHandler);
        });

        Assert.True(hasPing);
    }

    [Fact]
    public async Task Mediator_Send_Should_Return_Response()
    {
        var sp = new ServiceCollection()
            .AddMediatoid(typeof(GeneratedDispatchTests).Assembly)
            .AddTransient<IRequestHandler<SgPing, string>, SgPingHandler>()
            .BuildServiceProvider();

        var sender = sp.GetRequiredService<ISender>();
        var val = await sender.Send(new SgPing("x"));
        Assert.Equal("x", val);
    }

    [Fact]
    public void GeneratedDispatch_Should_Expose_TryInvoke_Method()
    {
        var dispatchType = AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(a => a.GetType("Mediatoid.Generated.MediatoidGeneratedDispatch", false))
            .FirstOrDefault(t => t is not null);

        Assert.NotNull(dispatchType);

        var method = dispatchType!.GetMethod("TryInvoke", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        Assert.True(method!.IsGenericMethodDefinition);
        var pars = method.GetParameters();
        Assert.Equal(3, pars.Length);
    }
}
