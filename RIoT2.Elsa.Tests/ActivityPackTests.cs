using System.Net;
using System.Net.Http;
using System.Text.Json;
using Elsa.Extensions;
using Elsa.Scheduling.Bookmarks;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Activities.Flowchart.Activities;
using Elsa.Workflows.Activities.Flowchart.Models;
using Elsa.Workflows.Memory;
using Elsa.Workflows.Models;
using Elsa.Workflows.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RIoT2.Core.Security;
using RIoT2.Elsa.Workflows.Activities;
using RIoT2.Elsa.Workflows.Automation;
using RIoT2.Elsa.Workflows.Models;
using RIoT2.Elsa.Server.RIoT.Services.Interfaces;
using RIoT2.Elsa.Workflows.Models;

namespace RIoT2.Elsa.Tests;

/// <summary>The automation activities of the activity pack (I48): compare, threshold, debounce, throttle, wait for state, notification and webhook.</summary>
[TestClass]
public class ActivityPackTests
{
    // ---------------------------------------------------------------- values

    [TestMethod]
    public void AReportIsReadAsItsValue()
    {
        var report = JsonSerializer.Deserialize<JsonElement>("""{"id":"door","value":true,"timeStamp":1}""");
        Assert.AreEqual(JsonValueKind.True, AutomationValues.Read(report).ValueKind);
        Assert.AreEqual(21.5, AutomationValues.ToNumber(AutomationValues.Read(new ElsaReport { Id = "t", TimeStamp = 1, Value = 21.5 })));
        Assert.AreEqual(JsonValueKind.Object, AutomationValues.Read(JsonSerializer.Deserialize<JsonElement>("""{"value":1,"unit":"C"}""")).ValueKind,
            "an entity with a value field but no id stays itself");
        Assert.AreEqual(JsonValueKind.Undefined, AutomationValues.Read(null).ValueKind);
    }

    [TestMethod]
    public void ComparisonsFollowTheValuesType()
    {
        static bool Compare(object? value, string op, string expected) =>
            AutomationValues.Compare(AutomationValues.Read(value), AutomationValues.ParseOperator(op), expected);

        Assert.IsTrue(Compare(true, "equals", "on"));
        Assert.IsFalse(Compare(true, "equals", "false"));
        Assert.IsTrue(Compare(21.5, "=", "21.50"));
        Assert.IsTrue(Compare("Open", "equals", "open"));
        Assert.IsTrue(Compare("open", "not equals", "closed"));
        Assert.IsTrue(Compare(25, "greater than", "22"));
        Assert.IsTrue(Compare(22, ">=", "22"));
        Assert.IsFalse(Compare(22, "less than", "22"));
        Assert.IsTrue(Compare("18", "atmost", "18"));
        Assert.IsFalse(Compare("warm", "greater than", "1"), "order comparisons need numbers");
        Assert.IsTrue(Compare(null, "equals", ""));
        Assert.IsFalse(Compare(null, "equals", "0"));
        Assert.ThrowsExactly<InvalidOperationException>(() => AutomationValues.ParseOperator("about"));
    }

    [TestMethod]
    public void HysteresisChangesStateOnlyOutsideTheBand()
    {
        var state = ThresholdState.Unknown;
        var outcomes = new List<string>();
        foreach (var value in new[] { 20.0, 25, 23, 21, 19, 17, 15, 22.5 })
        {
            (state, var outcome) = Hysteresis.Next(state, value, 18, 22);
            outcomes.Add(outcome);
        }
        CollectionAssert.AreEqual(new[] { "Unchanged", "Above", "Unchanged", "Unchanged", "Unchanged", "Below", "Unchanged", "Above" }, outcomes);
        Assert.AreEqual(Hysteresis.Unchanged, Hysteresis.Next(ThresholdState.High, 20, 20, 20).Outcome, "a value on a plain threshold doesn't flap");
    }

    [TestMethod]
    public void TheStoreDebouncesThrottlesAndStaysBounded()
    {
        var time = new ManualTime();
        var store = new AutomationStateStore(time);

        var first = store.BeginDebounce("k");
        var second = store.BeginDebounce("k");
        Assert.IsFalse(store.EndDebounce("k", first), "a later run supersedes the first");
        Assert.IsTrue(store.EndDebounce("k", second));
        Assert.IsTrue(store.EndDebounce("k", first), "after a restart (unknown key) a pending run passes");

        Assert.IsTrue(store.TryPass("t", TimeSpan.FromMinutes(1)));
        time.Advance(TimeSpan.FromSeconds(59));
        Assert.IsFalse(store.TryPass("t", TimeSpan.FromMinutes(1)));
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.IsTrue(store.TryPass("t", TimeSpan.FromMinutes(1)));

        for (var i = 0; i < AutomationStateStore.MaxKeys + 10; i++)
        {
            time.Advance(TimeSpan.FromMilliseconds(1));
            store.Threshold("key" + i, 30, 18, 22);
        }
        Assert.AreEqual(Hysteresis.Above, store.Threshold("key0", 30, 18, 22).Outcome, "the oldest keys were dropped");
        Assert.AreEqual(Hysteresis.Unchanged, store.Threshold($"key{AutomationStateStore.MaxKeys + 9}", 30, 18, 22).Outcome);
    }

    // ---------------------------------------------------------------- activities in a workflow

    [TestMethod]
    [DataRow("true", "True")]
    [DataRow("false", "False")]
    public async Task CompareFiltersTheTriggeringReport(string value, string expected)
    {
        await using var provider = Services();
        var compare = new RIoTCompare { Expected = new Input<string>("true") };
        var reached = new List<string>();

        await Run(provider, Flow(compare, reached, RIoTCompare.TrueOutcome, RIoTCompare.FalseOutcome), Report("motion", value));

        CollectionAssert.AreEqual(new[] { expected }, reached);
    }

    [TestMethod]
    public async Task ThresholdKeepsItsStateBetweenRuns()
    {
        await using var provider = Services();
        var outcomes = new List<string>();
        foreach (var value in new[] { "10", "20", "25", "21", "15" })
        {
            var threshold = new RIoTThreshold { Lower = new Input<double>(18d), Upper = new Input<double>(22d), Key = new Input<string?>("heater") };
            var reached = new List<string>();
            await Run(provider, Flow(threshold, reached, Hysteresis.Above, Hysteresis.Below, Hysteresis.Unchanged), Report("price", value));
            outcomes.AddRange(reached);
        }
        CollectionAssert.AreEqual(new[] { "Below", "Unchanged", "Above", "Unchanged", "Below" }, outcomes);
    }

    [TestMethod]
    public async Task ThrottleLetsTheFirstRunThrough()
    {
        await using var provider = Services();
        var reached = new List<string>();
        for (var i = 0; i < 2; i++)
        {
            var throttle = new RIoTThrottle { WindowSeconds = new Input<int>(60), Key = new Input<string?>("notify") };
            await Run(provider, Flow(throttle, reached, RIoTThrottle.PassOutcome, RIoTThrottle.SuppressedOutcome), null);
        }
        CollectionAssert.AreEqual(new[] { "Pass", "Suppressed" }, reached);
    }

    [TestMethod]
    public async Task DebounceContinuesOnlyTheLastRun()
    {
        await using var provider = Services();
        var reached = new List<string>();
        var first = await Run(provider, Flow(new RIoTDebounce { WindowSeconds = new Input<int>(300), Key = new Input<string?>("light") }, reached,
            RIoTDebounce.DoneOutcome, RIoTDebounce.SupersededOutcome), null);
        var second = await Run(provider, Flow(new RIoTDebounce { WindowSeconds = new Input<int>(300), Key = new Input<string?>("light") }, reached,
            RIoTDebounce.DoneOutcome, RIoTDebounce.SupersededOutcome), null);
        Assert.IsEmpty(reached, "both wait");
        var delay = first.WorkflowState.Bookmarks.Single();
        Assert.IsInstanceOfType<DelayPayload>(delay.Payload);
        Assert.IsTrue(((DelayPayload)delay.Payload!).ResumeAt > DateTimeOffset.UtcNow.AddSeconds(290));

        await Resume(provider, first, delay);
        await Resume(provider, second, second.WorkflowState.Bookmarks.Single());

        CollectionAssert.AreEqual(new[] { "Superseded", "Done" }, reached);
    }

    [TestMethod]
    public async Task WaitForStateContinuesAtOnceWhenTheReportAlreadyMatches()
    {
        await using var provider = Services(new ReportData { Latest = new ElsaReport { Id = "door", TimeStamp = 5, Value = JsonSerializer.Deserialize<JsonElement>("false") } });
        var reached = new List<string>();
        var wait = WaitFor("door", "false");

        var result = await Run(provider, Flow(wait, reached, RIoTWaitForState.DoneOutcome, RIoTWaitForState.TimedOutOutcome), null);

        CollectionAssert.AreEqual(new[] { "Done" }, reached);
        Assert.IsEmpty(result.WorkflowState.Bookmarks);
    }

    [TestMethod]
    public async Task WaitForStateResumesOnTheMatchingReport()
    {
        await using var provider = Services(new ReportData { Latest = new ElsaReport { Id = "door", TimeStamp = 0 } });
        var reached = new List<string>();
        var wait = WaitFor("door", "false");

        var result = await Run(provider, Flow(wait, reached, RIoTWaitForState.DoneOutcome, RIoTWaitForState.TimedOutOutcome), null);
        Assert.HasCount(2, result.WorkflowState.Bookmarks, "the report and the timeout");
        var report = result.WorkflowState.Bookmarks.Single(b => b.Name == RIoTWaitForState.ReportStimulusName);
        var hasher = provider.GetRequiredService<IStimulusHasher>();
        Assert.AreEqual(hasher.Hash(RIoTWaitForState.ReportStimulusName, "door", null), report.Hash,
            "the bookmark matches the stimulus RIoT Trigger reports are sent with");

        result = await Resume(provider, result, report, Report("door", "true"));
        Assert.IsEmpty(reached, "true doesn't match: still waiting");
        Assert.HasCount(2, result.WorkflowState.Bookmarks);

        result = await Resume(provider, result, report, Report("door", "false"));
        CollectionAssert.AreEqual(new[] { "Done" }, reached);
        Assert.IsEmpty(result.WorkflowState.Bookmarks, "the timeout is gone too");
        Assert.AreEqual(WorkflowStatus.Finished, result.WorkflowState.Status);
    }

    [TestMethod]
    public async Task WaitForStateTimesOut()
    {
        await using var provider = Services(new ReportData { Latest = new ElsaReport { Id = "door", TimeStamp = 0 } });
        var reached = new List<string>();

        var result = await Run(provider, Flow(WaitFor("door", "false"), reached, RIoTWaitForState.DoneOutcome, RIoTWaitForState.TimedOutOutcome), null);
        result = await Resume(provider, result, result.WorkflowState.Bookmarks.Single(b => b.Payload is DelayPayload));

        CollectionAssert.AreEqual(new[] { "Timed out" }, reached);
        Assert.IsEmpty(result.WorkflowState.Bookmarks);
    }

    [TestMethod]
    public async Task ScheduleInsideAWorkflowWaitsForItsKey()
    {
        await using var provider = Services();
        var schedule = new RIoTSchedule { At = new Input<string>("Sunset-30"), Days = new Input<string>("sun,sat") };
        var reached = new List<string>();
        var done = new Inline(() => reached.Add("fired"));
        var flow = new Flowchart { Start = new Inline(() => { }), Activities = { schedule, done } };
        flow.Activities.Add(flow.Start);
        flow.Connections.Add(new Connection(new Endpoint(flow.Start), new Endpoint(schedule)));
        flow.Connections.Add(new Connection(new Endpoint(schedule), new Endpoint(done)));

        var result = await Run(provider, flow, null);
        var bookmark = result.WorkflowState.Bookmarks.Single();
        Assert.AreEqual("sunset-30 sat,sun", bookmark.Payload);
        Assert.AreEqual(provider.GetRequiredService<IStimulusHasher>().Hash(ScheduleService.StimulusName, "sunset-30 sat,sun", null), bookmark.Hash);

        await Resume(provider, result, bookmark, new Dictionary<string, object> { [RIoTSchedule.FiredAtInput] = DateTimeOffset.UnixEpoch });
        CollectionAssert.AreEqual(new[] { "fired" }, reached);
    }

    [TestMethod]
    public async Task NotificationSendsAMessageTheMessagingDeviceReads()
    {
        var data = new ReportData();
        await using var provider = Services(data);
        var notification = new RIoTNotification
        {
            Command = new Input<RIoTTemplateItem>(new RIoTTemplateItem { Id = "push" }),
            Title = new Input<string>("Door"),
            Body = new Input<string>("The door is open"),
            ImageUrl = new Input<string?>(" ")
        };

        var result = await provider.GetRequiredService<IWorkflowRunner>().RunAsync(notification);

        Assert.IsEmpty(result.WorkflowState.Incidents);
        Assert.AreEqual("push", data.CommandId);
        var json = JsonSerializer.Serialize(data.CommandValue);
        Assert.AreEqual("""{"title":"Door","body":"The door is open","topic":"alerts","subject":"Door"}""", json);
        Assert.AreEqual("a@b", RIoTNotification.Message("t", "b", "notifications", "http://i", "a@b")["to"]);
    }

    [TestMethod]
    [DataRow(200, "Done", null)]
    [DataRow(500, "Failed", "HTTP 500 Internal Server Error")]
    public async Task WebhookPostsAndReadsTheStatus(int status, string expected, string? error)
    {
        HttpRequestMessage? sent = null;
        string? body = null;
        await using var provider = Services(webhook: new Handler(async (request, cancellationToken) =>
        {
            sent = request;
            body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("ok") };
        }));
        var outputs = new WebhookOutputs();
        var webhook = outputs.Bind(new RIoTWebhook { Url = new Input<string>("http://hooks.local/x"), Body = new Input<string?>("""{"a":1}""") });
        var reached = new List<string>();

        await Run(provider, Flow(webhook, reached, outputs, RIoTWebhook.DoneOutcome, RIoTWebhook.FailedOutcome), null);

        CollectionAssert.AreEqual(new[] { expected }, reached);
        Assert.AreEqual((HttpMethod.Post, "http://hooks.local/x", """{"a":1}""", "application/json"),
            (sent!.Method, sent.RequestUri!.ToString(), body, sent.Content!.Headers.ContentType!.MediaType));
        Assert.AreEqual((status, "ok", error), (outputs.Status, outputs.Body, outputs.Error));
    }

    [TestMethod]
    public async Task WebhookFailsOnATimeoutAndARefusedUrl()
    {
        await using var slow = Services(webhook: new Handler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        var reached = new List<string>();
        var outputs = new WebhookOutputs();
        var webhook = outputs.Bind(new RIoTWebhook { Url = new Input<string>("http://hooks.local/x"), TimeoutSeconds = new Input<int>(1) });
        await Run(slow, Flow(webhook, reached, outputs, RIoTWebhook.DoneOutcome, RIoTWebhook.FailedOutcome), null);
        CollectionAssert.AreEqual(new[] { "Failed" }, reached);
        Assert.AreEqual((0, "No answer within 1 s"), (outputs.Status, outputs.Error));

        await using var refused = Services(webhook: new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))), policy: new RefuseAll());
        reached.Clear();
        outputs = new WebhookOutputs();
        webhook = outputs.Bind(new RIoTWebhook { Url = new Input<string>("http://hooks.local/x") });
        await Run(refused, Flow(webhook, reached, outputs, RIoTWebhook.DoneOutcome, RIoTWebhook.FailedOutcome), null);
        CollectionAssert.AreEqual(new[] { "Failed" }, reached);
        StringAssert.Contains(outputs.Error, "not here");

        var invalid = await refused.GetRequiredService<IWorkflowRunner>().RunAsync(new RIoTWebhook { Url = new Input<string>("ftp://x") });
        StringAssert.Contains(invalid.WorkflowState.Incidents.Single().Message, "http or https URL");
        invalid = await refused.GetRequiredService<IWorkflowRunner>().RunAsync(new RIoTWebhook
        {
            Url = new Input<string>("http://hooks.local/x"), Body = new Input<string?>("x"), ContentType = new Input<string>("not a type")
        });
        StringAssert.Contains(invalid.WorkflowState.Incidents.Single().Message, "is not a content type");
    }

    [TestMethod]
    public async Task WebhookSendsContentTypeParametersAndFailsWhenTheBodyBreaks()
    {
        string? contentType = null;
        await using var provider = Services(webhook: new Handler((request, _) =>
        {
            contentType = request.Content!.Headers.ContentType!.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new BrokenStream()) });
        }));
        var reached = new List<string>();
        var outputs = new WebhookOutputs();
        var webhook = outputs.Bind(new RIoTWebhook
        {
            Url = new Input<string>("http://hooks.local/x"), Body = new Input<string?>("a=1"), ContentType = new Input<string>("application/x-www-form-urlencoded; charset=latin1")
        });

        await Run(provider, Flow(webhook, reached, outputs, RIoTWebhook.DoneOutcome, RIoTWebhook.FailedOutcome), null);

        Assert.AreEqual("application/x-www-form-urlencoded; charset=utf-8", contentType);
        CollectionAssert.AreEqual(new[] { "Failed" }, reached);
        Assert.AreEqual((200, "The connection dropped"), (outputs.Status, outputs.Error));
    }

    private sealed class BrokenStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("The connection dropped");
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    // ---------------------------------------------------------------- helpers

    private static ServiceProvider Services(ReportData? data = null, HttpMessageHandler? webhook = null, IOutboundUrlPolicy? policy = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddElsa(elsa => elsa.UseJavaScript().UseScheduling().AddActivitiesFrom<RIoTCompare>());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AutomationStateStore>();
        services.AddSingleton<IRIoTDataService>(data ?? new ReportData());
        var client = services.AddHttpClient(RIoTWebhook.HttpClientName)
            .AddHttpMessageHandler(() => new OutboundUrlPolicyHandler(policy ?? AllowAllOutboundUrlPolicy.Instance, RIoTWebhook.OutboundPurpose));
        if (webhook != null)
            client.ConfigurePrimaryHttpMessageHandler(() => webhook);
        return services.BuildServiceProvider();
    }

    private static Flowchart Flow(IActivity activity, List<string> reached, params string[] outcomes) => Flow(activity, reached, null, outcomes);

    private static Flowchart Flow(IActivity activity, List<string> reached, WebhookOutputs? outputs, params string[] outcomes)
    {
        var flowchart = new Flowchart { Start = activity, Activities = { activity } };
        if (outputs != null)
            flowchart.Variables = [outputs.StatusVariable, outputs.BodyVariable, outputs.ErrorVariable];
        foreach (var outcome in outcomes)
        {
            var marker = new Inline(context =>
            {
                reached.Add(outcome);
                outputs?.Read(context);
            });
            flowchart.Activities.Add(marker);
            flowchart.Connections.Add(new Connection(new Endpoint(activity, outcome), new Endpoint(marker)));
        }
        return flowchart;
    }

    private static async Task<RunWorkflowResult> Run(IServiceProvider provider, IActivity root, IDictionary<string, object>? input)
    {
        var result = await provider.GetRequiredService<IWorkflowRunner>().RunAsync(root, new RunWorkflowOptions { Input = input });
        Assert.IsEmpty(result.WorkflowState.Incidents, string.Join(Environment.NewLine, result.WorkflowState.Incidents.Select(x => x.Message)));
        return result;
    }

    private static async Task<RunWorkflowResult> Resume(IServiceProvider provider, RunWorkflowResult previous, Bookmark bookmark, IDictionary<string, object>? input = null)
    {
        var result = await provider.GetRequiredService<IWorkflowRunner>().RunAsync(previous.Workflow, previous.WorkflowState,
            new RunWorkflowOptions { BookmarkId = bookmark.Id, Input = input });
        Assert.IsEmpty(result.WorkflowState.Incidents, string.Join(Environment.NewLine, result.WorkflowState.Incidents.Select(x => x.Message)));
        return result;
    }

    /// <summary>The webhook's outputs, through workflow variables that the outcome markers read.</summary>
    private sealed class WebhookOutputs
    {
        public Variable<int> StatusVariable { get; } = new("status", 0);
        public Variable<string?> BodyVariable { get; } = new("body", null);
        public Variable<string?> ErrorVariable { get; } = new("error", null);
        public int Status { get; private set; }
        public string? Body { get; private set; }
        public string? Error { get; private set; }

        public RIoTWebhook Bind(RIoTWebhook webhook)
        {
            webhook.StatusCode = new Output<int>(StatusVariable);
            webhook.ResponseBody = new Output<string?>(BodyVariable);
            webhook.Error = new Output<string?>(ErrorVariable);
            return webhook;
        }

        public void Read(ActivityExecutionContext context)
        {
            Status = StatusVariable.Get(context);
            Body = BodyVariable.Get(context);
            Error = ErrorVariable.Get(context);
        }
    }

    private static Dictionary<string, object> Report(string id, string valueJson) => new()
    {
        [AutomationActivity.EventDataInput] = JsonSerializer.Deserialize<JsonElement>($$"""{"id":"{{id}}","value":{{valueJson}},"timeStamp":1}""")
    };

    private static RIoTWaitForState WaitFor(string report, string expected) => new()
    {
        Report = new Input<RIoTTemplateItem>(new RIoTTemplateItem { Id = report }),
        Expected = new Input<string>(expected),
        TimeoutSeconds = new Input<int>(600)
    };

    private sealed class ReportData : IRIoTDataService
    {
        public ElsaReport Latest { get; set; } = new() { Id = "none" };
        public string? CommandId { get; private set; }
        public object? CommandValue { get; private set; }

        public Task<object> GetReportValueAsync(string reportId) => Task.FromResult<object>(Latest);

        public Task ExecuteCommandAsync(string id, object? data, CancellationToken cancellationToken = default)
        {
            (CommandId, CommandValue) = (id, data);
            return Task.CompletedTask;
        }

        public Task<CommandConfirmation> ExecuteCommandWithConfirmationAsync(string id, object? data, int ttlSeconds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<object> GetCommandValueAsync(string commandId) => throw new NotSupportedException();
        public Task<object> GetVariableValueAsync(string variableId) => throw new NotSupportedException();
    }

    private sealed class RefuseAll : IOutboundUrlPolicy
    {
        public bool IsAllowed(Uri url, string purpose, out string? reason)
        {
            reason = $"{purpose} calls are not here";
            return false;
        }
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }

    internal sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
        public void Set(DateTimeOffset now) => _now = now;
    }
}
