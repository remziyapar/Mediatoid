using Mediatoid.Pipeline;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Mediatoid.Behaviors;

/// <summary>
/// Simple debug-level behavior that logs request processing duration and
/// errors. Runs silently when no <see cref="ILogger"/> is available.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger? _logger;

    /// <summary>Silent default constructor for consumers that do not provide an ILogger.</summary>
    public LoggingBehavior() { }

    /// <summary>Constructor selected by DI when a logger is available.</summary>
    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    /// <summary>Logs before/after executing the request and measures duration when a logger is available.</summary>
    public async ValueTask<TResponse> Handle(
        TRequest request,
        RequestHandlerContinuation<TResponse> continuation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(continuation);

        if (_logger is null)
            return await continuation().ConfigureAwait(false);

        var name = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();
        try
        {
            _logger.LogDebug("Handling {Request}", name);
            var res = await continuation().ConfigureAwait(false);
            sw.Stop();
            _logger.LogDebug("Handled {Request} in {Elapsed}ms", name, sw.Elapsed.TotalMilliseconds);
            return res;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Error handling {Request} after {Elapsed}ms", name, sw.Elapsed.TotalMilliseconds);
            throw;
        }
    }
}
