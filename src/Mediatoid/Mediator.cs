using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Mediatoid.Pipeline;

namespace Mediatoid;

internal sealed class Mediator(IServiceProvider sp) : ISender
{
    private readonly IServiceProvider _sp = sp;

    // MethodInfo cache
    private static class ReflectionCache
    {
        private static readonly ConcurrentDictionary<(Type, string), MethodInfo> Cache = new();

        public static MethodInfo Get(Type type, string methodName)
            => Cache.GetOrAdd((type, methodName), static key =>
            {
                var (t, name) = key;
                var mi = t.GetMethod(name);
                return mi ?? throw new MissingMethodException(t.FullName, name);
            });
    }

    // Request handler delegate cache (per closed interface)
    private static class HandlerInvokerCache<TResponse>
    {
        private static readonly ConcurrentDictionary<Type, Func<object, IRequest<TResponse>, CancellationToken, ValueTask<TResponse>>> Cache = new();

        public static Func<object, IRequest<TResponse>, CancellationToken, ValueTask<TResponse>> Get(Type handlerInterface, Type requestType)
            => Cache.GetOrAdd(handlerInterface, _ => Build(handlerInterface, requestType));

        private static Func<object, IRequest<TResponse>, CancellationToken, ValueTask<TResponse>> Build(Type handlerInterface, Type requestType)
        {
            var mi = ReflectionCache.Get(handlerInterface, nameof(IRequestHandler<IRequest<TResponse>, TResponse>.Handle));

            var handlerParam = Expression.Parameter(typeof(object), "handler");
            var requestParam = Expression.Parameter(typeof(IRequest<TResponse>), "request");
            var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

            var castHandler = Expression.Convert(handlerParam, handlerInterface);
            var castRequest = Expression.Convert(requestParam, requestType);

            var call = Expression.Call(castHandler, mi, castRequest, ctParam);
            var lambda = Expression.Lambda<Func<object, IRequest<TResponse>, CancellationToken, ValueTask<TResponse>>>(
                call, handlerParam, requestParam, ctParam);

            return lambda.Compile();
        }
    }

    // Pipeline behavior delegate cache
    private static class BehaviorInvokerCache<TResponse>
    {
        private static readonly ConcurrentDictionary<Type, Func<object, IRequest<TResponse>, RequestHandlerContinuation<TResponse>, CancellationToken, ValueTask<TResponse>>> Cache = new();

        public static Func<object, IRequest<TResponse>, RequestHandlerContinuation<TResponse>, CancellationToken, ValueTask<TResponse>> Get(Type behaviorInterface, Type requestType)
            => Cache.GetOrAdd(behaviorInterface, _ => Build(behaviorInterface, requestType));

        private static Func<object, IRequest<TResponse>, RequestHandlerContinuation<TResponse>, CancellationToken, ValueTask<TResponse>> Build(Type behaviorInterface, Type requestType)
        {
            var mi = ReflectionCache.Get(behaviorInterface, nameof(IPipelineBehavior<IRequest<TResponse>, TResponse>.Handle));

            var behaviorParam = Expression.Parameter(typeof(object), "behavior");
            var requestParam = Expression.Parameter(typeof(IRequest<TResponse>), "request");
            var contParam = Expression.Parameter(typeof(RequestHandlerContinuation<TResponse>), "continuation");
            var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

            var castBehavior = Expression.Convert(behaviorParam, behaviorInterface);
            var castRequest = Expression.Convert(requestParam, requestType);

            var call = Expression.Call(castBehavior, mi, castRequest, contParam, ctParam);
            var lambda = Expression.Lambda<Func<object, IRequest<TResponse>, RequestHandlerContinuation<TResponse>, CancellationToken, ValueTask<TResponse>>>(
                call, behaviorParam, requestParam, contParam, ctParam);

            return lambda.Compile();
        }
    }

    // Notification handler delegate cache
    private static class NotificationInvokerCache
    {
        private static readonly ConcurrentDictionary<Type, Func<object, INotification, CancellationToken, ValueTask>> Cache = new();

        public static Func<object, INotification, CancellationToken, ValueTask> Get(Type handlerInterface, Type notificationType)
            => Cache.GetOrAdd(handlerInterface, _ => Build(handlerInterface, notificationType));

        private static Func<object, INotification, CancellationToken, ValueTask> Build(Type handlerInterface, Type notificationType)
        {
            var mi = ReflectionCache.Get(handlerInterface, nameof(INotificationHandler<INotification>.Handle));

            var handlerParam = Expression.Parameter(typeof(object), "handler");
            var notifParam = Expression.Parameter(typeof(INotification), "notification");
            var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

            var castHandler = Expression.Convert(handlerParam, handlerInterface);
            var castNotif = Expression.Convert(notifParam, notificationType);

            var call = Expression.Call(castHandler, mi, castNotif, ctParam);
            var lambda = Expression.Lambda<Func<object, INotification, CancellationToken, ValueTask>>(call, handlerParam, notifParam, ctParam);

            return lambda.Compile();
        }
    }

    // Stream handler delegate cache
    private static class StreamInvokerCache<TItem>
    {
        private static readonly ConcurrentDictionary<Type, Func<object, IStreamRequest<TItem>, CancellationToken, IAsyncEnumerable<TItem>>> Cache = new();

        public static Func<object, IStreamRequest<TItem>, CancellationToken, IAsyncEnumerable<TItem>> Get(Type handlerInterface, Type requestType)
            => Cache.GetOrAdd(handlerInterface, _ => Build(handlerInterface, requestType));

        private static Func<object, IStreamRequest<TItem>, CancellationToken, IAsyncEnumerable<TItem>> Build(Type handlerInterface, Type requestType)
        {
            var mi = ReflectionCache.Get(handlerInterface, nameof(IStreamRequestHandler<IStreamRequest<TItem>, TItem>.Handle));

            var handlerParam = Expression.Parameter(typeof(object), "handler");
            var requestParam = Expression.Parameter(typeof(IStreamRequest<TItem>), "request");
            var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

            var castHandler = Expression.Convert(handlerParam, handlerInterface);
            var castRequest = Expression.Convert(requestParam, requestType);

            var call = Expression.Call(castHandler, mi, castRequest, ctParam);
            var lambda = Expression.Lambda<Func<object, IStreamRequest<TItem>, CancellationToken, IAsyncEnumerable<TItem>>>(
                call, handlerParam, requestParam, ctParam);

            return lambda.Compile();
        }
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

        var handlerInvoker = HandlerInvokerCache<TResponse>.Get(handlerInterface, requestType);

        RequestHandlerContinuation<TResponse> terminal = () => handlerInvoker(handler, request, cancellationToken);

        if (behaviors.Length == 0)
            return await terminal().ConfigureAwait(false);

        var behaviorInvoker = BehaviorInvokerCache<TResponse>.Get(behaviorInterface, requestType);

        for (int i = behaviors.Length - 1; i >= 0; i--)
        {
            var current = behaviors[i];
            var next = terminal;
            terminal = () => behaviorInvoker(current, request, next, cancellationToken);
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

        var invoker = NotificationInvokerCache.Get(handlerInterface, notificationType);

        foreach (var handler in handlers)
            await invoker(handler, notification, cancellationToken).ConfigureAwait(false);
    }

    public IAsyncEnumerable<TItem> Stream<TItem>(IStreamRequest<TItem> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var handlerInterface = typeof(IStreamRequestHandler<,>).MakeGenericType(requestType, typeof(TItem));
        var handler = _sp.GetService(handlerInterface)
            ?? throw new InvalidOperationException($"No stream handler registered for request type '{requestType.FullName}'.");

        var invoker = StreamInvokerCache<TItem>.Get(handlerInterface, requestType);
        return invoker(handler, request, cancellationToken);
    }
}
