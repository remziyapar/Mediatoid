using Mediatoid;
using Microsoft.Extensions.DependencyInjection;

namespace Mediatoid.SourceGen.Tests;

public class GeneratedSmokeTests
{
    [Fact]
    public void Generated_Types_Should_Exist_In_This_Assembly()
    {
        var thisAsm = typeof(GeneratedSmokeTests).Assembly;
        var generatedNsTypes = thisAsm
            .GetTypes()
            .Where(t => t.Namespace == "Mediatoid.Generated")
            .ToArray();

        Assert.NotEmpty(generatedNsTypes);
        Assert.Contains(generatedNsTypes, t => t.Name == "MediatoidGeneratedRegistry");
        var pipelineTypes = generatedNsTypes.Where(t => t.Name.StartsWith("Pipeline_", StringComparison.Ordinal)).ToArray();
        Assert.Contains(pipelineTypes, t => t.Name.Contains("Greet") && t.Name.Contains("string"));
    }

    [Fact]
    public async Task Send_Should_Invoke_Generated_And_Unrolled_Pipeline()
    {
        BehaviorLogStore.Clear<Greet, string>("A");
        BehaviorLogStore.Clear<Greet, string>("B");
        GreetHandler.Reset();

        var sp = Di.Build();
        var sender = sp.GetRequiredService<ISender>();

        var res = await sender.Send(new Greet("Ada"));

        Assert.Equal("Hello Ada", res);

        var aLogs = BehaviorLogStore.Get<Greet, string>("A");
        var bLogs = BehaviorLogStore.Get<Greet, string>("B");

        // En az bir before/after çifti beklenir; diagnostik zenginleştirme (GEN/RT + correlation) nedeniyle
        // ek bilgi içerebilir, bu yüzden prefix üzerinden doğrula.
        Assert.True(aLogs.Count >= 2, "A behavior logları en az before/after içermeli.");
        Assert.StartsWith("A:before", aLogs[0]);
        Assert.Contains(aLogs, x => x.StartsWith("A:after", StringComparison.Ordinal));

        Assert.True(bLogs.Count >= 2, "B behavior logları en az before/after içermeli.");
        Assert.StartsWith("B:before", bLogs[0]);
        Assert.Contains(bLogs, x => x.StartsWith("B:after", StringComparison.Ordinal));
    }
}
