using System.Linq;
using Mediatoid;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Mediatoid.SourceGen.Tests;

public class MissingRootAttributeDiagnosticTests
{
    [Fact]
    public void NoRootAttribute_Should_Emit_MDT001_Info()
    {
        const string src = @"
using Mediatoid;
public sealed record Plain(string M) : IRequest<string>;
public sealed class PlainHandler : IRequestHandler<Plain,string>
{
    public ValueTask<string> Handle(Plain r, System.Threading.CancellationToken ct) => ValueTask.FromResult(r.M);
}
";

        var tree = CSharpSyntaxTree.ParseText(src);
        var refs = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IRequest<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(MediatoidGenerator).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create("NoRootTest",
            new[] { tree },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var gen = new MediatoidGenerator();
        CSharpGeneratorDriver.Create(gen)
            .RunGeneratorsAndUpdateCompilation(compilation, out var updated, out var diagnostics);

        var allDiagnostics = diagnostics.Concat(updated.GetDiagnostics()).ToList();
        Assert.Contains(allDiagnostics, d => d.Id == "MDT001");
    }
}
