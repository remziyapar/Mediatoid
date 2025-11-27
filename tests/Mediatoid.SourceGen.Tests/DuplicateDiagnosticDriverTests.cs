#if DUPLICATE_TEST
using System.Collections.Generic;
using System.Reflection;
using Mediatoid;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Mediatoid.SourceGen.Tests;

public sealed record DupReq(int X) : IRequest<string>;
public sealed class DupHandler1 : IRequestHandler<DupReq, string>
{ public ValueTask<string> Handle(DupReq r, CancellationToken ct) => ValueTask.FromResult("h1"); }
public sealed class DupHandler2 : IRequestHandler<DupReq, string>
{ public ValueTask<string> Handle(DupReq r, CancellationToken ct) => ValueTask.FromResult("h2"); }

public class DuplicateDiagnosticDriverTests
{
    [Fact]
    public void MDT002_Reported_For_Duplicate()
    {
        var src = @"
using Mediatoid;
using System.Threading;
using System.Threading.Tasks;
public sealed record DupReq(int X) : Mediatoid.IRequest<string>;
public sealed class DupHandler1 : Mediatoid.IRequestHandler<DupReq,string> { public ValueTask<string> Handle(DupReq r, CancellationToken ct)=>ValueTask.FromResult(""h1""); }
public sealed class DupHandler2 : Mediatoid.IRequestHandler<DupReq,string> { public ValueTask<string> Handle(DupReq r, CancellationToken ct)=>ValueTask.FromResult(""h2""); }
[assembly: Mediatoid.MediatoidRoot]";

        var tree = CSharpSyntaxTree.ParseText(src);
        var refs = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ValueTask).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IRequest<>).Assembly.Location),
        };
        var compilation = CSharpCompilation.Create("DupTest",
            new[] { tree },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var gen = new Mediatoid.SourceGen.MediatoidGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(gen);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out var diags);

        var runResult = driver.GetRunResult();
        Assert.Contains(runResult.Diagnostics, d => d.Id == "MDT002");
    }
}
#endif
