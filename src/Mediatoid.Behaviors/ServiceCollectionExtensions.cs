using Mediatoid.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Mediatoid.Behaviors;

/// <summary>
/// DI helper extensions for registering optional cross-cutting behaviors.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Logging and Validation behaviors as open generic
    /// <see cref="IPipelineBehavior{TRequest, TResponse}"/> with transient
    /// lifetime. If FluentValidation is not available, the Validation behavior
    /// becomes a no-op.
    /// </summary>
    public static IServiceCollection AddMediatoidBehaviors(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
