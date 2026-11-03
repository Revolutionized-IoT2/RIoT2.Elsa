using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RIoT2.Core.Configuration;
using RIoT2.Core.Hosting;
using RIoT2.Core.Security;
using RIoT2.Elsa.Server.RIoT.Endpoints;
using RIoT2.Elsa.Server.RIoT.Services;

namespace RIoT2.Elsa.Tests;

/// <summary>The optional security mode's seams in Elsa (I34, design 7.5 phase S0).</summary>
[TestClass]
public class SecuritySeamTests
{
    [TestMethod]
    public void TheSecuritySettingsAreReadAndOnlyOffIsAccepted()
    {
        var options = WorkflowOptions.Load(Settings(new() { [SecurityOptions.ApiKeyVariable] = "elsa-key" }));
        Assert.AreEqual(SecurityMode.Off, options.Security.Mode);
        Assert.AreEqual("elsa-key", options.Security.ApiKey);

        var error = Assert.ThrowsExactly<ConfigurationValidationException>(() =>
            WorkflowOptions.Load(Settings(new() { [SecurityOptions.ModeVariable] = "on" })));
        StringAssert.Contains(error.Message, "RIOT2_SECURITY_MODE=on is not supported by this version");
    }

    [TestMethod]
    public void EveryRIoTEndpointHasItsPolicy()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRIoT2Security(new SecurityOptions());
        // Only registered, so the endpoints' parameters can be bound; never called.
        builder.Services.AddSingleton<RIoTTriggers>(_ => throw new NotSupportedException());
        builder.Services.AddScoped<WorkflowManagementService>(_ => throw new NotSupportedException());
        using var app = builder.Build();

        app.MapRIoTEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().ToList();
        Assert.IsEmpty(RIoT2Security.EndpointsWithoutPolicy(app.Services));
        string Policy(string method, string route) => endpoints
            .Single(e => e.RoutePattern.RawText == route && e.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()!.HttpMethods.Contains(method))
            .Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Single().Policy!;
        Assert.AreEqual(SecurityPolicies.Operator, Policy("POST", "/riot/trigger/{id}"));
        Assert.AreEqual(SecurityPolicies.Viewer, Policy("GET", "/riot/workflows/"));
        Assert.AreEqual(SecurityPolicies.Viewer, Policy("GET", "/riot/workflows/catalog"));
        Assert.AreEqual(SecurityPolicies.Viewer, Policy("GET", "/riot/workflows/runs"));
        Assert.AreEqual(SecurityPolicies.Operator, Policy("POST", "/riot/workflows/runs/{instanceId}/cancel"));
        Assert.AreEqual(SecurityPolicies.Admin, Policy("PUT", "/riot/workflows/{definitionId}"));
        Assert.AreEqual(SecurityPolicies.Admin, Policy("POST", "/riot/workflows/{definitionId}/publish"));
        Assert.AreEqual(SecurityPolicies.Admin, Policy("DELETE", "/riot/workflows/{definitionId}"));
        // Changes need Admin and reads Viewer, everywhere.
        foreach (var endpoint in endpoints.Where(e => e.RoutePattern.RawText!.StartsWith("/riot/workflows", StringComparison.Ordinal)))
        {
            var methods = endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()!.HttpMethods;
            var policy = endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Single().Policy;
            if (methods.Contains("GET"))
                Assert.AreEqual(SecurityPolicies.Viewer, policy, endpoint.RoutePattern.RawText);
            else if (!endpoint.RoutePattern.RawText!.EndsWith("/cancel", StringComparison.Ordinal))
                Assert.AreEqual(SecurityPolicies.Admin, policy, endpoint.RoutePattern.RawText);
        }
    }

    private static IConfiguration Settings(Dictionary<string, string?> extra)
    {
        var values = new Dictionary<string, string?>
        {
            [WorkflowOptions.IdVariable] = "workflow",
            [WorkflowOptions.UrlVariable] = "http://elsa:8080",
            [WorkflowOptions.GrpcUrlVariable] = "http://elsa:5003",
            [MqttOptions.HostVariable] = "mqtt.local"
        };
        foreach (var (key, value) in extra)
            values[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
