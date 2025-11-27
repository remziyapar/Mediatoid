using Mediatoid;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection()
    .AddMediatoid(typeof(Program).Assembly)
    .BuildServiceProvider();

var sender = services.GetRequiredService<ISender>();

// Request/Response
var orderId = await sender.Send(new CreateOrder("cust-1", 120m));
Console.WriteLine($"Order ID: {orderId}");

// Notification
await sender.Publish(new OrderCreated(orderId));

// Stream
await foreach (var number in sender.Stream(new ListNumbers(3)))
{
    Console.WriteLine($"Number: {number}");
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

/// <summary>Notification published after an order is created.</summary>
public sealed record OrderCreated(Guid OrderId) : INotification;

/// <summary>Handles <see cref="OrderCreated"/> notifications.</summary>
public sealed class OrderCreatedHandler : INotificationHandler<OrderCreated>
{
    /// <summary>Handles the order created notification.</summary>
    public ValueTask Handle(OrderCreated notification, CancellationToken ct) =>
        ValueTask.CompletedTask;
}

/// <summary>Stream request that yields numbers from 1 to <see cref="Count"/>.</summary>
public sealed record ListNumbers(int Count) : IStreamRequest<int>;

/// <summary>Handles <see cref="ListNumbers"/> requests.</summary>
public sealed class ListNumbersHandler : IStreamRequestHandler<ListNumbers, int>
{
    /// <summary>Enumerates numbers for the given <paramref name="request"/>.</summary>
    public async IAsyncEnumerable<int> Handle(ListNumbers request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        for (var i = 1; i <= request.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            yield return i;
            await Task.Yield();
        }
    }
}
