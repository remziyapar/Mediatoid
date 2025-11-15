using System.Reflection;
using System.Runtime.ExceptionServices;
using Mediatoid.Pipeline;

namespace Mediatoid;

internal sealed class Mediator(IServiceProvider sp) : ISender
{
    private readonly IServiceProvider _sp = sp;

    // Reflection cache (MethodInfo lookup’larını tekilleştir)
    private static class ReflectionCache
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Type, string), MethodInfo> Cache = new();

        public static MethodInfo Get(Type type, string methodName)
            => Cache.GetOrAdd((type, methodName), static key =>
            {
                var (t, name) = key;
                var mi = t.GetMethod(name);
                return mi ?? throw new MissingMethodException(t.FullName, name);
            });
    }

    public async ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var handlerInterface = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
        var handler = _sp.GetService(handlerInterface)
            ?? throw new InvalidOperationException($"No handler registered for request type '{requestType.FullName}'.");

        var behaviorInterface = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResponse));
        var behaviorsEnumerable = (IEnumerable<object>?)_sp.GetService(typeof(IEnumerable<>).MakeGenericType(behaviorInterface)) ?? Array.Empty<object>();
        var behaviors = behaviorsEnumerable as object[] ?? behaviorsEnumerable.ToArray();

        // Handler terminali (cache’lenmiş MethodInfo ile)
        RequestHandlerContinuation<TResponse> terminal = () =>
        {
            var method = ReflectionCache.Get(handlerInterface, nameof(IRequestHandler<IRequest<TResponse>, TResponse>.Handle));
            try
            {
                var vt = (ValueTask<TResponse>)method.Invoke(handler, new object[] { request, cancellationToken })!;
                return vt;
            }
            catch (TargetInvocationException tie) when (tie.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
                throw;
            }
        };

        // Behavior yoksa kısa yol
        if (behaviors.Length == 0)
            return await terminal().ConfigureAwait(false);

        // Compose: sondan başa sar (LINQ Reverse yok)
        var behaviorMethod = ReflectionCache.Get(behaviorInterface, nameof(IPipelineBehavior<IRequest<TResponse>, TResponse>.Handle));
        for (int i = behaviors.Length - 1; i >= 0; i--)
        {
            var current = behaviors[i];
            var next = terminal;
            terminal = () =>
            {
                try
                {
                    var vt = (ValueTask<TResponse>)behaviorMethod.Invoke(current, new object[] { request, next, cancellationToken })!;
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
        var handlers = (IEnumerable<object>?)_sp.GetService(enumerableType) ?? Array.Empty<object>();

        var method = ReflectionCache.Get(handlerInterface, nameof(INotificationHandler<INotification>.Handle));

        foreach (var handler in handlers)
        {
            try
            {
                var vt = (ValueTask)method.Invoke(handler, new object[] { notification, cancellationToken })!;
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

        var method = ReflectionCache.Get(handlerInterface, nameof(IStreamRequestHandler<IStreamRequest<TItem>, TItem>.Handle));
        try
        {
            var result = method.Invoke(handler, new object[] { request, cancellationToken })!;
            return (IAsyncEnumerable<TItem>)result;
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(tie.InnerException).Throw();
            throw;
        }
    }
}
