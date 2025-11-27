using System;

namespace Mediatoid.SourceGen;

/// <summary>
/// Marks an assembly as a root for build-time pipeline invoker generation.
/// If this attribute is not present, the generator only emits the handler
/// registration table (current behavior).
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class MediatoidRootAttribute : Attribute
{
}
