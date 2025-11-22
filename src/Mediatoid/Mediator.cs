using System.Collections.Concurrent;
using System.Reflection;
using Mediatoid.Pipeline;

namespace Mediatoid;

internal sealed class Mediator(IServiceProvider sp) : ISender
{
    private readonly IServiceProvider _sp = sp;

    // Request handler delegate cache (per closed interface)
    private static class HandlerInvokerCache<TResponse>
    {
        private static readonly ConcurrentDictionary<Type, Func<object, IRequest<TResponse>, CancellationToken, ValueTask<TResponse>>> Cache = new();
        private static readonly MethodInfo HandlerBuildTyped = typeof(HandlerInvokerCache<TResponse>)
        .GetMethod(nameof(BuildTyped), BindingFlags.NonPublic | BindingFlags.Static)!;
        public static Func<object, IRequest<TResponse>, CancellationToken, ValueTask<TResponse>> Get(Type handlerInterface, Type requestType)
            => Cache.GetOrAdd(handlerInterface, _ => Build(requestType));

         private static Func<object, IRequest<TResponse>, CancellationToken, ValueTask<TResponse>> Build(Type requestType)
     {
         var closed = HandlerBuildTyped.MakeGenericMethod(requestType);
         return (Func<object, IRequest<TResponse>, CancellationToken, ValueTask<TResponse>>)closed.Invoke(null, Array.Empty<object>())!;
     }

        private static Func<object, IRequest<TResponse>, CancellationToken, ValueTask<TResponse>> BuildTyped<TRequest>()
            where TRequest : IRequest<TResponse>
            => static (handler, request, ct)
                => ((IRequestHandler<TRequest, TResponse>)handler).Handle((TRequest)request, ct);
    }

    // Pipeline behavior delegate cache
    private static class BehaviorInvokerCache<TResponse>
    {
        private static readonly ConcurrentDictionary<Type, Func<object, IRequest<TResponse>, RequestHandlerContinuation<TResponse>, CancellationToken, ValueTask<TResponse>>> Cache = new();
        private static readonly MethodInfo BehaviorBuildTyped = typeof(BehaviorInvokerCache<TResponse>).GetMethod(nameof(BuildTyped), BindingFlags.NonPublic | BindingFlags.Static)!;
        public static Func<object, IRequest<TResponse>, RequestHandlerContinuation<TResponse>, CancellationToken, ValueTask<TResponse>> Get(Type behaviorInterface, Type requestType)
            => Cache.GetOrAdd(behaviorInterface, _ => Build(requestType));

        private static Func<object, IRequest<TResponse>, RequestHandlerContinuation<TResponse>, CancellationToken, ValueTask<TResponse>> Build(Type requestType) 
        { 
            var closed = BehaviorBuildTyped.MakeGenericMethod(requestType); 
            return (Func<object, IRequest<TResponse>, RequestHandlerContinuation<TResponse>, CancellationToken, ValueTask<TResponse>>)closed.Invoke(null, Array.Empty<object>())!;
        }

        private static Func<object, IRequest<TResponse>, RequestHandlerContinuation<TResponse>, CancellationToken, ValueTask<TResponse>> BuildTyped<TRequest>()
            where TRequest : IRequest<TResponse>
            => static (behavior, request, continuation, ct)
                => ((IPipelineBehavior<TRequest, TResponse>)behavior).Handle((TRequest)request, continuation, ct);
    }

    // Notification handler delegate cache
    private static class NotificationInvokerCache
    {
        private static readonly ConcurrentDictionary<Type, Func<object, INotification, CancellationToken, ValueTask>> Cache = new();
        private static readonly MethodInfo NotificationBuildTyped = typeof(NotificationInvokerCache).GetMethod(nameof(BuildTyped), BindingFlags.NonPublic | BindingFlags.Static)!;
        public static Func<object, INotification, CancellationToken, ValueTask> Get(Type handlerInterface, Type notificationType)
            => Cache.GetOrAdd(handlerInterface, _ => Build(notificationType));

        private static Func<object, INotification, CancellationToken, ValueTask> Build(Type notificationType) 
        {
            var closed = NotificationBuildTyped.MakeGenericMethod(notificationType); 
            return (Func<object, INotification, CancellationToken, ValueTask>)closed.Invoke(null, Array.Empty<object>())!;
        }

        private static Func<object, INotification, CancellationToken, ValueTask> BuildTyped<TNotification>()
            where TNotification : INotification
            => static (handler, notification, ct)
                => ((INotificationHandler<TNotification>)handler).Handle((TNotification)notification, ct);
    }

    // Stream handler delegate cache
    private static class StreamInvokerCache<TItem>
    {
        private static readonly ConcurrentDictionary<Type, Func<object, IStreamRequest<TItem>, CancellationToken, IAsyncEnumerable<TItem>>> Cache = new();
        private static readonly MethodInfo StreamBuildTyped = typeof(StreamInvokerCache<TItem>).GetMethod(nameof(BuildTyped), BindingFlags.NonPublic | BindingFlags.Static)!;
        public static Func<object, IStreamRequest<TItem>, CancellationToken, IAsyncEnumerable<TItem>> Get(Type handlerInterface, Type requestType)
            => Cache.GetOrAdd(handlerInterface, _ => Build(requestType));

        private static Func<object, IStreamRequest<TItem>, CancellationToken, IAsyncEnumerable<TItem>> Build(Type requestType) 
        {
            var closed = StreamBuildTyped.MakeGenericMethod(requestType);
            return (Func<object, IStreamRequest<TItem>, CancellationToken, IAsyncEnumerable<TItem>>)closed.Invoke(null, Array.Empty<object>())!;
        }

        private static Func<object, IStreamRequest<TItem>, CancellationToken, IAsyncEnumerable<TItem>> BuildTyped<TRequest>()
            where TRequest : IStreamRequest<TItem>
            => static (handler, request, ct)
                => ((IStreamRequestHandler<TRequest, TItem>)handler).Handle((TRequest)request, ct);
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

        if (behaviors.Length == 0)
            return await handlerInvoker(handler, request, cancellationToken).ConfigureAwait(false);

        var behaviorInvoker = BehaviorInvokerCache<TResponse>.Get(behaviorInterface, requestType);

        if (behaviors.Length == 1)
            return await behaviorInvoker(
                behaviors[0],
                request,
                () => handlerInvoker(handler, request, cancellationToken),
                cancellationToken).ConfigureAwait(false);

        if (behaviors.Length == 2)
        {
            return await behaviorInvoker(
                behaviors[0],
                request,
                () => behaviorInvoker(
                    behaviors[1],
                    request,
                    () => handlerInvoker(handler, request, cancellationToken),
                    cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }
        // existing iterative path for >2
        int index = 0;
        ValueTask<TResponse> Next()
        {
            if (index >= behaviors.Length)
                return handlerInvoker(handler, request, cancellationToken);
            var current = behaviors[index++];
            return behaviorInvoker(current, request, Next, cancellationToken);
        }
        return await Next().ConfigureAwait(false);
    }

    public async ValueTask Publish(INotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var notificationType = notification.GetType();
        var handlerInterface = typeof(INotificationHandler<>).MakeGenericType(notificationType);
        var enumerableType = typeof(IEnumerable<>).MakeGenericType(handlerInterface);
        var handlersEnumerable = _sp.GetService(enumerableType) as IEnumerable<object>;
        if (handlersEnumerable is null) return;

        var handlers = handlersEnumerable as object[] ?? handlersEnumerable.ToArray();
        if (handlers.Length == 0) return;

        var invoker = NotificationInvokerCache.Get(handlerInterface, notificationType);

        for (int i = 0; i < handlers.Length; i++)
            await invoker(handlers[i], notification, cancellationToken).ConfigureAwait(false);
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
