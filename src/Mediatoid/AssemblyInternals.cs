using System.Runtime.CompilerServices;

// Test ve benchmark projelerinin internal tiplere (MediatoidDiagnostics) erişebilmesi için.
[assembly: InternalsVisibleTo("Mediatoid.SourceGen.Tests")]
[assembly: InternalsVisibleTo("Mediatoid.Tests")]
[assembly: InternalsVisibleTo("Mediatoid.Core.Tests")]
[assembly: InternalsVisibleTo("Mediatoid.Behaviors.Tests")]
[assembly: InternalsVisibleTo("Mediatoid.Benchmarks")]
