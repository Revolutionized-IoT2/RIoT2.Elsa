using System.Diagnostics.Metrics;
using Elsa.Workflows;
using Elsa.Workflows.Notifications;
using Elsa.Workflows.Runtime;
using Elsa.Workflows.Runtime.Results;
using Elsa.Workflows.State;
using Microsoft.Extensions.Logging.Abstractions;
using RIoT2.Core.Diagnostics;
using RIoT2.Elsa.Server.RIoT.Grpc;
using RIoT2.Elsa.Server.RIoT.Services;

namespace RIoT2.Elsa.Tests;

/// <summary>Elsa's instruments (design 7.4 phase 4, I22); the names are in docs/contracts/metrics.md.</summary>
[TestClass]
[DoNotParallelize]
public class ElsaMetricsTests
{
    [TestMethod]
    public void InstrumentsHaveTheDocumentedNames()
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(ElsaMetrics).TypeHandle);
        var names = new List<string>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, _) =>
            {
                if (instrument.Meter.Name == MeterNames.Elsa)
                    names.Add(instrument.Name);
            }
        };
        listener.Start();

        CollectionAssert.AreEquivalent(new[] { "riot2.workflow.triggers", "riot2.workflow.faults" }, names);
    }

    [TestMethod]
    public async Task TriggersAreCountedAsReceivedOrDuplicate()
    {
        var tags = Record(ElsaMetrics.TriggersName);
        var service = new RIoTTriggerGrpcService(TestTriggers.Create(new Sender(), new TriggerDeduplicator()), NullLogger<RIoTTriggerGrpcService>.Instance);
        var request = new TriggerRequest { Id = "report", MessageId = "m1" };

        await service.Trigger(request, null!);
        await service.Trigger(request, null!);

        CollectionAssert.AreEquivalent(new[] { "result=received,transport=grpc", "result=duplicate,transport=grpc" }, tags.Values);
        tags.Dispose();
    }

    [TestMethod]
    public async Task OnlyFaultedExecutionsCountAsFaults()
    {
        var tags = Record(ElsaMetrics.FaultsName);
        var handler = new WorkflowFaultMetrics();

        await handler.HandleAsync(new WorkflowExecuted(null!, new WorkflowState { DefinitionId = "ok", SubStatus = WorkflowSubStatus.Finished }, null!), default);
        await handler.HandleAsync(new WorkflowExecuted(null!, new WorkflowState { DefinitionId = "broken", SubStatus = WorkflowSubStatus.Faulted }, null!), default);

        CollectionAssert.AreEqual(new[] { "definition=broken" }, tags.Values);
        tags.Dispose();
    }

    private static Recording Record(string instrument)
    {
        var recording = new Recording();
        recording.Listener.InstrumentPublished = (published, listener) =>
        {
            if (published.Name == instrument)
                listener.EnableMeasurementEvents(published);
        };
        recording.Listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            recording.Values.Add(string.Join(",", tags.ToArray().OrderBy(t => t.Key).Select(t => $"{t.Key}={t.Value}"))));
        recording.Listener.Start();
        return recording;
    }

    private sealed class Recording : IDisposable
    {
        public MeterListener Listener { get; } = new();
        public List<string> Values { get; } = [];
        public void Dispose() => Listener.Dispose();
    }

    private sealed class Sender : IStimulusSender
    {
        public Task<SendStimulusResult> SendAsync(string activityTypeName, object stimulus, StimulusMetadata? metadata = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SendStimulusResult([]));

        public Task<SendStimulusResult> SendAsync(string stimulusHash, StimulusMetadata? metadata = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
