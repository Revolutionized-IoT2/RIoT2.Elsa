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
        var options = WorkflowOptions.Load(Settings(new() { [SecurityOptions.ApiKeyVariable] = "elsa-key" }), isDevelopment: false);
        Assert.AreEqual(SecurityMode.Off, options.Security.Mode);
        Assert.AreEqual("elsa-key", options.Security.ApiKey);

        var error = Assert.ThrowsExactly<ConfigurationValidationException>(() =>
            WorkflowOptions.Load(Settings(new() { [SecurityOptions.ModeVariable] = "on" }), isDevelopment: false));
        StringAssert.Contains(error.Message, "RIOT2_SECURITY_MODE=on is not supported by this version");
    }

    [TestMethod]
    public void TheTriggerEndpointHasTheOperatorPolicy()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRIoT2Security(new SecurityOptions(), defaultScheme: false);
        // Only registered, so the endpoint's parameters can be bound; never called.
        builder.Services.AddSingleton<global::Elsa.Workflows.Runtime.IStimulusSender>(_ => throw new NotSupportedException());
        using var app = builder.Build();

        app.MapRIoTEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).ToList();
        Assert.HasCount(1, endpoints);
        Assert.IsEmpty(RIoT2Security.EndpointsWithoutPolicy(app.Services));
        var policy = endpoints.Single().Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Single().Policy;
        Assert.AreEqual(SecurityPolicies.Operator, policy);
    }

    private static IConfiguration Settings(Dictionary<string, string?> extra)
    {
        var values = new Dictionary<string, string?>
        {
            [WorkflowOptions.IdVariable] = "workflow",
            [WorkflowOptions.UrlVariable] = "http://elsa:8080",
            [WorkflowOptions.GrpcUrlVariable] = "http://elsa:5003",
            [WorkflowOptions.IdentitySigningKeyVariable] = "key",
            [MqttOptions.HostVariable] = "mqtt.local"
        };
        foreach (var (key, value) in extra)
            values[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
