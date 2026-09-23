using System.Net;
using System.Net.Http;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RIoT2.Core.Models;
using RIoT2.Core.Utils;
using RIoT2.Elsa.Server.RIoT.Activities;
using RIoT2.Elsa.Server.RIoT.Services;
using RIoT2.Elsa.Server.RIoT.Services.Interfaces;
using RIoT2.Elsa.Studio.Models;
using Template = RIoT2.Elsa.Server.RIoT.Models.Template;

namespace RIoT2.Elsa.Tests;

[TestClass]
public class WorkflowIntegrationTests
{
    [TestMethod]
    public async Task ConfiguredSeparateEndpointsServeHttp1AndPlaintextHttp2()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Configuration["urls"] = "http://127.0.0.1:0";
        builder.Configuration["Kestrel:Endpoints:WorkflowGrpc:Url"] = "http://127.0.0.1:0";
        WorkflowEndpointConfiguration.Configure(builder.Configuration);
        await using var app = builder.Build();
        app.MapGet("/", () => "ok");
        await app.StartAsync();
        try
        {
            var endpoints = app.Urls.ToArray();
            Assert.AreEqual(2, endpoints.Length);
            using var client = new HttpClient();
            var versions = new List<Version>();
            foreach (var endpoint in endpoints)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, endpoint)
                {
                    Version = HttpVersion.Version20,
                    VersionPolicy = HttpVersionPolicy.RequestVersionExact
                };
                try
                {
                    using var response = await client.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    versions.Add(response.Version);
                }
                catch (HttpRequestException)
                {
                    using var response = await client.GetAsync(endpoint);
                    response.EnsureSuccessStatusCode();
                    versions.Add(response.Version);
                }
            }
            CollectionAssert.AreEquivalent(new[] { HttpVersion.Version11, HttpVersion.Version20 }, versions);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [TestMethod]
    public void EndpointConfigurationPreservesContainerAndExplicitWebBindings()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["http_ports"] = "80;8080",
            ["RIOT2_WORKFLOW_GRPC_PORT"] = "5004"
        }).Build();
        WorkflowEndpointConfiguration.Configure(configuration);
        Assert.AreEqual("http://*:80", configuration["Kestrel:Endpoints:Web0:Url"]);
        Assert.AreEqual("http://*:8080", configuration["Kestrel:Endpoints:Web1:Url"]);
        Assert.AreEqual("http://0.0.0.0:5004", configuration["Kestrel:Endpoints:WorkflowGrpc:Url"]);
        Assert.AreEqual("Http2", configuration["Kestrel:Endpoints:WorkflowGrpc:Protocols"]);
        WorkflowEndpointConfiguration.Configure(configuration);
        Assert.AreEqual(3, configuration.GetSection("Kestrel:Endpoints").GetChildren().Count());
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(65536)]
    public void InvalidGrpcPortFailsConfiguration(int port)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RIOT2_WORKFLOW_GRPC_PORT"] = port.ToString()
        }).Build();

        Assert.ThrowsException<InvalidOperationException>(() => WorkflowEndpointConfiguration.Configure(configuration));
    }

    [TestMethod]
    public void ExplicitWebEndpointIsNotReplacedByUrlBindings()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["urls"] = "http://localhost:1234",
            ["Kestrel:Endpoints:Studio:Url"] = "https://localhost:8443",
            ["Kestrel:Endpoints:Studio:Protocols"] = "Http1AndHttp2"
        }).Build();

        WorkflowEndpointConfiguration.Configure(configuration);

        Assert.AreEqual(2, configuration.GetSection("Kestrel:Endpoints").GetChildren().Count());
        Assert.AreEqual("https://localhost:8443", configuration["Kestrel:Endpoints:Studio:Url"]);
        Assert.AreEqual("Http1AndHttp2", configuration["Kestrel:Endpoints:Studio:Protocols"]);
        Assert.AreEqual("Http2", configuration["Kestrel:Endpoints:WorkflowGrpc:Protocols"]);
    }

    [TestMethod]
    [DataRow(400)]
    [DataRow(500)]
    public async Task RejectedCommandFailsRatherThanCompletingSuccessfully(int status)
    {
        using var client = new HttpClient(new Handler((_, _) =>
            Task.FromResult(new HttpResponseMessage((HttpStatusCode)status))));
        var service = new RIoTDataService(new TestConfiguration(), client);

        await Assert.ThrowsExceptionAsync<HttpRequestException>(() => service.ExecuteCommandAsync("lamp", true));
    }

    [TestMethod]
    public async Task CommandPreservesPayloadAndObservesCancellation()
    {
        Command? received = null;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new HttpClient(new Handler(async (request, cancellationToken) =>
        {
            Assert.AreEqual("/api/command/execute", request.RequestUri!.AbsolutePath);
            received = Json.Deserialize<Command>(await request.Content!.ReadAsStringAsync(cancellationToken));
            entered.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        var service = new RIoTDataService(new TestConfiguration(), client);
        using var cancellation = new CancellationTokenSource();

        var execution = service.ExecuteCommandAsync("lamp", new { on = true }, cancellation.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        try
        {
            await execution;
            Assert.Fail("The cancelled command should not succeed.");
        }
        catch (OperationCanceledException) { }
        Assert.AreEqual("lamp", received!.Id);
        Assert.IsTrue(received.Value.GetValue<bool>("on"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task OutputActivityWaitsForCommandCompletionAndRecordsFailure(bool fail)
    {
        var data = new BlockingDataService { Fail = fail };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddElsa(elsa => elsa.UseJavaScript().AddActivitiesFrom<RIoTOutput>());
        services.AddSingleton<IRIoTDataService>(data);
        await using var provider = services.BuildServiceProvider();
        var runner = provider.GetRequiredService<IWorkflowRunner>();
        var activity = new RIoTOutput
        {
            Command = new Input<RIoTTemplateItem>(new RIoTTemplateItem { Id = "lamp", Value = "return true;" })
        };

        var execution = runner.RunAsync(activity);
        var first = await Task.WhenAny(data.Entered.Task, execution).WaitAsync(TimeSpan.FromSeconds(10));
        if (first == execution)
            Assert.Fail(string.Join(Environment.NewLine, (await execution).WorkflowState.Incidents.Select(x => x.Message + " " + x.Exception?.Message)));
        Assert.IsFalse(execution.IsCompleted, "Elsa must await the output command.");
        data.Release.TrySetResult();
        var result = await execution.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.AreEqual(fail ? 1 : 0, result.WorkflowState.Incidents.Count);
        if (fail)
            Assert.IsTrue(result.WorkflowState.Incidents.Single().Message.Contains("Injected command failure"));
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }

    private sealed class TestConfiguration : IRIoTConfigurationService
    {
        public string Id => "test";
        public string? OrchestratorBaseUrl { get; set; } = "http://localhost";
        public string? WorkflowBaseUrl { get; set; }
        public string? WorkflowGrpcBaseUrl => "http://localhost:5003";
        public string MQTT_Password => "";
        public string MQTT_Username => "";
        public string MQTT_ServerUrl => "";
    }

    private sealed class BlockingDataService : IRIoTDataService
    {
        public bool Fail { get; init; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task ExecuteCommandAsync(string id, object? data, CancellationToken cancellationToken = default)
        {
            Assert.AreEqual("lamp", id);
            Assert.AreEqual(true, data);
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            if (Fail)
                throw new HttpRequestException("Injected command failure");
        }
        public Task<List<Template>> GetReportTemplatesAsync() => throw new NotSupportedException();
        public Task<List<Template>> GetCommandTemplatesAsync() => throw new NotSupportedException();
        public Task<List<Template>> GetVariableTemplatesAsync() => throw new NotSupportedException();
        public Task<object> GetReportValueAsync(string reportId) => throw new NotSupportedException();
        public Task<object> GetCommandValueAsync(string commandId) => throw new NotSupportedException();
        public Task<object> GetVariableValueAsync(string variableId) => throw new NotSupportedException();
    }
}
