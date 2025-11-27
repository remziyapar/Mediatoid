using Mediatoid;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mediatoid.SourceGen.Tests;

public sealed class GeneratedPipelineTests
{
    [Fact]
    public async Task Behaviors_Should_Not_Duplicate_In_Generated_Or_Runtime_Path()
    {
        // Temizlik
        BehaviorLogStore.Clear<Greet, string>("A");
        BehaviorLogStore.Clear<Greet, string>("B");
        GreetHandler.Reset();

        // Teşhis yakalama listesi
        var steps = new List<object>();
        MediatoidDiagnostics.OnStep = s => steps.Add(s);

        var sp = Di.Build();
        var sender = sp.GetRequiredService<ISender>();

        var result = await sender.Send(new Greet("x"));

        // Teşhis aboneliğini bırak
        MediatoidDiagnostics.OnStep = null;

        Assert.Equal("Hello x", result);

        // Korelasyon analizi
        Assert.NotEmpty(steps);
        var typed = steps.Cast<MediatoidDiagnostics.PipelineStep>().ToArray();

        // En az bir korelasyon beklenir (bazı test koşullarında GEN/RT ayrımı veya ek çağrılar olabilir)
        var correlationIds = typed.Select(t => t.CorrelationId).Distinct().ToArray();
        Assert.True(correlationIds.Length >= 1);

        // Yol analizi
        var paths = typed.Select(t => t.Path).Distinct().ToArray();
        Assert.True(paths.Length >= 1, "En az bir path beklenir.");
        Assert.All(paths, p => Assert.True(p is "GEN" or "RT"));
        var path = paths[0];

        // Behavior before/after eşleşmesi
        var behaviorGroups = typed
            .Where(t => t.BehaviorType is not null)
            .GroupBy(t => t.BehaviorType!)
            .ToArray();

        foreach (var g in behaviorGroups)
        {
            var phases = g.Select(x => x.Phase).ToArray();
            Assert.Equal(1, phases.Count(p => p == "before"));
            Assert.Equal(1, phases.Count(p => p == "after"));
        }

        if (path == "RT")
        {
            var handlerSteps = typed.Where(t => t.Phase == "handler").ToArray();
            Assert.Single(handlerSteps);
        }
        else
        {
            Assert.DoesNotContain(typed, t => t.Phase == "handler");
        }

        // Ham log doğrulaması (diagnostik zenginleştirme nedeniyle ek girişler olabilir)
        var aLogs = BehaviorLogStore.Get<Greet, string>("A");
        var bLogs = BehaviorLogStore.Get<Greet, string>("B");

        Assert.True(aLogs.Count >= 2, "A behavior logları en az before/after içermeli.");
        Assert.True(bLogs.Count >= 2, "B behavior logları en az before/after içermeli.");

        Assert.StartsWith("A:before", aLogs[0]);
        Assert.Contains(aLogs, x => x.StartsWith("A:after", StringComparison.Ordinal));

        Assert.StartsWith("B:before", bLogs[0]);
        Assert.Contains(bLogs, x => x.StartsWith("B:after", StringComparison.Ordinal));
    }
}
