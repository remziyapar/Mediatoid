namespace Mediatoid;

/// <summary>
/// İstek göndermek, bildirim yayınlamak ve akış üretmek için tek giriş noktası.
/// </summary>
public interface ISender
{
    /// <summary>Request/Response işleyip yanıt döner.</summary>
    ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>Notification publish eder (tüm ilgili handler'ları çağırır).</summary>
    ValueTask Publish(INotification notification, CancellationToken cancellationToken = default);

    /// <summary>Stream isteğini işleyip öğeleri döndürür.</summary>
    IAsyncEnumerable<TItem> Stream<TItem>(IStreamRequest<TItem> request, CancellationToken cancellationToken = default);
}
