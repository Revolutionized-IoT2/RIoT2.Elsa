using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RIoT2.Core.Models;
using RIoT2.Elsa.Server.RIoT.Services;
using RIoT2.Elsa.Server.RIoT.Services.Interfaces;
using RIoT2.Elsa.Workflows.Llm;

namespace RIoT2.Elsa.Tests;

/// <summary>The remote workflow host runs Run LLM steps on the orchestrator (ADR 0016).</summary>
[TestClass]
public class HttpLlmRunnerTests
{
    [TestMethod]
    public async Task TheStepIsPostedToTheOrchestratorAndItsResultRead()
    {
        string? body = null;
        using var client = new HttpClient(new Handler(async (request, ct) =>
        {
            Assert.AreEqual("http://orchestrator/api/llm/run", request.RequestUri!.ToString());
            body = await request.Content!.ReadAsStringAsync(ct);
            return Json(HttpStatusCode.OK, """{"succeeded":true,"text":"on","toolCalls":1,"usage":{"inputTokens":5,"outputTokens":2},"commands":[{"id":"lamp","value":true,"status":"executed"}]}""");
        }));
        var runner = new HttpLlmRunner(new Configuration("http://orchestrator/"), client);

        var result = await runner.RunAsync(new LlmRunRequest { Connection = "openai", Instructions = "Decide.", Input = JsonNode.Parse("""{"t":1}""") });

        var sent = JsonNode.Parse(body!)!;
        Assert.AreEqual("openai", sent["connection"]!.GetValue<string>());
        Assert.AreEqual(1, sent["input"]!["t"]!.GetValue<int>());
        Assert.IsTrue(result.Succeeded);
        Assert.AreEqual(("on", 1, 7, "lamp"), (result.Text, result.ToolCalls, result.Usage.TotalTokens, result.Commands.Single().Id));
    }

    [TestMethod]
    public async Task AFailedStepKeepsTheOrchestratorsReasonAndAnyOtherAnswerIsUnavailable()
    {
        using var failing = new HttpClient(new Handler((_, _) => Task.FromResult(Json(HttpStatusCode.UnprocessableEntity,
            """{"succeeded":false,"errorCode":"llm_unknown_connection","error":"No connection 'x'."}"""))));
        var result = await new HttpLlmRunner(new Configuration("http://orchestrator"), failing).RunAsync(new LlmRunRequest { Connection = "x", Instructions = "Decide." });
        Assert.AreEqual((false, LlmErrorCodes.UnknownConnection), (result.Succeeded, result.ErrorCode));

        using var broken = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway))));
        result = await new HttpLlmRunner(new Configuration("http://orchestrator"), broken).RunAsync(new LlmRunRequest { Connection = "x", Instructions = "Decide." });
        Assert.AreEqual(LlmErrorCodes.Unavailable, result.ErrorCode);

        result = await new HttpLlmRunner(new Configuration(null), broken).RunAsync(new LlmRunRequest { Connection = "x", Instructions = "Decide." });
        Assert.AreEqual(LlmErrorCodes.Unavailable, result.ErrorCode);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }

    private sealed class Configuration(string? orchestrator) : IRIoTConfigurationService
    {
        public string Id => "test";
        public string? OrchestratorBaseUrl { get; set; } = orchestrator;
        public string? WorkflowBaseUrl { get; set; }
        public string? WorkflowGrpcBaseUrl => "http://localhost:5003";
        public MqttConfiguration Mqtt { get; } = new();
    }
}
