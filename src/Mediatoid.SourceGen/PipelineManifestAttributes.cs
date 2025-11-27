using System;

namespace Mediatoid.SourceGen;

/// <summary>
/// Pipeline invoker zincirinin derleme zamanında üretilmesini tetiklemek için kök assembly işaretleyicisi.
/// Bu attribute eklenmemişse generator yalnızca handler registration tablosunu üretir (mevcut davranış).
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class MediatoidRootAttribute : Attribute
{
}
