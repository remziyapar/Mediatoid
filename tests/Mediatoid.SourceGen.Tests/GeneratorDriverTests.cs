using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using Xunit;

namespace Mediatoid.SourceGen.Tests;

/// <summary>
/// MediatoidGenerator'ın Roslyn GeneratorDriver ile birim testleri.
/// Üretilen kaynak kodunu ve diagnostic'leri doğrular.
/// </summary>
public class GeneratorDriverTests
{
    private static GeneratorDriverRunResult RunGenerator(string source, bool addRootAttribute = true)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Concat(new[]
            {
                MetadataReference.CreateFromFile(typeof(IRequest<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IRequestHandler<,>).Assembly.Location)
            });

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        if (addRootAttribute)
        {
            var attrSyntax = CSharpSyntaxTree.ParseText("[assembly: Mediatoid.MediatoidRoot]");
            compilation = compilation.AddSyntaxTrees(attrSyntax);
        }

        var generator = new MediatoidGenerator();

        // DÜZELTME: CSharpGeneratorDriver.Create kullan, sonra RunGenerators çağır
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        return driver.GetRunResult();
    }

    [Fact]
    public void Generator_Should_Produce_Registry_Without_Root_Attribute()
    {
        var source = @"
using Mediatoid;
namespace Test;
public record TestRequest(string Value) : IRequest<string>;
public class TestHandler : IRequestHandler<TestRequest, string>
{
    public ValueTask<string> Handle(TestRequest request, CancellationToken ct)
        => ValueTask.FromResult(request.Value);
}";

        var result = RunGenerator(source, addRootAttribute: false);

        // Registry üretilmeli
        Assert.Contains(result.GeneratedTrees, t => t.FilePath.Contains("MediatoidGeneratedRegistry"));

        // MDT001 (MissingRootAttribute) diagnostic'i üretilmeli
        var diagnostics = result.Diagnostics;
        Assert.Contains(diagnostics, d => d.Id == "MDT001" && d.Severity == DiagnosticSeverity.Info);
    }

    [Fact]
    public void Generator_Should_Generate_Pipeline_With_Root_Attribute()
    {
        var source = @"
using Mediatoid;
namespace Test;
public record TestRequest(string Value) : IRequest<string>;
public class TestHandler : IRequestHandler<TestRequest, string>
{
    public ValueTask<string> Handle(TestRequest request, CancellationToken ct)
        => ValueTask.FromResult(request.Value);
}";

        var result = RunGenerator(source, addRootAttribute: true);

        var generatedCode = result.GeneratedTrees.Single().ToString();

        // Pipeline sınıfı üretilmiş olmalı
        Assert.Contains("internal static class Pipeline_", generatedCode);
        Assert.Contains("internal static ValueTask<", generatedCode);
        Assert.Contains("Invoke(", generatedCode);

        // Dispatch sınıfı üretilmiş olmalı
        Assert.Contains("internal static class Dispatch_", generatedCode);
        Assert.Contains("Dictionary<Type, Func<", generatedCode);
    }

    [Fact]
    public void Generator_Should_Detect_Duplicate_Handlers()
    {
        var source = @"
using Mediatoid;
namespace Test;
public record TestRequest(string Value) : IRequest<string>;
public class TestHandler1 : IRequestHandler<TestRequest, string>
{
    public ValueTask<string> Handle(TestRequest request, CancellationToken ct)
        => ValueTask.FromResult(request.Value);
}
public class TestHandler2 : IRequestHandler<TestRequest, string>
{
    public ValueTask<string> Handle(TestRequest request, CancellationToken ct)
        => ValueTask.FromResult(request.Value + ""2"");
}";

        var result = RunGenerator(source);

        // MDT002 (DuplicateHandler) diagnostic'i üretilmeli
        var diagnostics = result.Diagnostics;
        Assert.Contains(diagnostics, d => d.Id == "MDT002" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Generator_Should_Detect_Invalid_Partially_Closed_Behavior()
    {
        var source = @"
using Mediatoid;
using Mediatoid.Pipeline;
namespace Test;
public record TestRequest(string Value) : IRequest<string>;
public class PartialBehavior<TReq> : IPipelineBehavior<TReq, string> where TReq : IRequest<string>
{
    public ValueTask<string> Handle(TReq r, RequestHandlerContinuation<string> c, CancellationToken ct)
        => c();
}";

        var result = RunGenerator(source, addRootAttribute: false);

        // MDT003 (InvalidBehaviorArity) diagnostic'i üretilmeli
        var diagnostics = result.Diagnostics;
        Assert.Contains(diagnostics, d => d.Id == "MDT003" && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Generator_Should_Accept_Open_Generic_Behavior()
    {
        var source = @"
using Mediatoid;
using Mediatoid.Pipeline;
namespace Test;
public record TestRequest(string Value) : IRequest<string>;
public class TestHandler : IRequestHandler<TestRequest, string>
{
    public ValueTask<string> Handle(TestRequest request, CancellationToken ct)
        => ValueTask.FromResult(request.Value);
}
public class OpenBehavior<TReq, TRes> : IPipelineBehavior<TReq, TRes> where TReq : IRequest<TRes>
{
    public ValueTask<TRes> Handle(TReq r, RequestHandlerContinuation<TRes> c, CancellationToken ct)
        => c();
}";

        var result = RunGenerator(source);

        var generatedCode = result.GeneratedTrees.Single().ToString();

        // Behavior pipeline'a dahil edilmiş olmalı
        Assert.Contains("OpenBehavior", generatedCode);

        // MDT003 diagnostic'i OLMAMALI
        var diagnostics = result.Diagnostics;
        Assert.DoesNotContain(diagnostics, d => d.Id == "MDT003");
    }

    [Fact]
    public void Generator_Should_Handle_Closed_Generic_Behavior()
    {
        var source = @"
using Mediatoid;
using Mediatoid.Pipeline;
namespace Test;
public record SpecificRequest(string Value) : IRequest<string>;
public class SpecificHandler : IRequestHandler<SpecificRequest, string>
{
    public ValueTask<string> Handle(SpecificRequest request, CancellationToken ct)
        => ValueTask.FromResult(request.Value);
}
public class ClosedBehavior : IPipelineBehavior<SpecificRequest, string>
{
    public ValueTask<string> Handle(SpecificRequest r, RequestHandlerContinuation<string> c, CancellationToken ct)
        => c();
}";

        var result = RunGenerator(source);

        var generatedCode = result.GeneratedTrees.Single().ToString();

        // Closed behavior pipeline'a dahil edilmiş olmalı
        Assert.Contains("ClosedBehavior", generatedCode);
    }

    [Fact]
    public void Generator_Should_Generate_Sanitized_Class_Names()
    {
        var source = @"
using Mediatoid;
namespace Test.Sub.Namespace;
public record Complex__Request<T>(T Value) : IRequest<string> where T : struct;
public class ComplexHandler : IRequestHandler<Complex__Request<int>, string>
{
    public ValueTask<string> Handle(Complex__Request<int> request, CancellationToken ct)
        => ValueTask.FromResult(request.Value.ToString());
}";

        var result = RunGenerator(source);

        var generatedCode = result.GeneratedTrees.Single().ToString();

        // Sanitized isimler olmalı (özel karakterler _ ile değiştirilmiş)
        Assert.Contains("Pipeline_", generatedCode);
        Assert.DoesNotContain("Pipeline_<", generatedCode); // < karakteri sanitize edilmeli
        Assert.DoesNotContain("Pipeline_.", generatedCode); // . karakteri sanitize edilmeli
    }

    [Fact]
    public void Generator_Should_Handle_Multiple_Behaviors_In_Correct_Order()
    {
        var source = @"
using Mediatoid;
using Mediatoid.Pipeline;
namespace Test;
public record TestRequest(string Value) : IRequest<string>;
public class TestHandler : IRequestHandler<TestRequest, string>
{
    public ValueTask<string> Handle(TestRequest request, CancellationToken ct)
        => ValueTask.FromResult(request.Value);
}
public class BehaviorA<TReq, TRes> : IPipelineBehavior<TReq, TRes> where TReq : IRequest<TRes>
{
    public ValueTask<TRes> Handle(TReq r, RequestHandlerContinuation<TRes> c, CancellationToken ct) => c();
}
public class BehaviorB<TReq, TRes> : IPipelineBehavior<TReq, TRes> where TReq : IRequest<TRes>
{
    public ValueTask<TRes> Handle(TReq r, RequestHandlerContinuation<TRes> c, CancellationToken ct) => c();
}";

        var result = RunGenerator(source);

        var generatedCode = result.GeneratedTrees.Single().ToString();

        // İki behavior da pipeline'da olmalı
        Assert.Contains("BehaviorA", generatedCode);
        Assert.Contains("BehaviorB", generatedCode);

        // Alfabetik sırada olmalı (FullName'e göre sort)
        var indexA = generatedCode.IndexOf("BehaviorA", StringComparison.Ordinal);
        var indexB = generatedCode.IndexOf("BehaviorB", StringComparison.Ordinal);
        Assert.True(indexA < indexB, "BehaviorA should appear before BehaviorB in generated code");
    }

    [Fact]
    public void Generator_Should_Not_Generate_Pipeline_For_Notification_Handlers()
    {
        var source = @"
using Mediatoid;
namespace Test;
public record TestNotification(string Message) : INotification;
public class TestNotificationHandler : INotificationHandler<TestNotification>
{
    public ValueTask Handle(TestNotification notification, CancellationToken ct)
        => ValueTask.CompletedTask;
}";

        var result = RunGenerator(source);

        var generatedCode = result.GeneratedTrees.Single().ToString();

        // Registry'de kayıt olmalı
        Assert.Contains("INotificationHandler", generatedCode);

        // Ama Pipeline_ sınıfı olmamalı (notification'lar için pipeline yok)
        Assert.DoesNotContain("internal static class Pipeline_", generatedCode);
    }

    [Fact]
    public void Generator_Should_Not_Generate_Pipeline_For_Stream_Handlers()
    {
        var source = @"
using Mediatoid;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
namespace Test;
public record TestStreamRequest(int Count) : IStreamRequest<int>;
public class TestStreamHandler : IStreamRequestHandler<TestStreamRequest, int>
{
    public async IAsyncEnumerable<int> Handle(TestStreamRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        for (int i = 0; i < request.Count; i++)
            yield return i;
    }
}";

        var result = RunGenerator(source);

        var generatedCode = result.GeneratedTrees.Single().ToString();

        // Registry'de kayıt olmalı
        Assert.Contains("IStreamRequestHandler", generatedCode);

        // Ama Pipeline_ sınıfı olmamalı (stream'ler için pipeline yok)
        Assert.DoesNotContain("internal static class Pipeline_", generatedCode);
    }
}
