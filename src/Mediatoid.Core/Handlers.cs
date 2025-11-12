namespace Mediatoid;

/// <summary>
/// Bir <typeparamref name="TRequest"/> isteğini işleyip <typeparamref name="TResponse"/> yanıtı üretir.
/// </summary>
public interface IRequestHandler<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    /// <summary>İsteği işler.</summary>
    ValueTask<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Fire-and-forget bildirimleri (notification) işler.
/// </summary>
public interface INotificationHandler<TNotification> where TNotification : INotification
{
    /// <summary>Bildirim işleme mantığı.</summary>
    ValueTask Handle(TNotification notification, CancellationToken cancellationToken);
}

/// <summary>
/// Akış tabanlı istekleri (IStreamRequest) işleyip öğeleri döndürür.
/// </summary>
public interface IStreamRequestHandler<TRequest, TItem> where TRequest : IStreamRequest<TItem>
{
    /// <summary>İsteği işleyip akış sonuçlarını üretir.</summary>
    IAsyncEnumerable<TItem> Handle(TRequest request, CancellationToken cancellationToken);
}
