using System.Reflection;
using Mediatoid.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Mediatoid;

/// <summary>
/// Mediatoid runtime kayıtlarını ve verilen derlemelerde (assemblies) handler/pipeline taramasını sağlayan DI uzantıları.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Mediatoid runtime'ını kaydeder ve verilen derlemelerdeki <see cref="IRequestHandler{TRequest,TResponse}"/>,
    /// <see cref="INotificationHandler{TNotification}"/>, <see cref="IStreamRequestHandler{TRequest,TItem}"/> ve
    /// <see cref="IPipelineBehavior{TRequest,TResponse}"/> implementasyonlarını otomatik olarak DI konteynerine ekler.
    /// </summary>
    /// <param name="services">DI servis koleksiyonu.</param>
    /// <param name="assemblies">Handler ve pipeline davranışlarının aranacağı derlemeler.</param>
    /// <returns>Girdi olarak verilen <paramref name="services"/> örneği.</returns>
    /// <exception cref="ArgumentException">Hiç derleme verilmediğinde fırlatılır.</exception>
    public static IServiceCollection AddMediatoid(this IServiceCollection services, params Assembly[] assemblies)
    {
        if (assemblies is null || assemblies.Length == 0)
            throw new ArgumentException("At least one assembly must be provided for handler scanning.", nameof(assemblies));

        services.AddScoped<ISender, Mediator>();

        // Deterministic scan order: by FullName
        foreach (var asm in assemblies)
        {
            var types = asm.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .OrderBy(t => t.FullName, StringComparer.Ordinal);

            foreach (var type in types)
            {
                foreach (var i in type.GetInterfaces())
                {
                    if (!i.IsGenericType) continue;
                    var def = i.GetGenericTypeDefinition();

                    // Open-generic behavior registration support
                    if (def == typeof(IPipelineBehavior<,>))
                    {
                        if (type.IsGenericTypeDefinition)
                            services.AddTransient(typeof(IPipelineBehavior<,>), type);
                        else
                            services.AddTransient(i, type);

                        continue;
                    }

                    // Handlers (usually closed generic)
                    if (def == typeof(IRequestHandler<,>) ||
                        def == typeof(INotificationHandler<>) ||
                        def == typeof(IStreamRequestHandler<,>))
                    {
                        if (type.IsGenericTypeDefinition)
                            services.AddTransient(def, type);
                        else
                            services.AddTransient(i, type);
                    }
                }
            }
        }

        return services;
    }
}
