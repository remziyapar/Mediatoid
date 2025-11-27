namespace Mediatoid;

/// <summary>
/// Build-time pipeline unrolling / optimizasyonu için kök (root) assembly işaretçisi.
/// İsteğe bağlı olarak sıra manifest'i sağlamak amacıyla marker tipleri alınır.
/// Marker tipleri AddMediatoid çağrısındaki assembly parametre sırasını yansıtmalıdır.
/// Yoksa tek assembly varsayılır.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class MediatoidRootAttribute : Attribute
{
    /// <summary>Assembly sıra manifestinde kullanılan marker tipleri.</summary>
    public Type[] AssemblyMarkers { get; }

    /// <summary>
    /// Sıra manifesti olmadan kullanıldığında yalnızca bu assembly içindeki handler/behavior unrolling yapılır.
    /// </summary>
    public MediatoidRootAttribute()
    {
        AssemblyMarkers = Array.Empty<Type>();
    }

    /// <summary>
    /// Çoklu assembly sırası için AddMediatoid parametre sırasına karşılık gelen marker tipleri.
    /// </summary>
    /// <param name="assemblyMarkers">Sıra manifesti marker tipleri (her biri hedef assembly içinde tanımlı basit bir sınıf).</param>
    public MediatoidRootAttribute(params Type[] assemblyMarkers)
    {
        AssemblyMarkers = assemblyMarkers ?? Array.Empty<Type>();
    }
}
