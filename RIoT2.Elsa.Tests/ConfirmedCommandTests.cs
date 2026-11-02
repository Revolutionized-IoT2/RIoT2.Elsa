using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Activities.Flowchart.Activities;
using Elsa.Workflows.Activities.Flowchart.Models;
using Elsa.Workflows.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RIoT2.Core.Models;
using RIoT2.Elsa.Server.RIoT.Activities;
using RIoT2.Elsa.Server.RIoT.Models;
using RIoT2.Elsa.Server.RIoT.Services;
using RIoT2.Elsa.Server.RIoT.Services.Interfaces;
using RIoT2.Elsa.Studio.Models;
using Template = RIoT2.Elsa.Server.RIoT.Models.Template;

namespace RIoT2.Elsa.Tests;

/// <summary>The "RIoT Command with confirmation" activity (design 7.1 phase 4, I36).</summary>
[TestClass]
public class ConfirmedCommandTests
{
    [TestMethod]
    public async Task TheCommandIsSentWithWaitAndTheDeliveryStatusIsRead()
    {
        JsonNode? posted = null;
        using var client = new HttpClient(new Handler(async (request, cancellationToken) =>
        {
            Assert.AreEqual(HttpMethod.Post, request.Method);
            Assert.AreEqual("/api/v2/commands", request.RequestUri!.AbsolutePath);
            posted = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "correlationId": "c1", "commandId": "lamp", "nodeId": "n", "status": "failed", "isFinal": true, "error": "Port COM3 not found", "attempts": 1 }""")
            };
        }));
        var service = new RIoTDataService(new Configuration(), client);

        var confirmation = await service.ExecuteCommandWithConfirmationAsync("lamp", true, 20);

        Assert.AreEqual(("lamp", true, 20, true), ((string?)posted!["id"], (bool?)posted["value"], (int?)posted["ttlSeconds"], (bool?)posted["wait"]));
        Assert.AreEqual(("c1", "failed", "Port COM3 not found", false), (confirmation.CorrelationId, confirmation.Status, confirmation.Error, confirmation.Succeeded));
    }

    [TestMethod]
    public async Task AnUnknownCommandIdFailsTheActivityRun()
    {
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))));
        var service = new RIoTDataService(new Configuration(), client);

        await Assert.ThrowsExactlyAsync<HttpRequestException>(() => service.ExecuteCommandWithConfirmationAsync("missing", 1, 30));
    }

    [TestMethod]
    [DataRow("executed", ConfirmedCommandOutcome.Done)]
    [DataRow("duplicate", ConfirmedCommandOutcome.Done)]
    [DataRow("sent", ConfirmedCommandOutcome.Done)]
    [DataRow("failed", ConfirmedCommandOutcome.Failed)]
    [DataRow("unknownCommand", ConfirmedCommandOutcome.Failed)]
    [DataRow("expired", ConfirmedCommandOutcome.Failed)]
    public async Task TheOutcomeFollowsTheDeliveryStatus(string status, ConfirmedCommandOutcome expected)
    {
        var data = new ConfirmingDataService(status);
        var reached = new List<string>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddElsa(elsa => elsa.UseJavaScript().AddActivitiesFrom<RIoTConfirmedCommand>());
        services.AddSingleton<IRIoTDataService>(data);
        await using var provider = services.BuildServiceProvider();
        var runner = provider.GetRequiredService<IWorkflowRunner>();

        var command = new RIoTConfirmedCommand
        {
            Command = new Input<RIoTTemplateItem>(new RIoTTemplateItem { Id = "lamp", Value = "return 42;" }),
            TimeoutSeconds = new Input<int>(15)
        };
        var done = new Inline(() => reached.Add(RIoTConfirmedCommand.DoneOutcome));
        var failed = new Inline(() => reached.Add(RIoTConfirmedCommand.FailedOutcome));
        var flowchart = new Flowchart
        {
            Start = command,
            Activities = { command, done, failed },
            Connections =
            {
                new Connection(new Endpoint(command, RIoTConfirmedCommand.DoneOutcome), new Endpoint(done)),
                new Connection(new Endpoint(command, RIoTConfirmedCommand.FailedOutcome), new Endpoint(failed))
            }
        };

        var result = await runner.RunAsync(flowchart);

        Assert.IsEmpty(result.WorkflowState.Incidents, string.Join(Environment.NewLine, result.WorkflowState.Incidents.Select(x => x.Message)));
        CollectionAssert.AreEqual(new[] { expected.ToString() }, reached);
        Assert.AreEqual(("lamp", 42.0, 15), (data.Id, Convert.ToDouble(data.Value), data.TtlSeconds));
    }

    [TestMethod]
    public async Task ATimeoutOutsideTheLimitIsAnIncident()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddElsa(elsa => elsa.UseJavaScript().AddActivitiesFrom<RIoTConfirmedCommand>());
        services.AddSingleton<IRIoTDataService>(new ConfirmingDataService("executed"));
        await using var provider = services.BuildServiceProvider();

        var result = await provider.GetRequiredService<IWorkflowRunner>().RunAsync(new RIoTConfirmedCommand
        {
            Command = new Input<RIoTTemplateItem>(new RIoTTemplateItem { Id = "lamp", Value = "return true;" }),
            TimeoutSeconds = new Input<int>(RIoTConfirmedCommand.MaxTimeoutSeconds + 1)
        });

        StringAssert.Contains(result.WorkflowState.Incidents.Single().Message, "The timeout must be 1 to 50 seconds");
    }

    public enum ConfirmedCommandOutcome { Done, Failed }

    private sealed class ConfirmingDataService(string status) : IRIoTDataService
    {
        public string? Id { get; private set; }
        public object? Value { get; private set; }
        public int TtlSeconds { get; private set; }

        public Task<CommandConfirmation> ExecuteCommandWithConfirmationAsync(string id, object? data, int ttlSeconds, CancellationToken cancellationToken = default)
        {
            (Id, Value, TtlSeconds) = (id, data, ttlSeconds);
            return Task.FromResult(new CommandConfirmation { CorrelationId = "c1", CommandId = id, Status = status });
        }

        public Task ExecuteCommandAsync(string id, object? data, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<List<Template>> GetReportTemplatesAsync() => throw new NotSupportedException();
        public Task<List<Template>> GetCommandTemplatesAsync() => throw new NotSupportedException();
        public Task<List<Template>> GetVariableTemplatesAsync() => throw new NotSupportedException();
        public Task<object> GetReportValueAsync(string reportId) => throw new NotSupportedException();
        public Task<object> GetCommandValueAsync(string commandId) => throw new NotSupportedException();
        public Task<object> GetVariableValueAsync(string variableId) => throw new NotSupportedException();
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
