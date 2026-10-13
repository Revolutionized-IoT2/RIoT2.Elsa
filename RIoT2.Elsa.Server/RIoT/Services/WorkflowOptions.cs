using RIoT2.Core.Configuration;

namespace RIoT2.Elsa.Server.RIoT.Services;

/// <summary>
/// The workflow engine's settings. Every <c>RIOT2_*</c> and <c>ELSA_*</c> name it reads is mapped
/// here (plan M4).
/// </summary>
public sealed class WorkflowOptions
{
    public const string IdVariable = "RIOT2_WORKFLOW_ID";
    public const string UrlVariable = "RIOT2_WORKFLOW_URL";
    public const string GrpcUrlVariable = "RIOT2_WORKFLOW_GRPC_URL";
    public const string GrpcPortVariable = "RIOT2_WORKFLOW_GRPC_PORT";
    public const string IdentitySigningKeyVariable = "ELSA_IDENTITY_SIGNING_KEY";
    public const int DefaultGrpcPort = 5003;

    /// <summary>Unique id; also the MQTT client id.</summary>
    public string Id { get; set; } = "";

    /// <summary>Studio and web URL as reachable from outside the container; advertised as <c>nodeBaseUrl</c>.</summary>
    public string Url { get; set; } = "";

    /// <summary>Externally reachable gRPC URL, including the published port; advertised as <c>grpcBaseUrl</c>.</summary>
    public string GrpcUrl { get; set; } = "";

    /// <summary>Port of the local plaintext HTTP/2 listener.</summary>
    public int GrpcPort { get; set; } = DefaultGrpcPort;

    /// <summary>Signing key for Elsa identity tokens; <c>null</c> only in Development.</summary>
    public string? IdentitySigningKey { get; set; }

    public MqttOptions Mqtt { get; set; } = new();

    /// <summary>
    /// Reads and validates the settings, and throws <see cref="ConfigurationValidationException"/>
    /// listing every problem (logged as critical) when any is missing or invalid.
    /// </summary>
    /// <param name="configuration">The host configuration, which includes the environment variables.</param>
    /// <param name="isDevelopment">In Development the signing key may be missing; Program then generates one.</param>
    /// <param name="logger">Optional logger for the critical message.</param>
    public static WorkflowOptions Load(IConfiguration configuration, bool isDevelopment, ILogger? logger = null)
    {
        var settings = new SettingsReader(configuration);
        var options = new WorkflowOptions
        {
            Id = settings.Required(IdVariable),
            Url = settings.RequiredHttpUrl(UrlVariable),
            GrpcUrl = settings.RequiredHttpUrl(GrpcUrlVariable),
            GrpcPort = settings.Integer(GrpcPortVariable, DefaultGrpcPort, 1, 65535),
            IdentitySigningKey = settings.Optional(IdentitySigningKeyVariable),
            Mqtt = MqttOptions.Read(settings)
        };
        if (!isDevelopment && string.IsNullOrWhiteSpace(options.IdentitySigningKey))
            settings.AddError($"{IdentitySigningKeyVariable} is required outside Development");
        settings.ThrowIfInvalid("workflow", logger);
        return options;
    }
}
