using System.Text.Json;
using Elsa.Workflows.Runtime;
using Elsa.Workflows.Runtime.Results;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using RIoT2.Elsa.Server.RIoT.Grpc;
using RIoT2.Elsa.Server.RIoT.Services;

namespace RIoT2.Elsa.Tests;

/// <summary>
/// The orchestrator's outbox delivers workflow triggers at least once, with the outbox row id as
/// <c>message_id</c> (design 7.1). Elsa runs each id once.
/// </summary>
[TestClass]
public class TriggerDeduplicationTests
{
    [TestMethod]
    public async Task DuplicateMessageIdDoesNotRunTheWorkflowAgain()
    {
        var sender = new RecordingStimulusSender();
        var service = new RIoTTriggerGrpcService(sender, new TriggerDeduplicator(), NullLogger<RIoTTriggerGrpcService>.Instance);
        var request = new TriggerRequest { Id = "report", Data = """{"value":1}""", MessageId = "m1" };

        Assert.IsTrue((await service.Trigger(request, null!)).Success);
        Assert.IsTrue((await service.Trigger(request, null!)).Success);
        Assert.IsTrue((await service.Trigger(new TriggerRequest { Id = "report", Data = """{"value":1}""", MessageId = "m2" }, null!)).Success);

        Assert.AreEqual(2, sender.Stimuli.Count);
        Assert.AreEqual("report", sender.Stimuli[0].Stimulus);
        Assert.AreEqual(1, ((JsonElement)sender.Stimuli[0].Metadata!.Input!["EventData"]).GetProperty("value").GetInt32());
    }

    [TestMethod]
    public async Task TriggerWithoutAMessageIdAlwaysRuns()
    {
        var sender = new RecordingStimulusSender();
        var service = new RIoTTriggerGrpcService(sender, new TriggerDeduplicator(), NullLogger<RIoTTriggerGrpcService>.Instance);
        var request = new TriggerRequest { Id = "report" };

        await service.Trigger(request, null!);
        await service.Trigger(request, null!);

        Assert.AreEqual(2, sender.Stimuli.Count);
    }

    [TestMethod]
    public async Task InvalidDataIsRejectedAndItsIdIsNotRemembered()
    {
        var deduplicator = new TriggerDeduplicator();
        var service = new RIoTTriggerGrpcService(new RecordingStimulusSender(), deduplicator, NullLogger<RIoTTriggerGrpcService>.Instance);

        var error = await Assert.ThrowsExactlyAsync<RpcException>(() =>
            service.Trigger(new TriggerRequest { Id = "report", Data = "{", MessageId = "m1" }, null!));

        Assert.AreEqual(StatusCode.InvalidArgument, error.StatusCode);
        Assert.AreEqual(0, deduplicator.Count);
    }

    [TestMethod]
    public async Task RequestPastItsDeadlineIsRejectedWithoutRunning()
    {
        var sender = new RecordingStimulusSender();
        var deduplicator = new TriggerDeduplicator();
        var service = new RIoTTriggerGrpcService(sender, deduplicator, NullLogger<RIoTTriggerGrpcService>.Instance);

        var error = await Assert.ThrowsExactlyAsync<RpcException>(() =>
            service.Trigger(new TriggerRequest { Id = "report", MessageId = "m1" }, new DeadlineContext(DateTime.UtcNow.AddSeconds(-1))));

        Assert.AreEqual(StatusCode.DeadlineExceeded, error.StatusCode);
        Assert.IsEmpty(sender.Stimuli);
        Assert.AreEqual(0, deduplicator.Count);
        await service.Trigger(new TriggerRequest { Id = "report", MessageId = "m1" }, new DeadlineContext(DateTime.UtcNow.AddSeconds(5)));
        Assert.HasCount(1, sender.Stimuli);
    }

    [TestMethod]
    public async Task FailedRunIsForgottenSoItsRedeliveryRuns()
    {
        var deduplicator = new TriggerDeduplicator();
        var runs = 0;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => deduplicator.RunOnceAsync("m1", () =>
        {
            runs++;
            throw new InvalidOperationException("engine failed");
        }));
        Assert.IsTrue(await deduplicator.RunOnceAsync("m1", () => { runs++; return Task.CompletedTask; }));

        Assert.AreEqual(2, runs);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DuplicateArrivingDuringTheRunSharesItsOutcome(bool fail)
    {
        var deduplicator = new TriggerDeduplicator();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runs = 0;
        var first = deduplicator.RunOnceAsync("m1", async () =>
        {
            runs++;
            await release.Task;
            if (fail)
                throw new InvalidOperationException("engine failed");
        });
        var duplicate = deduplicator.RunOnceAsync("m1", () => { runs++; return Task.CompletedTask; });
        Assert.IsFalse(duplicate.IsCompleted, "A duplicate must wait for the run in progress.");

        release.SetResult();

        if (fail)
        {
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => first);
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => duplicate);
        }
        else
        {
            Assert.IsTrue(await first);
            Assert.IsFalse(await duplicate);
        }
        Assert.AreEqual(1, runs);
    }

    [TestMethod]
    public async Task IdIsForgottenAfterTenMinutes()
    {
        var time = new ManualTimeProvider();
        var deduplicator = new TriggerDeduplicator(time);
        var runs = 0;
        Task Run() { runs++; return Task.CompletedTask; }

        Assert.IsTrue(await deduplicator.RunOnceAsync("m1", Run));
        time.Advance(TriggerDeduplicator.Window - TimeSpan.FromSeconds(1));
        Assert.IsFalse(await deduplicator.RunOnceAsync("m1", Run));
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.IsTrue(await deduplicator.RunOnceAsync("m1", Run));
        Assert.AreEqual(2, runs);

        // Other ids are pruned once their window has passed.
        Assert.IsTrue(await deduplicator.RunOnceAsync("m2", Run));
        time.Advance(TriggerDeduplicator.Window + TimeSpan.FromMinutes(1));
        Assert.IsTrue(await deduplicator.RunOnceAsync("m3", Run));
        Assert.AreEqual(1, deduplicator.Count);
    }

    private sealed class DeadlineContext(DateTime deadline) : ServerCallContext
    {
        protected override string MethodCore => "/riot.RIoTTriggerService/Trigger";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "ipv4:127.0.0.1:1";
        protected override DateTime DeadlineCore => deadline;
        protected override Metadata RequestHeadersCore => [];
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override Metadata ResponseTrailersCore => [];
        protected override Status StatusCore { get; set; }
        protected override WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore => new(null, []);
        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) => throw new NotSupportedException();
        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class RecordingStimulusSender : IStimulusSender
    {
        public List<(string ActivityTypeName, object Stimulus, StimulusMetadata? Metadata)> Stimuli { get; } = [];

        public Task<SendStimulusResult> SendAsync(string activityTypeName, object stimulus, StimulusMetadata? metadata = null, CancellationToken cancellationToken = default)
        {
            Stimuli.Add((activityTypeName, stimulus, metadata));
            return Task.FromResult(new SendStimulusResult([]));
        }

        public Task<SendStimulusResult> SendAsync(string stimulusHash, StimulusMetadata? metadata = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
