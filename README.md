# RIoT2.Elsa

Elsa 3 workflow automation for the [RIoT2](https://github.com/Revolutionized-IoT2) platform. This
repository contains the RIoT activities that connect workflows to orchestrator reports, variables
and commands, the *Run LLM* activity, and the workflow management service, all in the
`RIoT2.Elsa.Workflows` library, plus the separate workflow host that runs them in a container.

Since 2.0.0 there is no Elsa Studio: workflows are designed on RIoT2.UI's *Workflows* page, and by
default the orchestrator runs them itself with this library (`RIOT2_WORKFLOW_MODE=embedded`). The
container is for `RIOT2_WORKFLOW_MODE=remote` ([ADR 0016](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/adr/0016-integrated-workflows-and-llm.md)).

- Type: .NET class library (NuGet `RIoT2.Elsa.Workflows`) and ASP.NET Core application
- Target framework: .NET 10; Elsa 3.9
- Default branch: `master`
- Image: `ghcr.io/revolutionized-iot2/riot2-elsa`

How Elsa fits into the platform: [architecture overview](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/architecture/overview.md).
Elsa is the only RIoT2 automation engine: [ADR 0003](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/adr/0003-elsa-sole-automation-engine.md).

## Contents

| Path | Contents |
| --- | --- |
| `RIoT2.Elsa.Workflows/` | The library both hosts use (NuGet `RIoT2.Elsa.Workflows`): `AddRIoT2Workflows()` sets up Elsa with SQLite, the expression languages, the HTTP activities and the RIoT feature |
| `RIoT2.Elsa.Workflows/Activities/` | The RIoT activities: trigger, schedule, data, compare, threshold, debounce, throttle, wait for state, output, command with confirmation, notification, webhook, Run LLM, forecast, command plan |
| `RIoT2.Elsa.Workflows/Automation/` | What the activities share: value comparison, hysteresis, the state store, schedules and sunrise/sunset, `ScheduleService` |
| `RIoT2.Elsa.Workflows/Management/` | `WorkflowManagementService` (list, save, import, export, publish, runs) and the activity catalog the designer shows |
| `RIoT2.Elsa.Workflows/Llm/` | The Run LLM contract: `LlmRunRequest`, `LlmRunResult`, `ILlmRunner` (implemented by the orchestrator) |
| `RIoT2.Elsa.Workflows/Services/`, `UIHints/` | `IRIoTDataService` (implemented by each host), `RIoTTriggers`, trigger deduplication, metrics; the RIoT selector's settings for the designer |
| `RIoT2.Elsa.Server/` | The remote workflow host: MQTT presence, the RIoT endpoints, gRPC, and HTTP implementations of `IRIoTDataService` and `ILlmRunner` |
| `RIoT2.Elsa.Server/RIoT/Protos/riot.proto` | gRPC trigger contract used by the orchestrator |
| `RIoT2.Elsa.Tests/` | MSTest coverage for activities, schedules, the templates, the management service, Run LLM, split ports, command failures, gRPC behaviour and trigger deduplication |
| `templates/` | Workflow templates to import on the UI's *Workflows* page ([templates/README.md](templates/README.md)) |
| `Dockerfile` | Production image, web port 8080 (workflow API and `/health`) and gRPC port 5003 |

## RIoT integration

In the embedded mode the orchestrator gives the activities its data in process. The remote host
publishes itself as a workflow node on MQTT; when the orchestrator sends configuration, it stores the
orchestrator base URL and uses it for the activities:

- `RIoTTrigger` starts or resumes workflows for a selected report id.
- `RIoTData` reads a report, variable or command value from the orchestrator.
- `RIoTOutput` evaluates a JavaScript expression and submits a command through the orchestrator.
- "RIoT Command with confirmation" (`RIoTConfirmedCommand`) does the same with
  `POST /api/v2/commands` and `"wait": true`, and continues on `Done` or `Failed` once the device
  answered or the timeout (1 to 50 s) passed; `Status` and `Error` are its outputs.
- The activity pack (1.11.0): `RIoTSchedule` (a time of day, sunrise or sunset), `RIoTCompare`,
  `RIoTThreshold` (hysteresis), `RIoTDebounce`, `RIoTThrottle`, `RIoTWaitForState`,
  `RIoTNotification` (through a Messaging device) and `RIoTWebhook`. How to use them, and the
  templates: the [automation guide](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/guides/automation.md).
- `RIoTRunLlm` (2.0.0) sends instructions and input to a language model through a connection the
  orchestrator keeps, and continues on `Done` with the answer (text, or JSON checked against a
  schema) or on `Failed`. It may read values and send allowed commands within limits. The host calls
  `ILlmRunner`: in process in the orchestrator, `POST /api/llm/run` from the container.
- `RIoTForecast` (2.1.0) reads a window of a forecast report (`GET /api/v2/forecasts/{id}` from the
  container): its minimum, maximum, average, sum, current value, points and the lowest and highest
  N points, and continues on `Done`, or on `Unavailable` without a forecast or (by default) when it
  doesn't cover the window. `RIoTCommandPlan` (2.1.0) gives the orchestrator a schedule of a
  command's values (`POST /api/v2/command-plans`), which replaces the command's previous plan; empty
  steps end it. Run LLM's `PlanCommands` tool does the same for the model
  ([ADR 0019](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/adr/0019-forecasts-and-command-plans.md)).

The externally visible contracts are documented in the hub:

- [HTTP and gRPC APIs](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/http-api.md)
- [MQTT topics and payloads](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/mqtt-topics.md)
- [Environment variables, ports and volumes](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/env-vars.md)

## Configuration

The remote host reads these variables:

- `RIOT2_WORKFLOW_ID`
- `RIOT2_WORKFLOW_URL`
- `RIOT2_WORKFLOW_GRPC_URL`
- `RIOT2_WORKFLOW_GRPC_PORT` (optional, default `5003`)
- `RIOT2_MQTT_IP`
- `RIOT2_MQTT_USERNAME` and `RIOT2_MQTT_PASSWORD` (optional for anonymous brokers);
  `RIOT2_MQTT_TLS` and `RIOT2_MQTT_CA_FILE` (optional, TLS to the broker)
- `RIOT2_SECURITY_MODE` (optional, only `off` yet), `RIOT2_SECURITY_FEATURES`, `RIOT2_API_KEY` (sent
  to the orchestrator) and `RIOT2_CORS_ORIGINS`: the optional security mode ({D75})
- `RIOT2_LATITUDE` and `RIOT2_LONGITUDE` (optional, both or neither; decimal degrees): where
  `RIoTSchedule` calculates sunrise and sunset. Schedules use the time zone `TZ`.
- `RIOT2_LOG_FORMAT` (optional, `text` or `json`) and `RIOT2_LOG_LEVEL` (optional, default
  `Information`)
- `OTEL_EXPORTER_OTLP_ENDPOINT` (optional; the OTLP collector for metrics, such as
  `http://otel-collector:4317`) and `RIOT2_TRACING` (optional, `true` also exports traces)

Logs go to the console and to `Logs/RIoT2.log` (`/app/Logs` in the container), rolled daily and at
50 MB, 14 files kept, through `AddRIoT2Logging()` from `RIoT2.Core.Hosting`, the same way as the
other RIoT2 services. Lines logged while a gRPC trigger starts its workflow end with
`[MessageId=…]`, the id the orchestrator logs for that trigger.

Metrics go out through `AddRIoT2Telemetry()` when the endpoint is set: `riot2.workflow.triggers`
(received or duplicate, by transport), `riot2.workflow.faults` (by workflow definition), `riot2.mqtt.*`,
ASP.NET Core and the runtime. Every name is in the [metrics contract](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/metrics.md).

Use an externally reachable URL for `RIOT2_WORKFLOW_URL` and `RIOT2_WORKFLOW_GRPC_URL`. Do not use
`localhost` when the orchestrator runs in another container or on another machine.

Workflows are stored in `Data/elsa.sqlite.db`, or `/app/Data/elsa.sqlite.db` in the container.
Mount `/app/Data` for persistent workflows. A 1.x database (with Studio's identity tables) is
migrated on the first start and loads unchanged.

## Build, test and run

From the workspace root (`C:\Src\RIoT2`):

```powershell
dotnet restore .\RIoT2.Elsa\RIoT2.Elsa.sln
dotnet build .\RIoT2.Elsa\RIoT2.Elsa.sln
dotnet test .\RIoT2.Elsa\RIoT2.Elsa.Tests\RIoT2.Elsa.Tests.csproj
dotnet run --project .\RIoT2.Elsa\RIoT2.Elsa.Server\RIoT2.Elsa.Server.csproj
```

`GoldenMessageTests` read the [golden files](https://github.com/Revolutionized-IoT2/RIoT2.Core/blob/main/contracts/golden/README.md) from RIoT2.Core, which must be checked out next to this
repository (CI clones it).

The host serves the RIoT HTTP trigger at `POST /riot/trigger/{id}`, the workflow management API at
`/riot/workflows` (which the orchestrator's `/api/workflows` forwards to), the gRPC
`riot.RIoTTriggerService/Trigger` service on the dedicated HTTP/2 port, Elsa's HTTP endpoint
activities below `/api/workflows`, and health endpoints at `GET /health` and `GET /healthz`. It has no
UI and no login.

The orchestrator delivers triggers over gRPC through its durable outbox, at least once
([design 7.1](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/design/reliable-delivery.md)).
Each trigger carries its outbox id as `message_id`. `TriggerDeduplicator` remembers the ids of the
last ten minutes in memory: a repeated id is answered with success and not run again, a repeat that
arrives during the first run waits for it, and a failed run is forgotten so that its redelivery
runs. Requests without an id, such as `POST /riot/trigger/{id}`, always run. A request whose gRPC
deadline (at most the trigger's expiry) has passed on arrival is rejected with `DeadlineExceeded`.

## Docker

Build and run a local image. The GitHub Packages token is passed as a BuildKit secret, so it
never lands in an image layer:

```powershell
$env:NUGET_AUTH_TOKEN = '<github-packages-token>'
docker build --secret id=nuget_token,env=NUGET_AUTH_TOKEN -t riot2-elsa .\RIoT2.Elsa
docker run -p 8080:8080 -p 5003:5003 `
  -e RIOT2_MQTT_IP=192.168.0.30 `
  -e RIOT2_MQTT_USERNAME=user `
  -e RIOT2_MQTT_PASSWORD=<mqtt-password> `
  -e RIOT2_WORKFLOW_ID=<workflow-id> `
  -e RIOT2_WORKFLOW_URL=http://<host>:8080 `
  -e RIOT2_WORKFLOW_GRPC_URL=http://<host>:5003 `
  -v <host-data-directory>:/app/Data `
  riot2-elsa
```

The image runs as the non-root `app` user. Host directories mounted at `/app/Data` must be writable
by UID 1654.

## Versions and releases

- Release notes are in [CHANGELOG.md](CHANGELOG.md).
- To release, push a tag `x.y.z` on `master`. The release workflow runs the tests, publishes the
  `RIoT2.Elsa.Workflows` package to GitHub Packages, then the Docker image to GitHub Container
  Registry ([CI guide](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/guides/ci.md)).
  Release the package before the orchestrator that uses it.
- This repository references the `RIoT2.Core.Contracts`, `RIoT2.Core.Mqtt`, `RIoT2.Core.Http` and
  `RIoT2.Core.Hosting` packages `2.0.0` from GitHub Packages: the contract, the MQTT client and the
  outbound HTTP policy, without the device SDK, the node runtime or Quartz.

## Contributing

- Instructions for AI coding agents: [AGENTS.md](AGENTS.md).
- Platform documentation: [.github/docs](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/README.md).

## License

See [LICENSE.txt](LICENSE.txt).
