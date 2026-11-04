using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using Elsa.Expressions.Models;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Activities.Flowchart.Activities;
using Elsa.Workflows.Activities.Flowchart.Models;
using Elsa.Workflows.Memory;
using Elsa.Workflows.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RIoT2.Core.Models;
using RIoT2.Elsa.Server.RIoT.Services;
using RIoT2.Elsa.Workflows.Activities;
using RIoT2.Elsa.Workflows.Llm;
using RIoT2.Elsa.Workflows.Models;
using RIoT2.Elsa.Workflows.Services;
using RIoT2.Elsa.Workflows.UIHints;

namespace RIoT2.Elsa.Tests;

/// <summary>RIoT Forecast and RIoT Command Plan (ADR 0019, 2.1.0), and the remote host's forecast and plan calls.</summary>
[TestClass]
public class ForecastActivityTests
{
    private static readonly DateTimeOffset Midnight = new(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);

    // ---------------------------------------------------------------- RIoT Forecast

    [TestMethod]
    public async Task TheForecastsFiguresAndPointsReachTheNextSteps()
    {
        var data = new PlanData { Forecast = Summary(complete: true) };
        await using var provider = Services(data);
        var forecast = new RIoTForecast
        {
            Forecast = new Input<RIoTTemplateItem>(new RIoTTemplateItem { Id = "prices" }),
            Window = new Input<string>("tomorrow"),
            Rank = new Input<int>(2)
        };
        var min = new Variable<double?>("min", null);
        var cheapest = new Variable<object?>("cheapest", null);
        forecast.Min = new Output<double?>(min);
        forecast.Lowest = new Output<object?>(cheapest);
        var reached = new List<string>();
        double? readMin = null;
        object? readCheapest = null;

        await Run(provider, Flow(forecast, reached, [min, cheapest], context => (readMin, readCheapest) = (min.Get(context), cheapest.Get(context)),
            RIoTForecast.DoneOutcome, RIoTForecast.UnavailableOutcome));

        CollectionAssert.AreEqual(new[] { RIoTForecast.DoneOutcome }, reached);
        Assert.AreEqual(("prices", "tomorrow", 2), (data.ForecastId, data.ForecastRequest!.Window, data.ForecastRequest.Rank));
        Assert.AreEqual(3.0, readMin);
        var first = ((IEnumerable<object?>)readCheapest!).Cast<IDictionary<string, object?>>().First();
        Assert.AreEqual(3.0, Convert.ToDouble(first["value"]));
        StringAssert.StartsWith((string)first["start"]!, "2026-11-05T01:00:00");
    }

    [TestMethod]
    [DataRow(false, true, "Unavailable")]
    [DataRow(false, false, "Done")]
    [DataRow(true, true, "Done")]
    public async Task AnIncompleteWindowIsUnavailableWhenCompletenessIsRequired(bool complete, bool requireComplete, string expected)
    {
        await using var provider = Services(new PlanData { Forecast = Summary(complete) });
        var forecast = new RIoTForecast
        {
            Forecast = new Input<RIoTTemplateItem>(new RIoTTemplateItem { Id = "prices" }),
            RequireComplete = new Input<bool>(requireComplete)
        };
        var reached = new List<string>();
        await Run(provider, Flow(forecast, reached, [], null, RIoTForecast.DoneOutcome, RIoTForecast.UnavailableOutcome));
        CollectionAssert.AreEqual(new[] { expected }, reached);
    }

    [TestMethod]
    public async Task NoForecastYetIsUnavailable()
    {
        await using var provider = Services(new PlanData());
        var forecast = new RIoTForecast { Forecast = new Input<RIoTTemplateItem>(new RIoTTemplateItem { Id = "prices" }) };
        var reached = new List<string>();
        await Run(provider, Flow(forecast, reached, [], null, RIoTForecast.DoneOutcome, RIoTForecast.UnavailableOutcome));
        CollectionAssert.AreEqual(new[] { RIoTForecast.UnavailableOutcome }, reached);
    }

    [TestMethod]
    public async Task ABadRangeTimeIsAnIncident()
    {
        await using var provider = Services(new PlanData { Forecast = Summary(true) });
        var forecast = new RIoTForecast
        {
            Forecast = new Input<RIoTTemplateItem>(new RIoTTemplateItem { Id = "prices" }),
            Window = new Input<string>("range"),
            From = new Input<string?>("tomorrow morning")
        };
        var result = await provider.GetRequiredService<IWorkflowRunner>().RunAsync(forecast);
        StringAssert.Contains(result.WorkflowState.Incidents.Single().Message, "not a time");
    }

    [TestMethod]
    public async Task ALaterStepReadsTheForecastWithGetOutputFrom()
    {
        await using var provider = Services(new PlanData { Forecast = Summary(complete: true) });
        var forecast = new RIoTForecast { Id = "f", Name = "TomorrowTemperature", Forecast = new Input<RIoTTemplateItem>(new RIoTTemplateItem { Id = "temperature" }) };
        var compare = new RIoTCompare
        {
            Id = "c",
            Value = new Input<object?>(new Expression("JavaScript", "return getOutputFrom('TomorrowTemperature', 'Min');")),
            Operator = new Input<string>("less than"),
            Expected = new Input<string>("3.5")
        };
        var reached = new List<string>();
        var flowchart = Flow(compare, reached, [], null, RIoTCompare.TrueOutcome, RIoTCompare.FalseOutcome);
        flowchart.Activities.Add(forecast);
        flowchart.Start = forecast;
        flowchart.Connections.Add(new Connection(new Endpoint(forecast, RIoTForecast.DoneOutcome), new Endpoint(compare)));

        await Run(provider, flowchart);

        CollectionAssert.AreEqual(new[] { RIoTCompare.TrueOutcome }, reached, "the minimum is 3");
    }

    [TestMethod]
    public async Task ThePreheatTemplatesScriptTurnsTheCheapestSlotsIntoSteps()
    {
        // Slots 01-02 (3) and 00-01 (5) are adjacent: one run from 00 to 02.
        var data = new PlanData { Forecast = Summary(complete: true) };
        await using var provider = Services(data);
        var template = WorkflowTemplates.ForecastPreheat();
        var script = ((Flowchart)template.Root).Activities.OfType<RIoTCommandPlan>().Single().Steps.Expression!.Value!.ToString()!
            .Replace("CheapestSlotsLeftToday", "Cheapest", StringComparison.Ordinal);
        var forecast = new RIoTForecast { Id = "f", Name = "Cheapest", Forecast = new Input<RIoTTemplateItem>(new RIoTTemplateItem { Id = "prices" }), Rank = new Input<int>(2) };
        var plan = new RIoTCommandPlan
        {
            Id = "p",
            Command = new Input<RIoTTemplateItem>(new RIoTTemplateItem { Id = "heatpump" }),
            Steps = new Input<object?>(new Expression("JavaScript", script))
        };
        var reached = new List<string>();
        var flowchart = Flow(plan, reached, [], null, RIoTCommandPlan.DoneOutcome, RIoTCommandPlan.FailedOutcome);
        flowchart.Activities.Add(forecast);
        flowchart.Start = forecast;
        flowchart.Connections.Add(new Connection(new Endpoint(forecast, RIoTForecast.DoneOutcome), new Endpoint(plan)));

        await Run(provider, flowchart);

        CollectionAssert.AreEqual(new[] { RIoTCommandPlan.DoneOutcome }, reached);
        var steps = data.Submitted!.Steps;
        CollectionAssert.AreEqual(new[] { Midnight, Midnight.AddHours(2) }, steps.Select(x => x.At).ToArray());
        CollectionAssert.AreEqual(new[] { 22.0, 20.0 }, steps.Select(x => Convert.ToDouble(x.Value)).ToArray());
    }

    [TestMethod]
    public async Task TheLlmTemplatesPlanTakesRunLlmsJsonAnswer()
    {
        var data = new PlanData();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddElsa(elsa => elsa.UseJavaScript().AddActivitiesFrom<RIoTForecast>());
        services.AddSingleton<IRIoTDataService>(data);
        services.AddSingleton<ILlmRunner>(new AnsweringRunner("""{"steps":[{"at":"2026-11-05T02:00:00Z","value":22},{"at":"2026-11-05T05:00:00Z","value":19.5}],"reason":"cheap night"}"""));
        await using var provider = services.BuildServiceProvider();
        var template = (Flowchart)WorkflowTemplates.LlmHeatSchedule().Root;
        var llm = template.Activities.OfType<RIoTRunLlm>().Single();
        llm.Connection = new Input<string>("test");
        llm.InputData = new Input<object?>("{}");
        var plan = template.Activities.OfType<RIoTCommandPlan>().Single();
        plan.Command = new Input<RIoTTemplateItem>(new RIoTTemplateItem { Id = "heatpump" });
        var reached = new List<string>();
        var flowchart = Flow(plan, reached, [], null, RIoTCommandPlan.DoneOutcome, RIoTCommandPlan.FailedOutcome);
        flowchart.Activities.Add(llm);
        flowchart.Start = llm;
        flowchart.Connections.Add(new Connection(new Endpoint(llm, RIoTRunLlm.DoneOutcome), new Endpoint(plan)));

        await Run(provider, flowchart);

        CollectionAssert.AreEqual(new[] { RIoTCommandPlan.DoneOutcome }, reached);
        CollectionAssert.AreEqual(new[] { Midnight.AddHours(2), Midnight.AddHours(5) }, data.Submitted!.Steps.Select(x => x.At).ToArray());
        Assert.AreEqual(19.5, Convert.ToDouble(data.Submitted.Steps[1].Value));
    }

    private sealed class AnsweringRunner(string json) : ILlmRunner
    {
        public Task<LlmRunResult> RunAsync(LlmRunRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new LlmRunResult { Succeeded = true, Text = json, Json = JsonNode.Parse(json) });
    }

    // ---------------------------------------------------------------- RIoT Command Plan

    [TestMethod]
    public void StepsAreReadFromListsObjectsAndJsonText()
    {
        Assert.IsTrue(RIoTCommandPlan.TryReadSteps(new object[]
        {
            new Dictionary<string, object?> { ["at"] = "2026-11-05T06:00:00Z", ["value"] = 22 },
            new Dictionary<string, object?> { ["start"] = "2026-11-05T09:00:00+02:00", ["value"] = "eco" }
        }, out var steps, out _));
        Assert.HasCount(2, steps);
        Assert.AreEqual(Midnight.AddHours(6), steps[0].At);
        Assert.AreEqual(22L, steps[0].Value);
        Assert.AreEqual(Midnight.AddHours(7), steps[1].At);
        Assert.AreEqual("eco", steps[1].Value);

        // Run LLM's JSON answer.
        Assert.IsTrue(RIoTCommandPlan.TryReadSteps("""{"steps":[{"at":"2026-11-05T06:00:00Z","value":{"target":21.5}}],"reason":"cheap"}""", out steps, out _));
        Assert.AreEqual(21.5, ((IDictionary<string, object?>)steps.Single().Value!)["target"]);

        Assert.IsTrue(RIoTCommandPlan.TryReadSteps(null, out steps, out _));
        Assert.IsEmpty(steps);
        Assert.IsTrue(RIoTCommandPlan.TryReadSteps("[]", out steps, out _));
        Assert.IsEmpty(steps);

        Assert.IsFalse(RIoTCommandPlan.TryReadSteps("""[{"at":"soon","value":1}]""", out _, out var problem));
        StringAssert.Contains(problem, "time");
        Assert.IsFalse(RIoTCommandPlan.TryReadSteps("""[{"at":"2026-11-05T06:00:00Z"}]""", out _, out problem));
        StringAssert.Contains(problem, "no value");
        Assert.IsFalse(RIoTCommandPlan.TryReadSteps("""{"value":1}""", out _, out problem));
        StringAssert.Contains(problem, "list");
        Assert.IsFalse(RIoTCommandPlan.TryReadSteps(42, out _, out _));
    }

    [TestMethod]
    public async Task APlanGoesToTheOrchestratorAndItsIdToTheNextSteps()
    {
        var data = new PlanData();
        await using var provider = Services(data);
        var plan = new RIoTCommandPlan
        {
            Command = new Input<RIoTTemplateItem>(new RIoTTemplateItem { Id = "heatpump" }),
            Steps = new Input<object?>(new Expression("JavaScript", "return [{ at: '2026-11-05T06:00:00Z', value: 22 }, { at: '2026-11-05T09:00:00Z', value: 20 }];")),
            PlanName = new Input<string?>("Preheat"),
            CatchUp = new Input<string>("none")
        };
        var id = new Variable<string?>("id", null);
        plan.PlanId = new Output<string?>(id);
        var reached = new List<string>();
        string? readId = null;

        await Run(provider, Flow(plan, reached, [id], context => readId = id.Get(context), RIoTCommandPlan.DoneOutcome, RIoTCommandPlan.FailedOutcome));

        CollectionAssert.AreEqual(new[] { RIoTCommandPlan.DoneOutcome }, reached);
        Assert.AreEqual("plan-1", readId);
        var submitted = data.Submitted!;
        Assert.AreEqual(("heatpump", "Preheat", "none"), (submitted.CommandId, submitted.Name, submitted.CatchUp));
        StringAssert.StartsWith(submitted.Source, "workflow ");
        CollectionAssert.AreEqual(new[] { Midnight.AddHours(6), Midnight.AddHours(9) }, submitted.Steps.Select(x => x.At).ToArray());
        Assert.AreEqual(22.0, Convert.ToDouble(submitted.Steps[0].Value));
    }

    [TestMethod]
    public async Task ARefusedPlanContinuesOnFailed()
    {
        var data = new PlanData { Receipt = CommandPlanReceipt.Failure("A plan has at most 200 steps.") };
        await using var provider = Services(data);
        var plan = new RIoTCommandPlan
        {
            Command = new Input<RIoTTemplateItem>(new RIoTTemplateItem { Id = "heatpump" }),
            Steps = new Input<object?>("""[{"at":"2026-11-05T06:00:00Z","value":22}]""")
        };
        var reached = new List<string>();
        await Run(provider, Flow(plan, reached, [], null, RIoTCommandPlan.DoneOutcome, RIoTCommandPlan.FailedOutcome));
        CollectionAssert.AreEqual(new[] { RIoTCommandPlan.FailedOutcome }, reached);

        // Unreadable steps fail without asking the orchestrator.
        data.Submitted = null;
        plan.Steps = new Input<object?>("""[{"at":"never","value":22}]""");
        reached.Clear();
        await Run(provider, Flow(plan, reached, [], null, RIoTCommandPlan.DoneOutcome, RIoTCommandPlan.FailedOutcome));
        CollectionAssert.AreEqual(new[] { RIoTCommandPlan.FailedOutcome }, reached);
        Assert.IsNull(data.Submitted);
    }

    [TestMethod]
    public async Task EmptyStepsEndTheCommandsPlan()
    {
        var data = new PlanData();
        await using var provider = Services(data);
        var plan = new RIoTCommandPlan
        {
            Command = new Input<RIoTTemplateItem>(new RIoTTemplateItem { Id = "heatpump" }),
            Steps = new Input<object?>("[]")
        };
        var reached = new List<string>();
        await Run(provider, Flow(plan, reached, [], null, RIoTCommandPlan.DoneOutcome, RIoTCommandPlan.FailedOutcome));
        CollectionAssert.AreEqual(new[] { RIoTCommandPlan.DoneOutcome }, reached);
        Assert.AreEqual("heatpump", data.Cancelled);
        Assert.IsNull(data.Submitted);
    }

    // ---------------------------------------------------------------- contracts

    [TestMethod]
    public void TheForecastSelectorOffersOnlyForecastReports()
    {
        var settings = new RIoTForecastOptionsProvider().Settings;
        Assert.IsTrue(settings.ForecastOnly);
        CollectionAssert.AreEqual(new[] { TemplateType.Report }, settings.TemplateTypes.ToArray());
        Assert.IsFalse(new RIoTReportOptionsProvider().Settings.ForecastOnly);
    }

    [TestMethod]
    public void RunLlmAcceptsThePlanCommandsTool()
    {
        var request = new LlmRunRequest { Connection = "c", Instructions = "i", Tools = [LlmTools.PlanCommands] };
        Assert.IsNull(request.Validate());
        CollectionAssert.Contains(LlmTools.All.ToList(), LlmTools.PlanCommands);
    }

    [TestMethod]
    public async Task TheRemoteHostReadsForecastsAndPlansOverHttp()
    {
        var requests = new List<(HttpMethod Method, string Url, string? Body)>();
        using var client = new HttpClient(new Handler(async (request, cancellationToken) =>
        {
            requests.Add((request.Method, request.RequestUri!.PathAndQuery, request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
            var path = request.RequestUri.AbsolutePath;
            return (request.Method.Method, path) switch
            {
                ("GET", "/api/v2/forecasts/prices") => Json(HttpStatusCode.OK, RIoT2.Core.Utils.Json.Serialize(Summary(true))!),
                ("GET", "/api/v2/forecasts/missing") => new HttpResponseMessage(HttpStatusCode.NotFound),
                ("GET", "/api/v2/forecasts/bad") => Json(HttpStatusCode.BadRequest, "The window must be one of all, today, tomorrow, next, range."),
                ("POST", "/api/v2/command-plans") when requests.Count(x => x.Method == HttpMethod.Post) == 1 =>
                    Json(HttpStatusCode.Created, """{"id":"p1","commandId":"heatpump","steps":[{},{}]}"""),
                ("POST", "/api/v2/command-plans") => Json(HttpStatusCode.BadRequest, "\"No node has the command 'x'.\""),
                ("DELETE", "/api/v2/command-plans") => Json(HttpStatusCode.OK, """{"id":"p1"}"""),
                _ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
            };
        }));
        var service = new RIoTDataService(new Configuration(), client);

        var summary = await service.GetForecastAsync("prices", new ForecastRequest { Window = "range", From = Midnight, To = Midnight.AddHours(6), Rank = 3 });
        Assert.AreEqual(3.0, summary!.Min);
        StringAssert.Contains(requests[0].Url, "window=range&rank=3&from=2026-11-05T00%3A00%3A00.0000000Z&to=2026-11-05T06%3A00%3A00.0000000Z");
        Assert.IsNull(await service.GetForecastAsync("missing", new ForecastRequest()));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.GetForecastAsync("bad", new ForecastRequest { Window = "soon" }));

        var receipt = await service.SubmitCommandPlanAsync(new CommandPlanSubmission
        {
            CommandId = "heatpump",
            Steps = [new(Midnight.AddHours(6), 22), new(Midnight.AddHours(9), new Dictionary<string, object?> { ["target"] = 20 })]
        });
        Assert.AreEqual((true, "p1", 2), (receipt.Succeeded, receipt.PlanId, receipt.StepCount));
        var posted = JsonNode.Parse(requests.Single(x => x.Method == HttpMethod.Post).Body!)!;
        Assert.AreEqual("heatpump", (string?)posted["commandId"]);
        Assert.AreEqual(22, (int?)posted["steps"]![0]!["value"]);
        Assert.AreEqual(20, (int?)posted["steps"]![1]!["value"]!["target"]);
        StringAssert.StartsWith((string?)posted["steps"]![0]!["at"], "2026-11-05T06:00:00");

        var refused = await service.SubmitCommandPlanAsync(new CommandPlanSubmission { CommandId = "x", Steps = [new(Midnight, 1)] });
        Assert.AreEqual((false, "No node has the command 'x'."), (refused.Succeeded, refused.Error));

        Assert.AreEqual("p1", await service.CancelCommandPlanAsync("heatpump"));
        Assert.AreEqual("/api/v2/command-plans?commandId=heatpump", requests.Last().Url);
    }

    // ---------------------------------------------------------------- helpers

    private static ForecastSummary Summary(bool complete)
    {
        var series = new ForecastSeries
        {
            IssuedAt = Midnight.AddHours(-12),
            Kind = ForecastSeries.ConfirmedKind,
            Unit = "c/kWh",
            Points = [new(Midnight, Midnight.AddHours(1), 5), new(Midnight.AddHours(1), Midnight.AddHours(2), 3), new(Midnight.AddHours(2), Midnight.AddHours(3), 8)]
        };
        return ForecastWindow.Summarize(series, Midnight, complete ? Midnight.AddHours(3) : Midnight.AddHours(5), 2);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    private static ServiceProvider Services(PlanData data)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddElsa(elsa => elsa.UseJavaScript().AddActivitiesFrom<RIoTForecast>());
        services.AddSingleton<IRIoTDataService>(data);
        return services.BuildServiceProvider();
    }

    private static Flowchart Flow(IActivity activity, List<string> reached, global::Elsa.Workflows.Memory.Variable[] variables, Action<ActivityExecutionContext>? read, params string[] outcomes)
    {
        var flowchart = new Flowchart { Start = activity, Activities = { activity }, Variables = variables.ToList() };
        foreach (var outcome in outcomes)
        {
            var marker = new Inline(context =>
            {
                reached.Add(outcome);
                read?.Invoke(context);
            });
            flowchart.Activities.Add(marker);
            flowchart.Connections.Add(new Connection(new Endpoint(activity, outcome), new Endpoint(marker)));
        }
        return flowchart;
    }

    private static async Task Run(IServiceProvider provider, IActivity root)
    {
        var result = await provider.GetRequiredService<IWorkflowRunner>().RunAsync(root);
        Assert.IsEmpty(result.WorkflowState.Incidents, string.Join(Environment.NewLine, result.WorkflowState.Incidents.Select(x => $"{x.Message} {x.Exception?.Message} {x.Exception?.InnerException?.Message}")));
    }

    private sealed class PlanData : IRIoTDataService
    {
        public ForecastSummary? Forecast { get; set; }
        public string? ForecastId { get; private set; }
        public ForecastRequest? ForecastRequest { get; private set; }
        public CommandPlanSubmission? Submitted { get; set; }
        public CommandPlanReceipt? Receipt { get; set; }
        public string? Cancelled { get; private set; }

        public Task<ForecastSummary?> GetForecastAsync(string reportId, ForecastRequest request, CancellationToken cancellationToken = default)
        {
            (ForecastId, ForecastRequest) = (reportId, request);
            return Task.FromResult(Forecast);
        }

        public Task<CommandPlanReceipt> SubmitCommandPlanAsync(CommandPlanSubmission plan, CancellationToken cancellationToken = default)
        {
            Submitted = plan;
            return Task.FromResult(Receipt ?? new CommandPlanReceipt { Succeeded = true, PlanId = "plan-1", StepCount = plan.Steps.Count });
        }

        public Task<string?> CancelCommandPlanAsync(string commandId, CancellationToken cancellationToken = default)
        {
            Cancelled = commandId;
            return Task.FromResult<string?>("plan-0");
        }

        public Task<object> GetReportValueAsync(string reportId) => throw new NotSupportedException();
        public Task<object> GetCommandValueAsync(string commandId) => throw new NotSupportedException();
        public Task<object> GetVariableValueAsync(string variableId) => throw new NotSupportedException();
        public Task ExecuteCommandAsync(string id, object? data, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CommandConfirmation> ExecuteCommandWithConfirmationAsync(string id, object? data, int ttlSeconds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }

    private sealed class Configuration : RIoT2.Elsa.Server.RIoT.Services.Interfaces.IRIoTConfigurationService
    {
        public string Id => "test";
        public string? OrchestratorBaseUrl { get; set; } = "http://localhost";
        public string? WorkflowBaseUrl { get; set; }
        public string? WorkflowGrpcBaseUrl => "http://localhost:5003";
        public MqttConfiguration Mqtt { get; } = new();
    }
}
