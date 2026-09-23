namespace RIoT2.Elsa.Server.RIoT.Services;

public static class WorkflowEndpointConfiguration
{
    public static void Configure(IConfiguration configuration)
    {
        var grpcPort = configuration.GetValue("RIOT2_WORKFLOW_GRPC_PORT", 5003);
        if (grpcPort is < 1 or > 65535)
            throw new InvalidOperationException("RIOT2_WORKFLOW_GRPC_PORT must be between 1 and 65535.");

        // Explicit Kestrel endpoints override URL bindings, so carry existing web bindings forward.
        var endpoints = configuration.GetSection("Kestrel:Endpoints").GetChildren().ToArray();
        if (!endpoints.Any(endpoint => endpoint.Key != "WorkflowGrpc"))
        {
            var urls = configuration["urls"];
            if (string.IsNullOrWhiteSpace(urls))
            {
                var http = ExpandPorts(configuration["http_ports"], "http");
                var https = ExpandPorts(configuration["https_ports"], "https");
                urls = string.Join(";", http.Concat(https));
            }
            if (string.IsNullOrWhiteSpace(urls))
                urls = "http://localhost:5001";

            var bindings = urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var i = 0; i < bindings.Length; i++)
            {
                configuration[$"Kestrel:Endpoints:Web{i}:Url"] = bindings[i];
                configuration[$"Kestrel:Endpoints:Web{i}:Protocols"] =
                    bindings[i].StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? "Http1AndHttp2" : "Http1";
            }
        }

        configuration["Kestrel:Endpoints:WorkflowGrpc:Url"] ??= $"http://0.0.0.0:{grpcPort}";
        configuration["Kestrel:Endpoints:WorkflowGrpc:Protocols"] = "Http2";
    }

    private static IEnumerable<string> ExpandPorts(string? ports, string scheme) =>
        (ports ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(port => $"{scheme}://*:{port}");
}
