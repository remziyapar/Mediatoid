using System.Reflection;
using Mediatoid.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Mediatoid;

/// <summary>
/// DI extensions that register Mediatoid runtime services and perform
/// handler/pipeline scanning in the given assemblies. When source generator
/// output is available, handler registrations are taken from the generator;
/// otherwise reflection-based scanning is used. Pipeline behaviors are always
/// registered using deterministic scanning.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Mediatoid runtime services and scans the provided assemblies
    /// for handler/pipeline implementations. If source generator output is
    /// present, handler registrations are taken from there; otherwise
    /// reflection is used.
    /// </summary>
    public static IServiceCollection AddMediatoid(this IServiceCollection services, params Assembly[] assemblies)
    {
        if (assemblies is null || assemblies.Length == 0)
            throw new ArgumentException("At least one assembly must be provided for handler scanning.", nameof(assemblies));

        services.AddScoped<ISender, Mediator>();

        // SourceGen registry varsa handler kayıtlarını buradan yap (duplicate kontrolü için set döner)
        var generatedMaps = TryRegisterGeneratedHandlers(services);

        // Reflection taraması: behaviors (her zaman) + gerekirse (registry yoksa veya eksikse) handler'lar
        foreach (var asm in assemblies.Distinct())
        {
            Type[] types;
            try { types = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray(); }

            foreach (var type in types.Where(t => !t.IsAbstract && !t.IsInterface)
                                      .OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                foreach (var i in type.GetInterfaces())
                {
                    if (!i.IsGenericType) continue;
                    var def = i.GetGenericTypeDefinition();

                    // Behaviors: interface mapping + concrete (generator concrete çözer)
                    if (def == typeof(IPipelineBehavior<,>))
                    {
                        if (type.IsGenericTypeDefinition)
                        {
                            services.AddTransient(typeof(IPipelineBehavior<,>), type);
                            services.AddTransient(type); // open generic concrete
                        }
                        else
                        {
                            services.AddTransient(i, type);
                            services.AddTransient(type); // closed concrete
                        }
                        continue;
                    }

                    if (def == typeof(INotificationBehavior<>))
                    {
                        if (type.IsGenericTypeDefinition)
                        {
                            services.AddTransient(typeof(INotificationBehavior<>), type);
                            services.AddTransient(type); // open generic concrete
                        }
                        else
                        {
                            services.AddTransient(i, type);
                            services.AddTransient(type); // closed concrete
                        }
                        continue;
                    }

                    if (def == typeof(IStreamBehavior<,>))
                    {
                        if (type.IsGenericTypeDefinition)
                        {
                            services.AddTransient(typeof(IStreamBehavior<,>), type);
                            services.AddTransient(type); // open generic concrete
                        }
                        else
                        {
                            services.AddTransient(i, type);
                            services.AddTransient(type); // closed concrete
                        }
                        continue;
                    }

                    // Handlers: SourceGen varsa duplicate engelle; concrete yalnızca generated path'te eklenmiştir
                    if (def == typeof(IRequestHandler<,>) ||
                        def == typeof(INotificationHandler<>) ||
                        def == typeof(IStreamRequestHandler<,>))
                    {
                        if (generatedMaps is not null)
                        {
                            // Generated haritasında yoksa interface mapping ekle
                            if (!generatedMaps.Contains((i, type)))
                            {
                                if (type.IsGenericTypeDefinition)
                                    services.AddTransient(def, type);
                                else
                                    services.AddTransient(i, type);
                            }
                        }
                        else
                        {
                            // Reflection path: yalnız interface mapping; concrete ekleme yok
                            if (type.IsGenericTypeDefinition)
                                services.AddTransient(def, type);
                            else
                                services.AddTransient(i, type);
                        }
                    }
                }
            }
        }

        return services;
    }

    /// <summary>
    /// When a generated registry is present, reads its <c>Maps</c> field and
    /// adds entries to DI, returning a set used for duplicate detection. The
    /// concrete handler type is only registered here (generated pipeline
    /// invokers resolve concrete handlers).
    /// </summary>
    private static HashSet<(Type Service, Type Implementation)>? TryRegisterGeneratedHandlers(IServiceCollection services)
    {
        try
        {
            var regType = AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(a => a.GetType("Mediatoid.Generated.MediatoidGeneratedRegistry", throwOnError: false, ignoreCase: false))
                .FirstOrDefault(t => t is not null);
            if (regType is null)
                return null;

            var mapsField = regType.GetField("Maps", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (mapsField is null)
                return null;

            var array = mapsField.GetValue(null) as Array;
            if (array is null || array.Length == 0)
                return [];

            var set = new HashSet<(Type, Type)>();
            foreach (var item in array)
            {
                if (item is null) continue;
                var t = item.GetType();
                var service = (Type)t.GetField("Service")!.GetValue(item)!;
                var impl = (Type)t.GetField("Implementation")!.GetValue(item)!;

                // Interface → implementation
                services.AddTransient(service, impl);
                // Concrete handler (yalnız generated path'te)
                services.AddTransient(impl);

                _ = set.Add((service, impl));
            }

            return set;
        }
        catch
        {
            return null;
        }
    }

    private static bool ServiceExists(IServiceCollection services, Type concrete)
        => services.Any(d => d.ServiceType == concrete);
}
