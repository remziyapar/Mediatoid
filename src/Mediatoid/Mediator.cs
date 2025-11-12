using System.Reflection;
using Mediatoid.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Mediatoid;

internal sealed class Mediator : ISender
{
    private readonly IServiceProvider _sp;

    public Mediator(IServiceProvider sp) => _sp = sp;

    public async ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var handlerInterface = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
        var handler = _sp.GetService(handlerInterface)
            ?? throw new InvalidOperationException($"No handler registered for request type '{requestType.FullName}'.");

        var behaviorInterface = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResponse));
        var behaviors = ((IEnumerable<object>?)_sp.GetService(typeof(IEnumerable<>).MakeGenericType(behaviorInterface)))
                        ?? Array.Empty<object>();

        // Handler.Invoke
        RequestHandlerContinuation<TResponse> terminal = () =>
        {
            var method = handlerInterface.GetMethod("Handle")!;
            var vt = (ValueTask<TResponse>)method.Invoke(handler, new object[] { request, cancellationToken })!;
            return vt;
        };

        foreach (var behavior in behaviors.Reverse())
        {
            var method = behaviorInterface.GetMethod("Handle")!;
            var next = terminal;
            terminal = () =>
            {
                var vt = (ValueTask<TResponse>)method.Invoke(
                    behavior,
                    new object[] { request, next, cancellationToken })!;
                return vt;
            };
        }

        return await terminal().ConfigureAwait(false);
    }

    public async ValueTask Publish(INotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var notificationType = notification.GetType();
        var handlerInterface = typeof(INotificationHandler<>).MakeGenericType(notificationType);
        var enumerableType = typeof(IEnumerable<>).MakeGenericType(handlerInterface);
        var handlers = (IEnumerable<object>?)_sp.GetService(enumerableType) ?? Array.Empty<object>();

        foreach (var handler in handlers)
        {
            var method = handlerInterface.GetMethod("Handle")!;
            var vt = (ValueTask)method.Invoke(handler, new object[] { notification, cancellationToken })!;
            await vt.ConfigureAwait(false);
        }
    }

    public IAsyncEnumerable<TItem> Stream<TItem>(IStreamRequest<TItem> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var handlerInterface = typeof(IStreamRequestHandler<,>).MakeGenericType(requestType, typeof(TItem));
        var handler = _sp.GetService(handlerInterface)
            ?? throw new InvalidOperationException($"No stream handler registered for request type '{requestType.FullName}'.");

        var method = handlerInterface.GetMethod("Handle")!;
        var result = method.Invoke(handler, new object[] { request, cancellationToken })!;
        return (IAsyncEnumerable<TItem>)result;
    }
}
