namespace Mediatoid;

/// <summary>
/// Handles a <typeparamref name="TRequest"/> and produces a
/// <typeparamref name="TResponse"/>.
/// </summary>
public interface IRequestHandler<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    /// <summary>Handles the request.</summary>
    ValueTask<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Handles fire-and-forget notifications.
/// </summary>
public interface INotificationHandler<TNotification> where TNotification : INotification
{
    /// <summary>Notification handling logic.</summary>
    ValueTask Handle(TNotification notification, CancellationToken cancellationToken);
}

/// <summary>
/// Handles stream-based requests (<see cref="IStreamRequest{TItem}"/>) and
/// yields items.
/// </summary>
public interface IStreamRequestHandler<TRequest, TItem> where TRequest : IStreamRequest<TItem>
{
    /// <summary>Handles the request and produces a result stream.</summary>
    IAsyncEnumerable<TItem> Handle(TRequest request, CancellationToken cancellationToken);
}
