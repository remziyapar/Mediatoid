namespace Mediatoid;

/// <summary>
/// Marks a root assembly for build-time pipeline unrolling/optimization.
/// Optionally accepts marker types that define an explicit assembly order
/// manifest. Marker types should reflect the assembly parameter order used
/// in <c>AddMediatoid</c>. If no markers are provided, a single-assembly
/// scenario is assumed.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class MediatoidRootAttribute : Attribute
{
    /// <summary>Marker types used in the assembly order manifest.</summary>
    public Type[] AssemblyMarkers { get; }

    /// <summary>
    /// When used without an order manifest, only handlers/behaviors from this
    /// assembly are unrolled.
    /// </summary>
    public MediatoidRootAttribute()
    {
        AssemblyMarkers = Array.Empty<Type>();
    }

    /// <summary>
    /// Marker types that correspond to the <c>AddMediatoid</c> parameter order
    /// for multi-assembly scenarios.
    /// </summary>
    /// <param name="assemblyMarkers">Order manifest marker types (each is a simple class defined in the target assembly).</param>
    public MediatoidRootAttribute(params Type[] assemblyMarkers)
    {
        AssemblyMarkers = assemblyMarkers ?? Array.Empty<Type>();
    }
}
