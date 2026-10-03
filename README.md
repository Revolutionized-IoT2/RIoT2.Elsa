# RIoT2.Elsa

Elsa 3 workflow automation for the [RIoT2](https://github.com/Revolutionized-IoT2) platform. This
repository contains the workflow server, the Elsa Studio browser UI, and the RIoT-specific
activities that connect workflows to orchestrator reports, variables and commands.

- Type: ASP.NET Core / Blazor WebAssembly application
- Target framework: .NET 10
- Default branch: `master`
- Image: `ghcr.io/revolutionized-iot2/riot2-elsa`

How Elsa fits into the platform: [architecture overview](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/architecture/overview.md).
Elsa is the only RIoT2 automation engine: [ADR 0003](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/adr/0003-elsa-sole-automation-engine.md).

## Contents

| Path | Contents |
| --- | --- |
| `RIoT2.Elsa.Server/` | ASP.NET Core host for Elsa Workflows, Studio assets, RIoT endpoints and services |
| `RIoT2.Elsa.Server/RIoT/Activities/` | `RIoTTrigger`, `RIoTData` and `RIoTOutput` |
| `RIoT2.Elsa.Server/RIoT/Protos/riot.proto` | gRPC trigger contract used by the orchestrator |
| `RIoT2.Elsa.Studio/` | Blazor WebAssembly Elsa Studio client and RIoT picker UI |
| `RIoT2.Elsa.Tests/` | MSTest coverage for activities, split ports, command failures and gRPC behaviour |
| `Dockerfile` | Production image, web/Studio port 8080 and gRPC port 5003 |

## RIoT integration

The server publishes itself as a workflow node on MQTT. When the orchestrator sends configuration,
Elsa stores the orchestrator base URL and uses it for workflow activities:

- `RIoTTrigger` starts or resumes workflows for a selected report id.
- `RIoTData` reads a report, variable or command value from the orchestrator.
- `RIoTOutput` evaluates a JavaScript expression and submits a command through the orchestrator.

The externally visible contracts are documented in the hub:

- [HTTP and gRPC APIs](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/http-api.md)
- [MQTT topics and payloads](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/mqtt-topics.md)
- [Environment variables, ports and volumes](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/env-vars.md)

## Configuration

Set these variables when running the server outside a local Development profile:

- `RIOT2_WORKFLOW_ID`
- `RIOT2_WORKFLOW_URL`
- `RIOT2_WORKFLOW_GRPC_URL`
- `RIOT2_WORKFLOW_GRPC_PORT` (optional, default `5003`)
- `RIOT2_MQTT_IP`
- `RIOT2_MQTT_USERNAME` and `RIOT2_MQTT_PASSWORD` (optional for anonymous brokers)
- `ELSA_IDENTITY_SIGNING_KEY` (required outside Development)

Use an externally reachable URL for `RIOT2_WORKFLOW_URL` and `RIOT2_WORKFLOW_GRPC_URL`. Do not use
`localhost` when the orchestrator runs in another container or on another machine.

Workflow and identity data is stored in `Data/elsa.sqlite.db`, or `/app/Data/elsa.sqlite.db` in
the container. Mount `/app/Data` for persistent workflows.

## Build, test and run

From the workspace root (`C:\Src\RIoT2`):

```powershell
dotnet restore .\RIoT2.Elsa\RIoT2.Elsa.sln
dotnet build .\RIoT2.Elsa\RIoT2.Elsa.sln
dotnet test .\RIoT2.Elsa\RIoT2.Elsa.Tests\RIoT2.Elsa.Tests.csproj
dotnet run --project .\RIoT2.Elsa\RIoT2.Elsa.Server\RIoT2.Elsa.Server.csproj
```

The server hosts Studio at `/`, the RIoT HTTP trigger at `POST /riot/trigger/{id}`, the gRPC
`riot.RIoTTriggerService/Trigger` service on the dedicated HTTP/2 port, and health endpoints at
`GET /health` and `GET /healthz`.

## Docker

Build and run a local image:

```powershell
docker build -t riot2-elsa .\RIoT2.Elsa --build-arg NUGET_AUTH_TOKEN=<github-packages-token>
docker run -p 8080:8080 -p 5003:5003 `
  -e RIOT2_MQTT_IP=192.168.0.30 `
  -e RIOT2_MQTT_USERNAME=user `
  -e RIOT2_MQTT_PASSWORD=<mqtt-password> `
  -e RIOT2_WORKFLOW_ID=<workflow-id> `
  -e RIOT2_WORKFLOW_URL=http://<host>:8080 `
  -e RIOT2_WORKFLOW_GRPC_URL=http://<host>:5003 `
  -e ELSA_IDENTITY_SIGNING_KEY=<strong-signing-key> `
  -v <host-data-directory>:/app/Data `
  riot2-elsa
```

The image runs as the non-root `app` user. Host directories mounted at `/app/Data` must be writable
by UID 1654.

## Versions and releases

- Release notes are in [CHANGELOG.md](CHANGELOG.md).
- To release, push a tag `x.y.z` on `master`. CI publishes the Docker image to GitHub Container
  Registry.
- This repository references the `RIoT2.Core` package `1.0.1` from GitHub Packages.

## Contributing

- Instructions for AI coding agents: [AGENTS.md](AGENTS.md).
- Platform documentation: [.github/docs](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/README.md).

## License

See [LICENSE.txt](LICENSE.txt).
