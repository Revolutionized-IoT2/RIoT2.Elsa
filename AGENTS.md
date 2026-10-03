# AGENTS.md — RIoT2.Elsa

Applies to: this repository. Read the platform guide first:
[.github/AGENTS.md](https://github.com/Revolutionized-IoT2/.github/blob/main/AGENTS.md). It covers the
workspace map, platform-wide rules and the documentation rules. In the local workspace, every
`https://github.com/Revolutionized-IoT2/<Repo>/blob/main/<path>` link is the file
`C:\Src\RIoT2\<Repo>\<path>`; read the local file instead of fetching the URL.

## What this is

An Elsa 3 workflow server and Studio for the RIoT2 platform. The server runs Elsa Workflows,
hosts the Blazor Studio client, publishes itself as a workflow node over MQTT, and receives report
triggers over HTTP and gRPC from the orchestrator. Elsa is the only automation engine in RIoT2.

## Commands

Run from the workspace root (`C:\Src\RIoT2`), in PowerShell:

```powershell
dotnet restore .\RIoT2.Elsa\RIoT2.Elsa.sln
dotnet build .\RIoT2.Elsa\RIoT2.Elsa.sln
dotnet test .\RIoT2.Elsa\RIoT2.Elsa.Tests\RIoT2.Elsa.Tests.csproj
dotnet run --project .\RIoT2.Elsa\RIoT2.Elsa.Server\RIoT2.Elsa.Server.csproj
```

- The run command starts the server and Studio. It requires the environment variables in
  [env-vars.md](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/env-vars.md)
  when used outside a local Development profile. Do not copy values from `launchSettings.json`.
- To build the container locally:
  `docker build -t riot2-elsa .\RIoT2.Elsa --build-arg NUGET_AUTH_TOKEN=<github-packages-token>`.
- To release, push a tag `x.y.z` on `master`. CI (`.github/workflows/docker.yml`) builds and
  pushes `ghcr.io/revolutionized-iot2/riot2-elsa:latest` and `:<tag>`.

## Layout

| Path | Contents |
|---|---|
| `RIoT2.Elsa.Server/Program.cs` | Elsa, identity, CORS, health, RIoT HTTP and gRPC wiring |
| `RIoT2.Elsa.Server/RIoT/Activities/` | `RIoTTrigger`, `RIoTData`, `RIoTOutput` workflow activities |
| `RIoT2.Elsa.Server/RIoT/Endpoints/RIoTEndpoints.cs` | `POST /riot/trigger/{id}` trigger endpoint |
| `RIoT2.Elsa.Server/RIoT/Services/` | MQTT, configuration, gRPC trigger, data and command services |
| `RIoT2.Elsa.Server/RIoT/Protos/riot.proto` | Elsa copy of the workflow trigger gRPC contract |
| `RIoT2.Elsa.Studio/` | Blazor WebAssembly Elsa Studio and RIoT picker UI |
| `RIoT2.Elsa.Tests/` | MSTest coverage for endpoint, activity and gRPC behaviours |
| `Dockerfile` | `net10.0` multi-stage image, web port 8080 and gRPC port 5003 |

## Contracts implemented here

- [http-api.md](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/http-api.md):
  `RIoT2.Elsa.Server/RIoT/Endpoints/RIoTEndpoints.cs`,
  `RIoT2.Elsa.Server/RIoT/Services/RIoTTriggerGrpcService.cs` and `Program.cs` implement
  `POST /riot/trigger/{id}`, gRPC `riot.RIoTTriggerService/Trigger`, `/health` and `/healthz`.
- [mqtt-topics.md](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/mqtt-topics.md):
  `WorkflowMqttService` subscribes to orchestrator online/configuration messages and announces
  `NodeType.Workflow` on `riot2/node/{id}/online`.
- [env-vars.md](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/env-vars.md):
  `RIoTConfigurationService`, `WorkflowEndpointConfiguration`, `Program.cs` and `Dockerfile`
  define the Elsa variables, web port 8080, gRPC port 5003 and `/app/Data` persistence path.

The proto file `RIoT2.Elsa.Server/RIoT/Protos/riot.proto` must keep the same package, service,
messages and field numbers as `RIoT2.Net.Orchestrator/Protos/riot_trigger.proto`. Only
`option csharp_namespace` differs, intentionally. Changes must be additive and must touch both
repositories.

## Rules

- Do not reintroduce the old internal rule engine, NCalc rules, rule models or rule UI. Elsa is
  the only automation engine ([ADR 0003](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/adr/0003-elsa-sole-automation-engine.md)).
- Keep the custom activities named and registered as `RIoTTrigger`, `RIoTData` and `RIoTOutput`.
- Keep workflow work asynchronous. Do not block on `.Result`, `.Wait()` or `Task.WaitAll`.
- Keep `RIOT2_WORKFLOW_GRPC_URL` required and absolute. The orchestrator needs this advertised
  endpoint to trigger workflows over the dedicated HTTP/2 listener.
- `ELSA_IDENTITY_SIGNING_KEY` is required outside Development. Development may generate an
  ephemeral signing key; production must use a strong secret from the environment or a secret
  store.
- Keep `/health` and `/healthz` anonymous and lightweight.
- Do not copy hub contract tables into this repository. Link to the hub and update the hub only
  when a contract changes.
- Keep `RIoT2.Core` as a package reference. This repository currently pins `RIoT2.Core` `0.1.41`.

## Pitfalls

- Plaintext HTTP/2 gRPC is on a dedicated listener. Do not try to serve Studio, REST and gRPC from
  one plaintext `Http1AndHttp2` port.
- Docker exposes web/Studio on 8080 and gRPC on 5003. `RIOT2_WORKFLOW_URL` and
  `RIOT2_WORKFLOW_GRPC_URL` must be reachable by other containers or devices, not `localhost`.
- `WorkflowMqttService` announces both `NodeBaseUrl` and `GrpcBaseUrl` when connected and when the
  retained orchestrator-online message arrives. Install handlers before subscribing.
- Workflow and identity state lives in `Data/elsa.sqlite.db` (`/app/Data/elsa.sqlite.db` in the
  image). Bind-mounted data must be writable by UID 1654.
- `Program.cs` still uses Elsa `UseAdminUserProvider`, permissive CORS and disabled antiforgery.
  That is intentional until optional security mode work lands; do not silently tighten it in an
  unrelated change.
- `RIoT2.Core` `0.1.41` has no tag in `RIoT2.Core`; only `0.1.39`, `0.1.43` and `0.1.44` are
  tagged around it. Treat this as maintainer action
  [MA2](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/backlog/README.md#ma2-cut-a-core-release-and-align-all-consumers).

## Related work

- [Backlog item 2](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/backlog/open-issues.md):
  report triggers to Elsa are not durably delivered today.
- [Backlog item 14](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/backlog/open-issues.md):
  service configuration is still hand-read from environment variables.
- [Backlog item 17](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/backlog/open-issues.md):
  cross-repository contract and integration tests.
- [S7](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/backlog/optional-hardening.md):
  optional Elsa hardening for identity, CORS and antiforgery.
- [M4](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/plans/m04-typed-configuration.md):
  typed validated configuration.
- [M7](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/plans/m07-contract-integration-tests.md):
  contract and integration test plan.
- [M8](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/plans/m08-dotnet10-migration.md):
  .NET 10 alignment; Elsa already targets `net10.0`.
- [Design 7.1](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/design/reliable-delivery.md):
  workflow outbox and trigger `message_id` deduplication.
