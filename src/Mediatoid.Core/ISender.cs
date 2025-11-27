namespace Mediatoid;

/// <summary>
/// Single entry point for sending requests, publishing notifications and
/// producing streams.
/// </summary>
public interface ISender
{
    /// <summary>Handles a request/response and returns the response.</summary>
    ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>Publishes a notification (invokes all matching handlers).</summary>
    ValueTask Publish(INotification notification, CancellationToken cancellationToken = default);

    /// <summary>Handles a stream request and yields items.</summary>
    IAsyncEnumerable<TItem> Stream<TItem>(IStreamRequest<TItem> request, CancellationToken cancellationToken = default);
}
