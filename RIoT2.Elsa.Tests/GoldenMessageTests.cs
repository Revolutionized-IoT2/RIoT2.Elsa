using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using RIoT2.Core.Models;
using RIoT2.Elsa.Server.RIoT.Models;
using RIoT2.Elsa.Server.RIoT.Services;
using RIoT2.Elsa.Server.RIoT.Services.Interfaces;

namespace RIoT2.Elsa.Tests;

/// <summary>
/// Reads the orchestrator responses and writes the command format defined by the golden files in
/// RIoT2.Core/contracts/golden (plan M7 step 3). CI clones RIoT2.Core next to this repository.
/// </summary>
[TestClass]
public class GoldenMessageTests
{
    private static readonly string _golden = FindGolden();

    [TestMethod]
    public async Task ReadsTheGoldenReportValue()
    {
        var service = Serve("/api/report/golden-report-number/value", "http/report-value.json", out _);

        var report = (ElsaReport)await service.GetReportValueAsync("golden-report-number");

        Assert.AreEqual("golden-report-number", report.Id);
        Assert.AreEqual(1760000000, report.TimeStamp);
        Assert.AreEqual(21.5, Convert.ToDouble(report.Value));
    }

    [TestMethod]
    public async Task ReadsTheGoldenCommandValue()
    {
        var service = Serve("/api/command/golden-command-boolean/value", "http/command-value.json", out _);

        var command = (Command)await service.GetCommandValueAsync("golden-command-boolean");

        Assert.AreEqual("golden-command-boolean", command.Id);
        Assert.IsTrue(command.Value.GetValue<bool>());
    }

    [TestMethod]
    public async Task ReadsTheGoldenVariableValue()
    {
        var service = Serve("/api/variable/ba0fa3d9-e089-438e-b06e-a62de71d254d/value", "http/variable-value.json", out _);

        var variable = (Variable)await service.GetVariableValueAsync("ba0fa3d9-e089-438e-b06e-a62de71d254d");

        Assert.AreEqual("entity test", variable.Name);
        Assert.AreEqual(Core.ValueType.Entity, variable.Type);
        Assert.AreEqual(123, variable.Model.GetValue<int>("test"));
    }

    [TestMethod]
    [DataRow("boolean", "true")]
    [DataRow("number", "21.5")]
    [DataRow("text", "\"Living room\"")]
    [DataRow("textarray", "[\"red\",\"green\",\"blue\"]")]
    [DataRow("entity", "{\"temperature\":21.5,\"humidity\":40,\"open\":true,\"location\":{\"room\":\"Kitchen\"}}")]
    public async Task PostsCommandsInTheGoldenFormat(string type, string value)
    {
        var service = Serve("/api/command/execute", null, out var posted);

        await service.ExecuteCommandAsync($"golden-command-{type}", JsonNode.Parse(value)!.Deserialize<object>());

        var expected = JsonNode.Parse(File.ReadAllText(Path.Combine(_golden, "messages", $"command-{type}.json")));
        Assert.IsTrue(JsonNode.DeepEquals(expected, JsonNode.Parse(posted.Body!)), $"Posted {posted.Body}");
    }

    private static RIoTDataService Serve(string path, string? goldenFile, out Request request)
    {
        var captured = new Request();
        request = captured;
        var client = new HttpClient(new Handler(async message =>
        {
            Assert.AreEqual(path, message.RequestUri!.AbsolutePath);
            if (message.Content != null)
                captured.Body = await message.Content.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(goldenFile == null ? "" : File.ReadAllText(Path.Combine(_golden, goldenFile)))
            };
        }));
        return new RIoTDataService(new TestConfiguration(), client);
    }

    private static string FindGolden()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "RIoT2.Core", "contracts", "golden");
            if (Directory.Exists(candidate))
                return candidate;
        }

        throw new DirectoryNotFoundException("Clone RIoT2.Core next to RIoT2.Elsa; its contracts/golden folder holds the golden files.");
    }

    private sealed class Request
    {
        public string? Body { get; set; }
    }

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request);
    }

    private sealed class TestConfiguration : IRIoTConfigurationService
    {
        public string Id => "test";
        public string? OrchestratorBaseUrl { get; set; } = "http://localhost";
        public string? WorkflowBaseUrl { get; set; }
        public string? WorkflowGrpcBaseUrl => "http://localhost:5003";
        public RIoT2.Core.Models.MqttConfiguration Mqtt { get; } = new();
    }
}
