using Mediatoid.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Mediatoid.Behaviors;

/// <summary>
/// Opsiyonel cross-cutting behavior kayıtları için DI yardımcı uzantılar.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Logging ve Validation behavior'larını open generic <see cref="IPipelineBehavior{TRequest, TResponse}"/> altında transient olarak ekler.
    /// FluentValidation yoksa Validation behavior no-op çalışır.
    /// </summary>
    public static IServiceCollection AddMediatoidBehaviors(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
