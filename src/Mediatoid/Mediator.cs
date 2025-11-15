using System.Reflection;
using System.Runtime.ExceptionServices;
using Mediatoid.Pipeline;

namespace Mediatoid;

internal sealed class Mediator(IServiceProvider sp) : ISender
{
    private readonly IServiceProvider _sp = sp;

    public async ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var handlerInterface = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
        var handler = _sp.GetService(handlerInterface)
            ?? throw new InvalidOperationException($"No handler registered for request type '{requestType.FullName}'.");

        var behaviorInterface = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResponse));
        var behaviors = ((IEnumerable<object>?)_sp.GetService(typeof(IEnumerable<>).MakeGenericType(behaviorInterface)))
                        ?? [];

        // Handler terminali
        RequestHandlerContinuation<TResponse> terminal = () =>
        {
            var method = handlerInterface.GetMethod("Handle")!;
            try
            {
                var vt = (ValueTask<TResponse>)method.Invoke(handler, [request, cancellationToken])!;
                return vt;
            }
            catch (TargetInvocationException tie) when (tie.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
                throw;
            }
        };

        foreach (var behavior in behaviors.Reverse())
        {
            var method = behaviorInterface.GetMethod("Handle")!;
            var next = terminal;
            terminal = () =>
            {
                try
                {
                    var vt = (ValueTask<TResponse>)method.Invoke(
                        behavior,
                        [request, next, cancellationToken])!;
                    return vt;
                }
                catch (TargetInvocationException tie) when (tie.InnerException is not null)
                {
                    ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
                    throw;
                }
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
        var handlers = (IEnumerable<object>?)_sp.GetService(enumerableType) ?? [];

        foreach (var handler in handlers)
        {
            var method = handlerInterface.GetMethod("Handle")!;
            try
            {
                var vt = (ValueTask)method.Invoke(handler, [notification, cancellationToken])!;
                await vt.ConfigureAwait(false);
            }
            catch (TargetInvocationException tie) when (tie.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
                throw;
            }
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
        try
        {
            var result = method.Invoke(handler, [request, cancellationToken])!;
            return (IAsyncEnumerable<TItem>)result;
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
            throw;
        }
    }
}
