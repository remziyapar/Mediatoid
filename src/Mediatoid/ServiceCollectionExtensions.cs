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
    /// Registers Mediatoid runtime services and scans the provided assemblies for handler and pipeline implementations.
    /// </summary>
    /// <param name="services">The service collection to add Mediatoid services to.</param>
    /// <param name="assemblies">Assemblies to scan for handlers and pipeline behaviors.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddMediatoid(this IServiceCollection services, params Assembly[] assemblies)
    {
        if (assemblies is null || assemblies.Length == 0)
            throw new ArgumentException("At least one assembly must be provided for handler scanning.", nameof(assemblies));

        services.AddScoped<ISender, Mediator>();

        // Aynı assembly birden fazla verilmişse tekilleştir
        foreach (var asm in assemblies.Distinct())
        {
            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Yüklenebilen tiplerle devam et
                types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
            }

            // Deterministic scan order: by FullName
            foreach (var type in types
                         .Where(t => !t.IsAbstract && !t.IsInterface)
                         .OrderBy(t => t.FullName, StringComparer.Ordinal))
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
