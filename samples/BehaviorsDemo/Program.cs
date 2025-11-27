using Mediatoid;
using Mediatoid.Behaviors;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection()
    .AddMediatoid(typeof(Program).Assembly)
    .AddMediatoidBehaviors()
    .BuildServiceProvider();

var sender = services.GetRequiredService<ISender>();

try
{
    // Invalid request to trigger ValidationBehavior
    await sender.Send(new CreateOrder("", -1m));
}
catch (ValidationException ex)
{
    Console.WriteLine("Validation failed:");
    foreach (var error in ex.Failures)
    {
        Console.WriteLine($" - {error.PropertyName}: {error.ErrorMessage}");
    }
}

/// <summary>Simple request to create an order.</summary>
public sealed record CreateOrder(string CustomerId, decimal Total) : IRequest<Guid>;

/// <summary>Handles <see cref="CreateOrder"/> requests.</summary>
public sealed class CreateOrderHandler : IRequestHandler<CreateOrder, Guid>
{
    /// <summary>Processes the create order request.</summary>
    public ValueTask<Guid> Handle(CreateOrder request, CancellationToken ct) =>
        ValueTask.FromResult(Guid.NewGuid());
}
