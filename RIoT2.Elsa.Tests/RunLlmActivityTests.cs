using System.Text.Json.Nodes;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Activities.Flowchart.Activities;
using Elsa.Workflows.Activities.Flowchart.Models;
using Elsa.Workflows.Memory;
using Elsa.Workflows.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RIoT2.Elsa.Workflows.Llm;

namespace RIoT2.Elsa.Tests;

/// <summary>The Run LLM activity (ADR 0016): what it asks the orchestrator's LLM service, and how its answer continues the workflow.</summary>
[TestClass]
public class RunLlmActivityTests
{
    [TestMethod]
    public async Task TheStepSendsItsSettingsAndContinuesOnDoneWithTheAnswer()
    {
        var runner = new FakeRunner(new LlmRunResult
        {
            Succeeded = true,
            Text = """{"heating":true}""",
            Json = JsonNode.Parse("""{"heating":true,"reason":"cold"}"""),
            Commands = [new LlmSentCommand("heater", JsonValue.Create(true), "executed")],
            ToolCalls = 2,
            Usage = new LlmUsage(100, 20)
        });
        var step = new RIoTRunLlm
        {
            Connection = new Input<string>(" openai "),
            Instructions = new Input<string>("Decide."),
            InputData = new Input<object?>(new Dictionary<string, object> { ["temperature"] = 18.5 }),
            ResponseFormat = new Input<string>(LlmResponseFormats.Json),
            JsonSchema = new Input<string?>("""{"type":"object","required":["heating"]}"""),
            Tools = new Input<string?>("ReadValues, SendCommands"),
            AllowedCommands = new Input<string?>("heater\nlamp\nheater"),
            MaxCommands = new Input<int>(1),
            TimeoutSeconds = new Input<int>(30)
        };
        var outputs = new Outputs(step);

        var reached = await RunAsync(runner, step, outputs);

        CollectionAssert.AreEqual(new[] { RIoTRunLlm.DoneOutcome }, reached);
        var request = runner.Requests.Single();
        Assert.AreEqual("openai", request.Connection);
        Assert.AreEqual("Decide.", request.Instructions);
        Assert.AreEqual(18.5, request.Input!["temperature"]!.GetValue<double>());
        Assert.AreEqual(LlmResponseFormats.Json, request.ResponseFormat);
        Assert.AreEqual("object", request.JsonSchema!["type"]!.GetValue<string>());
        CollectionAssert.AreEqual(new[] { LlmTools.ReadValues, LlmTools.SendCommands }, request.Tools.ToArray());
        CollectionAssert.AreEqual(new[] { "heater", "lamp" }, request.AllowedCommands.ToArray());
        Assert.AreEqual((1, LlmRunRequest.DefaultMaxToolCalls, 30), (request.MaxCommands, request.MaxToolCalls, request.TimeoutSeconds));
        Assert.AreEqual("step", request.ActivityId);
        Assert.IsFalse(string.IsNullOrEmpty(request.WorkflowInstanceId));

        Assert.AreEqual("""{"heating":true}""", outputs.Text);
        var json = (IDictionary<string, object?>)outputs.Json!;
        Assert.AreEqual(true, json["heating"]);
        Assert.AreEqual("cold", json["reason"]);
        Assert.AreEqual((1, 2, 120, null), (outputs.CommandsSent, outputs.ToolCalls, outputs.TotalTokens, outputs.ErrorCode));
    }

    [TestMethod]
    public async Task AFailedStepContinuesOnFailedWithTheReason()
    {
        var runner = new FakeRunner(LlmRunResult.Failure(LlmErrorCodes.InvalidJson, "Not JSON", new LlmUsage(10, 5)));
        var step = new RIoTRunLlm { Connection = new Input<string>("openai"), Instructions = new Input<string>("Decide.") };
        var outputs = new Outputs(step);

        var reached = await RunAsync(runner, step, outputs);

        CollectionAssert.AreEqual(new[] { RIoTRunLlm.FailedOutcome }, reached);
        Assert.AreEqual((LlmErrorCodes.InvalidJson, "Not JSON", 15), (outputs.ErrorCode, outputs.Error, outputs.TotalTokens));
    }

    [TestMethod]
    [DataRow("", "Decide.", null, null, "A model connection is required.")]
    [DataRow("openai", " ", null, null, "Instructions are required.")]
    [DataRow("openai", "Decide.", "{not json", null, "The JSON schema is not JSON")]
    [DataRow("openai", "Decide.", null, "Delete", "Unknown tool 'Delete'")]
    public async Task AnInvalidStepFailsWithoutAskingTheModel(string connection, string instructions, string? schema, string? tools, string problem)
    {
        var runner = new FakeRunner(new LlmRunResult { Succeeded = true });
        var step = new RIoTRunLlm
        {
            Connection = new Input<string>(connection),
            Instructions = new Input<string>(instructions),
            JsonSchema = new Input<string?>(schema),
            Tools = new Input<string?>(tools)
        };
        var outputs = new Outputs(step);

        var reached = await RunAsync(runner, step, outputs);

        CollectionAssert.AreEqual(new[] { RIoTRunLlm.FailedOutcome }, reached);
        Assert.IsEmpty(runner.Requests);
        Assert.AreEqual(LlmErrorCodes.InvalidRequest, outputs.ErrorCode);
        StringAssert.Contains(outputs.Error, problem);
    }

    [TestMethod]
    public async Task WithoutAnLlmServiceTheStepFails()
    {
        var step = new RIoTRunLlm { Connection = new Input<string>("openai"), Instructions = new Input<string>("Decide.") };
        var outputs = new Outputs(step);

        var reached = await RunAsync(null, step, outputs);

        CollectionAssert.AreEqual(new[] { RIoTRunLlm.FailedOutcome }, reached);
        Assert.AreEqual(LlmErrorCodes.Unavailable, outputs.ErrorCode);
    }

    [TestMethod]
    public void ListsAreSplitOnCommasAndLines()
    {
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, RIoTRunLlm.List(" a, b\r\nc;a ").ToArray());
        Assert.IsEmpty(RIoTRunLlm.List(null));
    }

    private static async Task<List<string>> RunAsync(ILlmRunner? runner, RIoTRunLlm step, Outputs outputs)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddElsa(elsa => elsa.UseJavaScript().AddActivitiesFrom<RIoTRunLlm>());
        if (runner != null)
            services.AddSingleton(runner);
        await using var provider = services.BuildServiceProvider();

        step.Id = "step";
        var reached = new List<string>();
        var flowchart = new Flowchart { Start = step, Activities = { step }, Variables = outputs.Variables };
        foreach (var outcome in new[] { RIoTRunLlm.DoneOutcome, RIoTRunLlm.FailedOutcome })
        {
            var marker = new Inline(context =>
            {
                reached.Add(outcome);
                outputs.Read(context);
            });
            flowchart.Activities.Add(marker);
            flowchart.Connections.Add(new Connection(new Endpoint(step, outcome), new Endpoint(marker)));
        }

        var result = await provider.GetRequiredService<IWorkflowRunner>().RunAsync(flowchart);
        Assert.IsEmpty(result.WorkflowState.Incidents, string.Join(Environment.NewLine, result.WorkflowState.Incidents.Select(x => x.Message)));
        return reached;
    }

    private sealed class FakeRunner(LlmRunResult result) : ILlmRunner
    {
        public List<LlmRunRequest> Requests { get; } = [];

        public Task<LlmRunResult> RunAsync(LlmRunRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(result);
        }
    }

    private sealed class Outputs
    {
        private readonly Variable<string?> _text = new("text", null);
        private readonly Variable<object?> _json = new("json", null);
        private readonly Variable<int> _commands = new("commands", 0);
        private readonly Variable<int> _toolCalls = new("toolCalls", 0);
        private readonly Variable<int> _tokens = new("tokens", 0);
        private readonly Variable<string?> _errorCode = new("errorCode", null);
        private readonly Variable<string?> _error = new("error", null);

        public Outputs(RIoTRunLlm step)
        {
            step.Text = new Output<string?>(_text);
            step.Json = new Output<object?>(_json);
            step.CommandsSent = new Output<int>(_commands);
            step.ToolCalls = new Output<int>(_toolCalls);
            step.TotalTokens = new Output<int>(_tokens);
            step.ErrorCode = new Output<string?>(_errorCode);
            step.Error = new Output<string?>(_error);
        }

        public ICollection<Variable> Variables => [_text, _json, _commands, _toolCalls, _tokens, _errorCode, _error];

        public string? Text { get; private set; }
        public object? Json { get; private set; }
        public int CommandsSent { get; private set; }
        public int ToolCalls { get; private set; }
        public int TotalTokens { get; private set; }
        public string? ErrorCode { get; private set; }
        public string? Error { get; private set; }

        public void Read(ActivityExecutionContext context)
        {
            Text = _text.Get(context);
            Json = _json.Get(context);
            CommandsSent = _commands.Get(context);
            ToolCalls = _toolCalls.Get(context);
            TotalTokens = _tokens.Get(context);
            ErrorCode = _errorCode.Get(context);
            Error = _error.Get(context);
        }
    }
}
