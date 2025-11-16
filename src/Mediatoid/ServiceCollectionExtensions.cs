using System.Reflection;
using Mediatoid.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Mediatoid;

/// <summary>
/// Mediatoid runtime kayıtlarını ve verilen derlemelerde (assemblies) handler/pipeline taramasını sağlayan DI uzantıları.
/// Source generator çıktısı varsa, handler kayıtları jeneratörden alınır; değilse reflection taraması yapılır.
/// Pipeline behaviors her zaman deterministik tarama ile kaydedilir.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Mediatoid runtime servislerini kaydeder ve verilen derlemelerde handler/pipeline implementasyonlarını tarar.
    /// Source generator çıktısı mevcutsa handler kayıtları oradan yapılır, aksi halde reflection kullanılır.
    /// </summary>
    /// <param name="services">Mediatoid servislerinin kaydedileceği koleksiyon.</param>
    /// <param name="assemblies">Handler ve pipeline davranış implementasyonlarının taranacağı derlemeler.</param>
    /// <returns>Güncellenmiş servis koleksiyonu.</returns>
    /// <exception cref="ArgumentException">Hiç assembly verilmemişse fırlatılır.</exception>
    public static IServiceCollection AddMediatoid(this IServiceCollection services, params Assembly[] assemblies)
    {
        if (assemblies is null || assemblies.Length == 0)
            throw new ArgumentException("At least one assembly must be provided for handler scanning.", nameof(assemblies));

        services.AddScoped<ISender, Mediator>();

        // 1) SourceGen registry varsa handler kayıtlarını buradan yap
        var generatedMaps = TryRegisterGeneratedHandlers(services);

        // 2) Reflection taraması: sadece behaviors (deterministik sıra) + gerektiğinde (registry yoksa) handler'lar
        foreach (var asm in assemblies.Distinct())
        {
            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
            }

            foreach (var type in types.Where(t => !t.IsAbstract && !t.IsInterface)
                                      .OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                foreach (var i in type.GetInterfaces())
                {
                    if (!i.IsGenericType) continue;
                    var def = i.GetGenericTypeDefinition();

                    // Behaviors: her zaman deterministik sıralı kayıt
                    if (def == typeof(IPipelineBehavior<,>))
                    {
                        if (type.IsGenericTypeDefinition)
                            services.AddTransient(typeof(IPipelineBehavior<,>), type);
                        else
                            services.AddTransient(i, type);

                        continue;
                    }

                    // Handlers: eğer SourceGen kayıtları varsa, duplicate ekleme; yoksa reflection ile ekle
                    if (def == typeof(IRequestHandler<,>) ||
                        def == typeof(INotificationHandler<>) ||
                        def == typeof(IStreamRequestHandler<,>))
                    {
                        if (generatedMaps is not null)
                        {
                            // Generated haritasında zaten varsa atla, yoksa ekle (nadir senaryolarda kapsama dışı olabilir)
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

    // Generated registry varsa: Maps alanını okuyup DI'a ekler, ayrıca duplicate kontrolü için set döndürür.
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

                services.AddTransient(service, impl);
                _ = set.Add((service, impl));
            }

            return set;
        }
        catch
        {
            // Herhangi bir sebeple okunamazsa sessizce fallback yap
            return null;
        }
    }
}
