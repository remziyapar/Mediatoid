using System.Collections.Generic;
using System.Linq;
using Mediatoid;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Mediatoid.SourceGen.Tests;

public class DuplicateHandlerDiagnosticTests
{
    [Fact]
    public void DuplicateHandlers_Should_Emit_MDT002()
    {
        const string src = @"
using Mediatoid;
using System;
[assembly: MediatoidRoot]

public sealed record DpReq(string V) : IRequest<string>;

public sealed class H1 : IRequestHandler<DpReq, string>
{
    public ValueTask<string> Handle(DpReq r, System.Threading.CancellationToken ct) => ValueTask.FromResult(r.V);
}
public sealed class H2 : IRequestHandler<DpReq, string>
{
    public ValueTask<string> Handle(DpReq r, System.Threading.CancellationToken ct) => ValueTask.FromResult(r.V);
}
";

        var tree = CSharpSyntaxTree.ParseText(src);
        var refs = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IRequest<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(MediatoidGenerator).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create("DupTest",
            new[] { tree },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var gen = new MediatoidGenerator();
        CSharpGeneratorDriver.Create(gen)
            .RunGeneratorsAndUpdateCompilation(compilation, out var updated, out var diagnostics);

        var allDiagnostics = diagnostics.Concat(updated.GetDiagnostics()).ToList();
        Assert.Contains(allDiagnostics, d => d.Id == "MDT002");
    }
}
