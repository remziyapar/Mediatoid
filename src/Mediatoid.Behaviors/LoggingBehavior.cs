using Mediatoid.Pipeline;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Mediatoid.Behaviors;

/// <summary>
/// İstek işlemeyi süre ve hata açısından basitçe loglayan behavior (debug seviyesinde).
/// ILogger yoksa sessiz çalışır.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger? _logger;

    /// <summary>ILogger sağlamayan tüketiciler için sessiz varsayılan kurucu.</summary>
    public LoggingBehavior() { }

    /// <summary>Logger mevcutsa DI tarafından seçilecek kurucu.</summary>
    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    /// <summary>İsteği devam ettirmeden önce/sonra loglar ve süreyi ölçer (ILogger varsa).</summary>
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
