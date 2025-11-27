using Mediatoid.Pipeline;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Mediatoid;

internal static class MediatoidDiagnostics
{
    internal record struct PipelineStep(
        Guid CorrelationId,
        string Path,                 // "GEN" | "RT"
        Type RequestType,
        Type ResponseType,
        Type? BehaviorType,          // null ise handler
        string Phase);               // "before" | "after" | "handler"

    // Abone olunabilir (test / benchmark). Null ise hiçbir maliyet yok (JIT inline conditional).
    internal static Action<PipelineStep>? OnStep;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Publish(Guid cid, string path, Type req, Type res, Type? beh, string phase)
    {
        var d = OnStep;
        if (d is not null)
            d(new PipelineStep(cid, path, req, res, beh, phase));
    }
}

/// <summary>
/// Runtime mediator that performs Send/Publish/Stream operations. When source
/// generator output is available, it first tries the static dispatch fast path
/// (<c>MediatoidGeneratedDispatch</c>); otherwise, it falls back to a
/// reflection-based composed pipeline.
/// </summary>
internal sealed class Mediator(IServiceProvider sp) : ISender
{
    private readonly IServiceProvider _sp = sp;

    private static class GeneratedDispatchCache
    {
        private static volatile bool _initialized;
        private static MethodInfo? _genericTryInvoke;
        private static readonly object _initLock = new();
        private static readonly ConcurrentDictionary<Type, object> _delegateCache = new();

        private static void EnsureInitialized()
        {
            // Fast exit: zaten başarılı şekilde initialize edildiyse tekrar uğraşma.
            if (_initialized && _genericTryInvoke is not null)
                return;

            lock (_initLock)
            {
                if (_initialized && _genericTryInvoke is not null)
                    return;

                // Şu anda yüklü assembly'ler arasında generated dispatch tipini ara.
                var dispatchType = AppDomain.CurrentDomain
                    .GetAssemblies()
                    .Where(a => a != typeof(Mediator).Assembly)
                    .Select(a => a.GetType("Mediatoid.Generated.MediatoidGeneratedDispatch", throwOnError: false, ignoreCase: false))
                    .FirstOrDefault(t => t is not null);

                // Tip henüz yüklenmemiş olabilir; bu durumda hiçbir bayrak latch'lenmez.
                // Sonraki TryInvoke çağrılarında tekrar denenecektir.
                if (dispatchType is null)
                    return;

                _genericTryInvoke = dispatchType.GetMethod(
                    "TryInvoke",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);

                // Yalnızca TryInvoke metodu başarıyla bulunduysa "initialized" kabul et.
                if (_genericTryInvoke is not null)
                    _initialized = true;
            }
        }

        internal static bool TryInvoke<TResponse>(
            IRequest<TResponse> request,
            IServiceProvider sp,
            CancellationToken ct,
            out ValueTask<TResponse> result)
        {
            EnsureInitialized();

            if (_genericTryInvoke is null)
            {
                result = default;
                return false;
            }

            var delObj = _delegateCache.GetOrAdd(typeof(TResponse), static _ =>
            {
                var closed = _genericTryInvoke.MakeGenericMethod(typeof(TResponse));
                return closed.CreateDelegate<Func<IRequest<TResponse>, IServiceProvider, CancellationToken, (bool, ValueTask<TResponse>)>>();
            });

            var del = (Func<IRequest<TResponse>, IServiceProvider, CancellationToken, (bool, ValueTask<TResponse>)>)delObj;
            var (ok, vt) = del(request, sp, ct);
            result = vt;
            return ok;
        }
    }

    private static class HandlerInvokerCache<TResponse>
    {
        private static readonly ConcurrentDictionary<(Type HandlerInterface, Type RequestType), Func<object, IRequest<TResponse>, CancellationToken, ValueTask<TResponse>>> Cache = new();
        private static readonly MethodInfo HandlerBuildTyped = typeof(HandlerInvokerCache<TResponse>)
            .GetMethod(nameof(BuildTyped), BindingFlags.NonPublic | BindingFlags.Static)!;

        public static Func<object, IRequest<TResponse>, CancellationToken, ValueTask<TResponse>> Get(Type handlerInterface, Type requestType)
            => Cache.GetOrAdd((handlerInterface, requestType), static k => Build(k.RequestType));

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

    private static class BehaviorInvokerCache<TResponse>
    {
        private static readonly ConcurrentDictionary<(Type BehaviorInterface, Type RequestType), Func<object, IRequest<TResponse>, RequestHandlerContinuation<TResponse>, CancellationToken, ValueTask<TResponse>>> Cache = new();
        private static readonly MethodInfo BehaviorBuildTyped = typeof(BehaviorInvokerCache<TResponse>)
            .GetMethod(nameof(BuildTyped), BindingFlags.NonPublic | BindingFlags.Static)!;

        public static Func<object, IRequest<TResponse>, RequestHandlerContinuation<TResponse>, CancellationToken, ValueTask<TResponse>> Get(Type behaviorInterface, Type requestType)
            => Cache.GetOrAdd((behaviorInterface, requestType), static k => Build(k.RequestType));

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

    private static class NotificationInvokerCache
    {
        private static readonly ConcurrentDictionary<Type, Func<object, INotification, CancellationToken, ValueTask>> Cache = new();
        private static readonly MethodInfo NotificationBuildTyped = typeof(NotificationInvokerCache)
            .GetMethod(nameof(BuildTyped), BindingFlags.NonPublic | BindingFlags.Static)!;

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

    private static class NotificationBehaviorInvokerCache
    {
        private static readonly ConcurrentDictionary<Type, Func<object, INotification, NotificationHandlerContinuation, CancellationToken, ValueTask>> Cache = new();
        private static readonly MethodInfo BehaviorBuildTyped = typeof(NotificationBehaviorInvokerCache)
            .GetMethod(nameof(BuildTyped), BindingFlags.NonPublic | BindingFlags.Static)!;

        public static Func<object, INotification, NotificationHandlerContinuation, CancellationToken, ValueTask> Get(Type behaviorInterface, Type notificationType)
            => Cache.GetOrAdd(behaviorInterface, _ => Build(notificationType));

        private static Func<object, INotification, NotificationHandlerContinuation, CancellationToken, ValueTask> Build(Type notificationType)
        {
            var closed = BehaviorBuildTyped.MakeGenericMethod(notificationType);
            return (Func<object, INotification, NotificationHandlerContinuation, CancellationToken, ValueTask>)closed.Invoke(null, Array.Empty<object>())!;
        }

        private static Func<object, INotification, NotificationHandlerContinuation, CancellationToken, ValueTask> BuildTyped<TNotification>()
            where TNotification : INotification
            => static (behavior, notification, continuation, ct)
                => ((INotificationBehavior<TNotification>)behavior).Handle((TNotification)notification, continuation, ct);
    }

    private static class StreamInvokerCache<TItem>
    {
        private static readonly ConcurrentDictionary<(Type HandlerInterface, Type RequestType), Func<object, IStreamRequest<TItem>, CancellationToken, IAsyncEnumerable<TItem>>> Cache = new();
        private static readonly MethodInfo StreamBuildTyped = typeof(StreamInvokerCache<TItem>)
            .GetMethod(nameof(BuildTyped), BindingFlags.NonPublic | BindingFlags.Static)!;

        public static Func<object, IStreamRequest<TItem>, CancellationToken, IAsyncEnumerable<TItem>> Get(Type handlerInterface, Type requestType)
            => Cache.GetOrAdd((handlerInterface, requestType), static k => Build(k.RequestType));

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

    private static class StreamBehaviorInvokerCache<TItem>
    {
        private static readonly ConcurrentDictionary<Type, Func<object, IStreamRequest<TItem>, StreamHandlerContinuation<TItem>, CancellationToken, IAsyncEnumerable<TItem>>> Cache = new();
        private static readonly MethodInfo BehaviorBuildTyped = typeof(StreamBehaviorInvokerCache<TItem>)
            .GetMethod(nameof(BuildTyped), BindingFlags.NonPublic | BindingFlags.Static)!;

        public static Func<object, IStreamRequest<TItem>, StreamHandlerContinuation<TItem>, CancellationToken, IAsyncEnumerable<TItem>> Get(Type behaviorInterface, Type requestType)
            => Cache.GetOrAdd(behaviorInterface, _ => Build(requestType));

        private static Func<object, IStreamRequest<TItem>, StreamHandlerContinuation<TItem>, CancellationToken, IAsyncEnumerable<TItem>> Build(Type requestType)
        {
            var closed = BehaviorBuildTyped.MakeGenericMethod(requestType);
            return (Func<object, IStreamRequest<TItem>, StreamHandlerContinuation<TItem>, CancellationToken, IAsyncEnumerable<TItem>>)closed.Invoke(null, Array.Empty<object>())!;
        }

        private static Func<object, IStreamRequest<TItem>, StreamHandlerContinuation<TItem>, CancellationToken, IAsyncEnumerable<TItem>> BuildTyped<TRequest>()
            where TRequest : IStreamRequest<TItem>
            => static (behavior, request, continuation, ct)
                => ((IStreamBehavior<TRequest, TItem>)behavior).Handle((TRequest)request, continuation, ct);
    }

    public async ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var cid = Guid.NewGuid();
        var requestType = request.GetType();
        var responseType = typeof(TResponse);

        // Fast-path
        if (GeneratedDispatchCache.TryInvoke(request, _sp, cancellationToken, out var genResult))
        {
            MediatoidDiagnostics.Publish(cid, "GEN", requestType, responseType, null, "before");
            var r = await genResult.ConfigureAwait(false);
            MediatoidDiagnostics.Publish(cid, "GEN", requestType, responseType, null, "after");
            return r;
        }

        // Runtime compose
        var handlerInterface = typeof(IRequestHandler<,>).MakeGenericType(requestType, responseType);
        var handler = _sp.GetService(handlerInterface)
            ?? throw new InvalidOperationException($"No handler registered for request type '{requestType.FullName}'.");

        var behaviorInterface = typeof(IPipelineBehavior<,>).MakeGenericType(requestType, responseType);
        var behaviorsEnumerable = (IEnumerable<object>?)_sp.GetService(typeof(IEnumerable<>).MakeGenericType(behaviorInterface)) ?? Array.Empty<object>();
        var behaviors = behaviorsEnumerable as object[] ?? behaviorsEnumerable.ToArray();

        // Concrete dedup
        if (behaviors.Length > 1)
        {
            var seenConcrete = new HashSet<Type>();
            var concreteUnique = new List<object>(behaviors.Length);
            foreach (var b in behaviors)
            {
                var t = b.GetType();
                if (seenConcrete.Add(t))
                    concreteUnique.Add(b);
            }
            behaviors = concreteUnique.ToArray();
        }

        // Generic definition dedup
        if (behaviors.Length > 1)
        {
            var seenGenDef = new HashSet<Type>();
            var genUnique = new List<object>(behaviors.Length);
            foreach (var b in behaviors)
            {
                var t = b.GetType();
                var key = t.IsGenericType ? t.GetGenericTypeDefinition() : t;
                if (seenGenDef.Add(key))
                    genUnique.Add(b);
            }
            behaviors = genUnique.ToArray();
        }

        var handlerInvoker = HandlerInvokerCache<TResponse>.Get(handlerInterface, requestType);

        if (behaviors.Length == 0)
        {
            MediatoidDiagnostics.Publish(cid, "RT", requestType, responseType, null, "before");
            var res0 = await handlerInvoker(handler, request, cancellationToken).ConfigureAwait(false);
            MediatoidDiagnostics.Publish(cid, "RT", requestType, responseType, null, "after");
            return res0;
        }

        var behaviorInvoker = BehaviorInvokerCache<TResponse>.Get(behaviorInterface, requestType);

        if (behaviors.Length == 1)
        {
            var behType = behaviors[0].GetType();
            MediatoidDiagnostics.Publish(cid, "RT", requestType, responseType, behType, "before");
            var res1 = await behaviorInvoker(
                behaviors[0],
                request,
                () =>
                {
                    MediatoidDiagnostics.Publish(cid, "RT", requestType, responseType, null, "handler");
                    return handlerInvoker(handler, request, cancellationToken);
                },
                cancellationToken).ConfigureAwait(false);
            MediatoidDiagnostics.Publish(cid, "RT", requestType, responseType, behType, "after");
            return res1;
        }

        if (behaviors.Length == 2)
        {
            var beh0 = behaviors[0].GetType();
            var beh1 = behaviors[1].GetType();

            MediatoidDiagnostics.Publish(cid, "RT", requestType, responseType, beh0, "before");
            var res2 = await behaviorInvoker(
                behaviors[0],
                request,
                () =>
                {
                    MediatoidDiagnostics.Publish(cid, "RT", requestType, responseType, beh1, "before");
                    return behaviorInvoker(
                        behaviors[1],
                        request,
                        () =>
                        {
                            MediatoidDiagnostics.Publish(cid, "RT", requestType, responseType, null, "handler");
                            return handlerInvoker(handler, request, cancellationToken);
                        },
                        cancellationToken);
                },
                cancellationToken).ConfigureAwait(false);
            MediatoidDiagnostics.Publish(cid, "RT", requestType, responseType, beh1, "after");
            MediatoidDiagnostics.Publish(cid, "RT", requestType, responseType, beh0, "after");
            return res2;
        }

        int index = 0;
        ValueTask<TResponse> Next()
        {
            if (index >= behaviors.Length)
            {
                MediatoidDiagnostics.Publish(cid, "RT", requestType, responseType, null, "handler");
                return handlerInvoker(handler, request, cancellationToken);
            }

            var current = behaviors[index++];
            var currentType = current.GetType();
            MediatoidDiagnostics.Publish(cid, "RT", requestType, responseType, currentType, "before");
            var vt = behaviorInvoker(current, request, Next, cancellationToken);

            // after yayınını continuation içinde yapmak için wrapper
            return AwaitAndMark(vt, cid, requestType, responseType, currentType);
        }

        static async ValueTask<TResponse> AwaitAndMark(
            ValueTask<TResponse> vt,
            Guid cid,
            Type req,
            Type res,
            Type behType)
        {
            var result = await vt.ConfigureAwait(false);
            MediatoidDiagnostics.Publish(cid, "RT", req, res, behType, "after");
            return result;
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

        // Behaviors
        var behaviorInterface = typeof(INotificationBehavior<>).MakeGenericType(notificationType);
        var behaviorsEnumerable = (IEnumerable<object>?)_sp.GetService(typeof(IEnumerable<>).MakeGenericType(behaviorInterface)) ?? Array.Empty<object>();
        var behaviors = behaviorsEnumerable as object[] ?? behaviorsEnumerable.ToArray();

        // Concrete dedup
        if (behaviors.Length > 1)
        {
            var seenConcrete = new HashSet<Type>();
            var concreteUnique = new List<object>(behaviors.Length);
            foreach (var b in behaviors)
            {
                var t = b.GetType();
                if (seenConcrete.Add(t))
                    concreteUnique.Add(b);
            }
            behaviors = concreteUnique.ToArray();
        }

        // Generic definition dedup
        if (behaviors.Length > 1)
        {
            var seenGenDef = new HashSet<Type>();
            var genUnique = new List<object>(behaviors.Length);
            foreach (var b in behaviors)
            {
                var t = b.GetType();
                var key = t.IsGenericType ? t.GetGenericTypeDefinition() : t;
                if (seenGenDef.Add(key))
                    genUnique.Add(b);
            }
            behaviors = genUnique.ToArray();
        }

        // 0 behavior: eski davranışı koru
        if (behaviors.Length == 0)
        {
            for (int i = 0; i < handlers.Length; i++)
                await invoker(handlers[i], notification, cancellationToken).ConfigureAwait(false);
            return;
        }

        // Tek continuation: tüm handler zincirini temsil eder
        NotificationHandlerContinuation TerminalContinuation = async () =>
        {
            for (int i = 0; i < handlers.Length; i++)
                await invoker(handlers[i], notification, cancellationToken).ConfigureAwait(false);
        };

        NotificationHandlerContinuation continuation = TerminalContinuation;

        var behaviorInvoker = NotificationBehaviorInvokerCache.Get(behaviorInterface, notificationType);

        // 1 ve 2 behavior için inline compose, daha fazlası için Next pattern'i
        if (behaviors.Length == 1)
        {
            await behaviorInvoker(behaviors[0], notification, continuation, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (behaviors.Length == 2)
        {
            // behaviors[1] içe, behaviors[0] dışa sarılır
            NotificationHandlerContinuation c1 = () => behaviorInvoker(behaviors[1], notification, continuation, cancellationToken);
            await behaviorInvoker(behaviors[0], notification, c1, cancellationToken).ConfigureAwait(false);
            return;
        }

        int index = 0;
        NotificationHandlerContinuation Next()
        {
            if (index >= behaviors.Length)
                return continuation;

            var current = behaviors[index++];
            return () => behaviorInvoker(current, notification, Next(), cancellationToken);
        }

        var root = Next();
        await root().ConfigureAwait(false);
    }

    public IAsyncEnumerable<TItem> Stream<TItem>(IStreamRequest<TItem> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var handlerInterface = typeof(IStreamRequestHandler<,>).MakeGenericType(requestType, typeof(TItem));
        var handler = _sp.GetService(handlerInterface)
            ?? throw new InvalidOperationException($"No stream handler registered for request type '{requestType.FullName}'.");

        var invoker = StreamInvokerCache<TItem>.Get(handlerInterface, requestType);

        // Behaviors
        var behaviorInterface = typeof(IStreamBehavior<,>).MakeGenericType(requestType, typeof(TItem));
        var behaviorsEnumerable = (IEnumerable<object>?)_sp.GetService(typeof(IEnumerable<>).MakeGenericType(behaviorInterface)) ?? Array.Empty<object>();
        var behaviors = behaviorsEnumerable as object[] ?? behaviorsEnumerable.ToArray();

        // Concrete dedup
        if (behaviors.Length > 1)
        {
            var seenConcrete = new HashSet<Type>();
            var concreteUnique = new List<object>(behaviors.Length);
            foreach (var b in behaviors)
            {
                var t = b.GetType();
                if (seenConcrete.Add(t))
                    concreteUnique.Add(b);
            }
            behaviors = concreteUnique.ToArray();
        }

        // Generic definition dedup
        if (behaviors.Length > 1)
        {
            var seenGenDef = new HashSet<Type>();
            var genUnique = new List<object>(behaviors.Length);
            foreach (var b in behaviors)
            {
                var t = b.GetType();
                var key = t.IsGenericType ? t.GetGenericTypeDefinition() : t;
                if (seenGenDef.Add(key))
                    genUnique.Add(b);
            }
            behaviors = genUnique.ToArray();
        }

        // 0 behavior: eski davranışı koru
        if (behaviors.Length == 0)
            return invoker(handler, request, cancellationToken);

        var behaviorInvoker = StreamBehaviorInvokerCache<TItem>.Get(behaviorInterface, requestType);

        // Terminal continuation: handler akışı
        StreamHandlerContinuation<TItem> terminal = () => invoker(handler, request, cancellationToken);

        // 1 ve 2 behavior için inline compose, daha fazlası için Next pattern'i
        if (behaviors.Length == 1)
            return behaviorInvoker(behaviors[0], request, terminal, cancellationToken);

        if (behaviors.Length == 2)
        {
            StreamHandlerContinuation<TItem> c1 = () => behaviorInvoker(behaviors[1], request, terminal, cancellationToken);
            return behaviorInvoker(behaviors[0], request, c1, cancellationToken);
        }

        int index = 0;
        StreamHandlerContinuation<TItem> Next()
        {
            if (index >= behaviors.Length)
                return terminal;

            var current = behaviors[index++];
            return () => behaviorInvoker(current, request, Next(), cancellationToken);
        }

        var root = Next();
        return root();
    }
}
