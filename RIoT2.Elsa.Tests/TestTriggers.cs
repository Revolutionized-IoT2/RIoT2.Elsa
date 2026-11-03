using Elsa.Workflows.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace RIoT2.Elsa.Tests;

/// <summary>Builds <see cref="RIoTTriggers"/> around a stimulus sender, the way the hosts register it.</summary>
internal static class TestTriggers
{
    public static RIoTTriggers Create(IStimulusSender sender, TriggerDeduplicator? deduplicator = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(sender);
        return new RIoTTriggers(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), deduplicator ?? new TriggerDeduplicator());
    }
}
