using System.Globalization;
using RIoT2.Core.Configuration;
using RIoT2.Elsa.Workflows.Automation;

namespace RIoT2.Elsa.Server.RIoT.Services;

/// <summary>
/// The workflow engine's settings. Every <c>RIOT2_*</c> name it reads is mapped
/// here (plan M4).
/// </summary>
public sealed class WorkflowOptions
{
    public const string IdVariable = "RIOT2_WORKFLOW_ID";
    public const string UrlVariable = "RIOT2_WORKFLOW_URL";
    public const string GrpcUrlVariable = "RIOT2_WORKFLOW_GRPC_URL";
    public const string GrpcPortVariable = "RIOT2_WORKFLOW_GRPC_PORT";
    public const string LatitudeVariable = GeoLocation.LatitudeVariable;
    public const string LongitudeVariable = GeoLocation.LongitudeVariable;
    public const int DefaultGrpcPort = 5003;

    /// <summary>Unique id; also the MQTT client id.</summary>
    public string Id { get; set; } = "";

    /// <summary>Web URL (the management API) as reachable from outside the container; advertised as <c>nodeBaseUrl</c>.</summary>
    public string Url { get; set; } = "";

    /// <summary>Externally reachable gRPC URL, including the published port; advertised as <c>grpcBaseUrl</c>.</summary>
    public string GrpcUrl { get; set; } = "";

    /// <summary>Port of the local plaintext HTTP/2 listener.</summary>
    public int GrpcPort { get; set; } = DefaultGrpcPort;

    /// <summary>
    /// <c>RIOT2_LATITUDE</c> and <c>RIOT2_LONGITUDE</c>, both or neither: where <c>RIoT Schedule</c> calculates sunrise and
    /// sunset. <c>null</c> when unset; sun-based schedules then don't fire.
    /// </summary>
    public GeoLocation? Location { get; set; }

    public MqttOptions Mqtt { get; set; } = new();

    /// <summary><c>RIOT2_LOG_FORMAT</c> and <c>RIOT2_LOG_LEVEL</c>, applied by <c>AddRIoT2Logging()</c> (design 7.4).</summary>
    public LoggingOptions Logging { get; set; } = new();

    /// <summary><c>OTEL_EXPORTER_OTLP_ENDPOINT</c> and <c>RIOT2_TRACING</c>, applied by <c>AddRIoT2Telemetry()</c> (design 7.4).</summary>
    public TelemetryOptions Telemetry { get; set; } = new();

    /// <summary>
    /// <c>RIOT2_SECURITY_MODE</c>, <c>RIOT2_SECURITY_FEATURES</c>, <c>RIOT2_API_KEY</c> and
    /// <c>RIOT2_CORS_ORIGINS</c> (design 7.5); this version accepts the mode <c>off</c> only.
    /// </summary>
    public SecurityOptions Security { get; set; } = new();

    /// <summary>
    /// Reads and validates the settings, and throws <see cref="ConfigurationValidationException"/>
    /// listing every problem (logged as critical) when any is missing or invalid.
    /// </summary>
    /// <param name="configuration">The host configuration, which includes the environment variables.</param>
    /// <param name="logger">Optional logger for the critical message.</param>
    public static WorkflowOptions Load(IConfiguration configuration, ILogger? logger = null)
    {
        var settings = new SettingsReader(configuration);
        var options = new WorkflowOptions
        {
            Id = settings.Required(IdVariable),
            Url = settings.RequiredHttpUrl(UrlVariable),
            GrpcUrl = settings.RequiredHttpUrl(GrpcUrlVariable),
            GrpcPort = settings.Integer(GrpcPortVariable, DefaultGrpcPort, 1, 65535),
            Mqtt = MqttOptions.Read(settings),
            Logging = LoggingOptions.Read(settings),
            Telemetry = TelemetryOptions.Read(settings),
            Security = SecurityOptions.Read(settings),
            Location = ReadLocation(settings)
        };
        settings.ThrowIfInvalid("workflow", logger);
        return options;
    }

    private static GeoLocation? ReadLocation(SettingsReader settings) => GeoLocation.Read(name => settings.Optional(name), settings.AddError);
}
