namespace Mediatoid.Pipeline;

/// <summary>
/// Defines a cross-cutting behavior that can wrap the handling of a Send
/// request (for example logging, validation). Behaviors are composed
/// outer-to-inner based on registration order.
/// </summary>
/// <typeparam name="TRequest">Request type.</typeparam>
/// <typeparam name="TResponse">Response type.</typeparam>
public interface IPipelineBehavior<TRequest, TResponse>
    where TRequest : Mediatoid.IRequest<TResponse>
{
    /// <summary>
    /// Applies the behavior. Call <paramref name="continuation"/> to continue
    /// the pipeline.
    /// </summary>
    /// <param name="request">Request to handle.</param>
    /// <param name="continuation">Next step in the chain (next behavior or final handler).</param>
    /// <param name="cancellationToken">Cancellation token (always last).</param>
    ValueTask<TResponse> Handle(
        TRequest request,
        RequestHandlerContinuation<TResponse> continuation,
        CancellationToken cancellationToken);
}

/// <summary>
/// Represents the next terminal or intermediate step in the Send pipeline.
/// </summary>
/// <typeparam name="TResponse">Response type.</typeparam>
public delegate ValueTask<TResponse> RequestHandlerContinuation<TResponse>();

/// <summary>
/// Defines a cross-cutting behavior that can wrap the Publish (notification)
/// flow. Just like <see cref="IPipelineBehavior{TRequest,TResponse}"/>,
/// behaviors are composed outer-to-inner in deterministic order.
/// </summary>
/// <typeparam name="TNotification">Notification type.</typeparam>
public interface INotificationBehavior<TNotification>
    where TNotification : Mediatoid.INotification
{
    /// <summary>
    /// Applies the behavior. Call <paramref name="continuation"/> to continue
    /// the pipeline.
    /// </summary>
    /// <param name="notification">Notification to handle.</param>
    /// <param name="continuation">Next step in the chain (next behavior or remaining handler chain).</param>
    /// <param name="cancellationToken">Cancellation token (always last).</param>
    ValueTask Handle(
        TNotification notification,
        NotificationHandlerContinuation continuation,
        CancellationToken cancellationToken);
}

/// <summary>
/// Represents the next step in the Publish pipeline (remaining handler chain
/// or inner behavior).
/// </summary>
public delegate ValueTask NotificationHandlerContinuation();

/// <summary>
/// Defines a cross-cutting behavior for handling Stream (IAsyncEnumerable)
/// requests. Behaviors may wrap the sequence returned by the inner
/// continuation delegate.
/// </summary>
/// <typeparam name="TRequest">Request type.</typeparam>
/// <typeparam name="TItem">Item type in the stream.</typeparam>
public interface IStreamBehavior<TRequest, TItem>
    where TRequest : Mediatoid.IStreamRequest<TItem>
{
    /// <summary>
    /// Applies the behavior. Call <paramref name="continuation"/> to continue
    /// the pipeline.
    /// </summary>
    /// <param name="request">Request to handle.</param>
    /// <param name="continuation">Next step in the chain (next behavior or final handler stream).</param>
    /// <param name="cancellationToken">Cancellation token (always last).</param>
    IAsyncEnumerable<TItem> Handle(
        TRequest request,
        StreamHandlerContinuation<TItem> continuation,
        CancellationToken cancellationToken);
}

/// <summary>
/// Represents the next step in the Stream pipeline (remaining behavior chain
/// or handler stream).
/// </summary>
/// <typeparam name="TItem">Item type in the stream.</typeparam>
public delegate IAsyncEnumerable<TItem> StreamHandlerContinuation<TItem>();
