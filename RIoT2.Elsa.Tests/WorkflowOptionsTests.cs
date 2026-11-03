using Microsoft.Extensions.Configuration;
using RIoT2.Core.Configuration;
using RIoT2.Elsa.Server.RIoT.Services;

namespace RIoT2.Elsa.Tests;

[TestClass]
public class WorkflowOptionsTests
{
    [TestMethod]
    public void ValidSettingsAreReadAndUsedByTheConfigurationService()
    {
        var options = WorkflowOptions.Load(Settings(ValidSettings()), isDevelopment: false);
        var configuration = new RIoTConfigurationService(Microsoft.Extensions.Options.Options.Create(options));

        Assert.AreEqual("workflow", configuration.Id);
        Assert.AreEqual("http://elsa:8080", configuration.WorkflowBaseUrl);
        Assert.AreEqual("http://elsa:5003", configuration.WorkflowGrpcBaseUrl);
        Assert.AreEqual("workflow", configuration.Mqtt.ClientId);
        Assert.AreEqual("mqtt.local", configuration.Mqtt.ServerUrl);
        Assert.AreEqual(1883, configuration.Mqtt.Port);
        Assert.IsNull(configuration.Mqtt.Username, "No user means an anonymous MQTT connection.");
        Assert.AreEqual(WorkflowOptions.DefaultGrpcPort, options.GrpcPort);
        Assert.AreEqual("key", options.IdentitySigningKey);
    }

    [TestMethod]
    public void EveryMissingSettingIsReportedAtOnce()
    {
        var error = Assert.ThrowsExactly<ConfigurationValidationException>(() =>
            WorkflowOptions.Load(Settings([]), isDevelopment: false));

        Assert.AreEqual("Invalid workflow configuration: RIOT2_WORKFLOW_ID is required; RIOT2_WORKFLOW_URL is required; " +
            "RIOT2_WORKFLOW_GRPC_URL is required; RIOT2_MQTT_IP is required; " +
            "ELSA_IDENTITY_SIGNING_KEY is required outside Development.", error.Message);
    }

    [TestMethod]
    public void DevelopmentRunsWithoutASigningKey()
    {
        var values = ValidSettings();
        values.Remove(WorkflowOptions.IdentitySigningKeyVariable);

        Assert.IsNull(WorkflowOptions.Load(Settings(values), isDevelopment: true).IdentitySigningKey);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("65536")]
    [DataRow("grpc")]
    public void InvalidGrpcPortIsRejected(string port)
    {
        var values = ValidSettings();
        values[WorkflowOptions.GrpcPortVariable] = port;

        var error = Assert.ThrowsExactly<ConfigurationValidationException>(() => WorkflowOptions.Load(Settings(values), isDevelopment: false));

        CollectionAssert.AreEqual(new[] { "RIOT2_WORKFLOW_GRPC_PORT must be a whole number from 1 to 65535" }, error.Errors.ToArray());
    }

    [TestMethod]
    public void GrpcUrlMustBeAnAbsoluteHttpUrl()
    {
        var values = ValidSettings();
        values[WorkflowOptions.GrpcUrlVariable] = "elsa:5003";

        var error = Assert.ThrowsExactly<ConfigurationValidationException>(() => WorkflowOptions.Load(Settings(values), isDevelopment: false));

        CollectionAssert.AreEqual(new[] { "RIOT2_WORKFLOW_GRPC_URL must be an absolute http or https URL" }, error.Errors.ToArray());
    }

    [TestMethod]
    public void TheLocationIsReadInInvariantDegreesAndBothAreNeeded()
    {
        var values = ValidSettings();
        Assert.IsNull(WorkflowOptions.Load(Settings(values), isDevelopment: false).Location);
        values[WorkflowOptions.LatitudeVariable] = "60.17";
        values[WorkflowOptions.LongitudeVariable] = "-24.94";
        Assert.AreEqual(new RIoT2.Elsa.Server.RIoT.Automation.GeoLocation(60.17, -24.94), WorkflowOptions.Load(Settings(values), isDevelopment: false).Location);

        values[WorkflowOptions.LatitudeVariable] = "60,17";
        values[WorkflowOptions.LongitudeVariable] = "181";
        var error = Assert.ThrowsExactly<ConfigurationValidationException>(() => WorkflowOptions.Load(Settings(values), isDevelopment: false));
        CollectionAssert.AreEqual(new[]
        {
            "RIOT2_LATITUDE must be a number of degrees from -90 to 90, with a decimal point",
            "RIOT2_LONGITUDE must be a number of degrees from -180 to 180, with a decimal point"
        }, error.Errors.ToArray());

        values.Remove(WorkflowOptions.LongitudeVariable);
        values[WorkflowOptions.LatitudeVariable] = "60";
        error = Assert.ThrowsExactly<ConfigurationValidationException>(() => WorkflowOptions.Load(Settings(values), isDevelopment: false));
        CollectionAssert.AreEqual(new[] { "RIOT2_LATITUDE and RIOT2_LONGITUDE must be set together" }, error.Errors.ToArray());
    }

    private static IConfiguration Settings(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static Dictionary<string, string?> ValidSettings() => new()
    {
        [WorkflowOptions.IdVariable] = "workflow",
        [WorkflowOptions.UrlVariable] = "http://elsa:8080",
        [WorkflowOptions.GrpcUrlVariable] = "http://elsa:5003",
        [WorkflowOptions.IdentitySigningKeyVariable] = "key",
        [MqttOptions.HostVariable] = "mqtt.local"
    };
}
