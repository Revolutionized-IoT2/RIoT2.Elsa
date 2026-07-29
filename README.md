# RIoT2.Elsa

RIoT2.Elsa integrates [Elsa Workflows 3](https://elsa-workflows.github.io/elsa-documentation/) into the [RIoT2](https://github.com/Revolutionized-IoT2) IoT platform, allowing IoT events, data, and commands from RIoT2 to be orchestrated through visual, low-code workflows.

## Solution structure

| Project | Description |
|---|---|
| [RIoT2.Elsa.Server](RIoT2.Elsa.Server) | ASP.NET Core host that runs the Elsa workflow engine and runtime, exposes the Elsa Workflows API, hosts the Elsa Studio Blazor WebAssembly client, and provides the RIoT-specific activities, endpoints, and services described below. |
| [RIoT2.Elsa.Studio](RIoT2.Elsa.Studio) | Blazor WebAssembly client (Elsa Studio) used to design, manage, and monitor workflows in the browser. Includes RIoT-specific UI providers/components for selecting RIoT triggers, data, and commands in the workflow designer. |

## RIoT integration (`RIoT2.Elsa.Server/RIoT`)

The `RIoT` folder adds a custom Elsa feature (`RIoTFeature`) that connects Elsa workflows to a RIoT2 orchestrator over MQTT:

- **Activities**
  - `RIoTTrigger` – starts/resumes a workflow when a selected RIoT event occurs.
  - `RIoTData` – reads a RIoT report, variable, or command value into the workflow.
  - `RIoTOutput` – sends a command back out to RIoT2 (evaluated as JavaScript).
- **Endpoints** – `RIoTEndpoints.MapRIoTEndpoints` exposes `POST /riot/trigger/{id}` to trigger/resume workflows externally. The same operation is also available over gRPC via `RIoTTriggerService.Trigger` (see [gRPC](#grpc) below).
- **Services**
  - `RIoTConfigurationService` – reads RIoT/MQTT configuration from environment variables.
  - `WorkflowMqttService` / `MqttBackgroundService` – connects to the MQTT broker and relays messages to/from workflows.
  - `RIoTDataService` – fetches report/variable/command values from the RIoT2 orchestrator.
  - `RIoTTriggerGrpcService` – gRPC implementation of the trigger operation, sharing the same logic as the REST endpoint.
- **UI Hints** – dropdown providers used by the Elsa Studio designer so users can pick RIoT triggers, data sources, and commands from the RIoT2 orchestrator's templates.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Access to a running RIoT2 orchestrator and MQTT broker (for full functionality)

## Configuration

The server is configured primarily through environment variables (see [RIoT2.Elsa.Server/Properties/launchSettings.json](RIoT2.Elsa.Server/Properties/launchSettings.json) and the [Dockerfile](Dockerfile)):

| Variable | Description |
|---|---|
| `RIOT2_WORKFLOW_ID` | Identifier for this workflow host instance. |
| `RIOT2_WORKFLOW_URL` | Base URL of this workflow host, used by RIoT2 to call back. |
| `RIOT2_MQTT_IP` | Hostname/IP of the MQTT broker. |
| `RIOT2_MQTT_USERNAME` | MQTT username. |
| `RIOT2_MQTT_PASSWORD` | MQTT password. |

Additional settings (Elsa HTTP options, logging) are configured in [RIoT2.Elsa.Server/appsettings.json](RIoT2.Elsa.Server/appsettings.json).

Workflow and identity data is persisted to a local SQLite database at `Data/elsa.sqlite.db`.

## Getting started

Restore, build, and run the server from the repository root:

```powershell
dotnet restore RIoT2.Elsa.sln
dotnet build RIoT2.Elsa.sln
dotnet run --project RIoT2.Elsa.Server
```

By default the server listens on `http://localhost:5001` (see `launchSettings.json`). Once running:

- Elsa Studio (workflow designer) is served from the root of the site.
- The Elsa Workflows API is available under `/api/workflows` (base path configurable via the `Http:BasePath` setting).
- The RIoT trigger endpoint is available at `POST /riot/trigger/{id}`.
- The same trigger operation is available over gRPC (see [gRPC](#grpc) below).

## gRPC

In addition to the REST endpoint, RIoT events can be triggered over gRPC using the contract defined in [RIoT2.Elsa.Server/RIoT/Protos/riot.proto](RIoT2.Elsa.Server/RIoT/Protos/riot.proto):

```protobuf
service RIoTTriggerService {
  rpc Trigger (TriggerRequest) returns (TriggerResponse);
}

message TriggerRequest {
  string id = 1;   // matches the {id} route parameter / RIoT event id
  string data = 2; // JSON-encoded event payload
}

message TriggerResponse {
  bool success = 1;
}
```

The gRPC service (`RIoTTriggerGrpcService`) is mapped on the same Kestrel endpoint as the REST API and Elsa Studio. Kestrel is configured for `Http1AndHttp2` so gRPC (HTTP/2) works over the same plaintext port used for HTTP/1.1 traffic, without requiring TLS.

## Running with Docker

A multi-stage [Dockerfile](Dockerfile) is provided to build and run the server:

```powershell
docker build -t riot2-elsa .
docker run -p 8080:80 `
  -e RIOT2_MQTT_IP=192.168.0.30 `
  -e RIOT2_MQTT_USERNAME=user `
  -e RIOT2_MQTT_PASSWORD=password `
  -e RIOT2_WORKFLOW_ID=<workflow-id> `
  -e RIOT2_WORKFLOW_URL=http://<host>:8080 `
  riot2-elsa
```

Mount a volume to `/app/Data` to persist the SQLite database across container restarts.

## License

This project is licensed under the [MIT License](LICENSE.txt).
