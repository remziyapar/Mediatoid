using Mediatoid;
using Microsoft.Extensions.DependencyInjection;

[assembly: MediatoidRoot]

var services = new ServiceCollection()
    .AddMediatoid(typeof(Program).Assembly)
    .BuildServiceProvider();

var sender = services.GetRequiredService<ISender>();

var id = await sender.Send(new Ping("hello"));
Console.WriteLine($"Response: {id}");

/// <summary>Simple ping request.</summary>
public sealed record Ping(string Message) : IRequest<string>;

/// <summary>Handles <see cref="Ping"/> requests.</summary>
public sealed class PingHandler : IRequestHandler<Ping, string>
{
    /// <summary>Returns a pong response for the given ping.</summary>
    public ValueTask<string> Handle(Ping request, CancellationToken ct) =>
        ValueTask.FromResult($"Pong: {request.Message}");
}
