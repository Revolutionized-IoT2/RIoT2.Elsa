# AGENTS.md — RIoT2.Elsa

Applies to: this repository. Read the platform guide first:
[.github/AGENTS.md](https://github.com/Revolutionized-IoT2/.github/blob/main/AGENTS.md). It covers the
workspace map, platform-wide rules and the documentation rules. In the local workspace, every
`https://github.com/Revolutionized-IoT2/<Repo>/blob/main/<path>` link is the file
`C:\Src\RIoT2\<Repo>\<path>`; read the local file instead of fetching the URL.

## What this is

Elsa 3 (3.9) workflow automation for the RIoT2 platform, in two parts
([ADR 0016](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/adr/0016-integrated-workflows-and-llm.md)):

- `RIoT2.Elsa.Workflows`, a library (NuGet package) with the RIoT activities, Run LLM, the workflow
  management service and `AddRIoT2Workflows()`. The orchestrator hosts it in process
  (`RIOT2_WORKFLOW_MODE=embedded`, the default).
- `RIoT2.Elsa.Server`, the remote workflow host (`riot2-elsa`, `RIOT2_WORKFLOW_MODE=remote`). It
  publishes itself as a workflow node over MQTT, receives report triggers over gRPC and HTTP, and
  serves `/riot/workflows` to the orchestrator.

There is no Elsa Studio and no Elsa login since 2.0.0: RIoT2.UI's *Workflows* page is the designer,
through the orchestrator's `/api/workflows`. Elsa is the only automation engine in RIoT2.

## Commands

Run from the workspace root (`C:\Src\RIoT2`), in PowerShell:

```powershell
dotnet restore .\RIoT2.Elsa\RIoT2.Elsa.sln
dotnet build .\RIoT2.Elsa\RIoT2.Elsa.sln
dotnet test .\RIoT2.Elsa\RIoT2.Elsa.Tests\RIoT2.Elsa.Tests.csproj
dotnet run --project .\RIoT2.Elsa\RIoT2.Elsa.Server\RIoT2.Elsa.Server.csproj
```

- The run command starts the remote host. It requires the environment variables in
  [env-vars.md](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/env-vars.md)
  when used outside a local Development profile. Do not copy values from `launchSettings.json`.
- To build the container locally, put a GitHub Packages token in `$env:NUGET_AUTH_TOKEN`, then:
  `docker build --secret id=nuget_token,env=NUGET_AUTH_TOKEN -t riot2-elsa .\RIoT2.Elsa`.
  The token is a BuildKit secret; never pass it as a build argument.
- `GoldenMessageTests` read the [golden files](https://github.com/Revolutionized-IoT2/RIoT2.Core/blob/main/contracts/golden/README.md) in `RIoT2.Core/contracts/golden`; RIoT2.Core must be
  checked out next to this repository (CI clones it with `siblings: RIoT2.Core`). A failure means
  the orchestrator's response or command format no longer matches what Elsa reads or writes.
- Every push runs `.github/workflows/ci.yml` (build, tests, image build, secret scan). To release,
  push a tag `x.y.z` on `master`: `release.yml` validates, publishes the `RIoT2.Elsa.Workflows`
  package (version from `Directory.Build.repo.props`) to GitHub Packages, then pushes
  `ghcr.io/revolutionized-iot2/riot2-elsa:latest` and `:<tag>` ([CI guide](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/guides/ci.md)).
  To try the library in the orchestrator before a release, `dotnet pack` it to
  `C:\Src\RIoT2\.localfeed`.

## Layout

| Path | Contents |
|---|---|
| `RIoT2.Elsa.Workflows/WorkflowsServiceCollectionExtensions.cs`, `RIoT2WorkflowsOptions.cs` | `AddRIoT2Workflows()`: Elsa with SQLite (migrations run at start), JavaScript and Liquid, HTTP activities (base path `/workflows` unless the host sets it), retention, the RIoT feature and the management service |
| `RIoT2.Elsa.Workflows/RIoTFeature.cs` | Registers the activities, `RIoTTriggers`, `TriggerDeduplicator`, the schedule service, and the serialization aliases saved workflows need |
| `RIoT2.Elsa.Workflows/Activities/` | `RIoTTrigger`, `RIoTSchedule`, `RIoTData`, `RIoTCompare`, `RIoTThreshold`, `RIoTDebounce`, `RIoTThrottle`, `RIoTWaitForState`, `RIoTOutput`, `RIoTConfirmedCommand`, `RIoTNotification`, `RIoTWebhook`, `RIoTRunLlm`, `RIoTForecast`, `RIoTCommandPlan` |
| `RIoT2.Elsa.Workflows/Automation/` | `AutomationValues` (reading and comparing values, `Hysteresis`), `AutomationStateStore`, `Schedule.cs` (`ScheduleSpec`, `SunCalculator`, `GeoLocation`), `ScheduleService` |
| `RIoT2.Elsa.Workflows/Management/` | `WorkflowManagementService` (the `/riot/workflows` and `/api/workflows` API), `WorkflowCatalogBuilder` (the designer's activity catalog) and their messages |
| `RIoT2.Elsa.Workflows/Llm/LlmContracts.cs` | `LlmRunRequest`, `LlmRunResult`, `ILlmRunner`, the tools and error codes |
| `RIoT2.Elsa.Workflows/Services/`, `Models/`, `UIHints/` | `IRIoTDataService` (each host implements it), `RIoTTriggers`, `TriggerDeduplicator`, `ElsaMetrics`; `RIoTTemplateItem`; the RIoT selector's settings |
| `RIoT2.Elsa.Server/Program.cs` | The remote host: `AddRIoT2Workflows()`, `AddRIoT2Security()` (CORS, the RIoT policies), health, RIoT HTTP and gRPC wiring |
| `RIoT2.Elsa.Server/RIoT/Endpoints/RIoTEndpoints.cs` | `POST /riot/trigger/{id}` and the `/riot/workflows` management API |
| `RIoT2.Elsa.Server/RIoT/Services/` | MQTT, configuration, gRPC trigger, `RIoTDataService` and `HttpLlmRunner` (both over HTTP to the orchestrator) |
| `RIoT2.Elsa.Server/RIoT/Protos/riot.proto` | Elsa copy of the workflow trigger gRPC contract |
| `RIoT2.Elsa.Tests/` | MSTest coverage for endpoint, activity, schedule, template, management, Run LLM, gRPC and trigger deduplication behaviours; `WorkflowTemplates.cs` builds the templates |
| `templates/` | Workflow templates (Elsa export format), generated by `WorkflowTemplates` |
| `Dockerfile` | `net10.0` multi-stage image, web port 8080 and gRPC port 5003; base images pinned by digest (Renovate updates them) |

## Contracts implemented here

- [http-api.md](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/http-api.md):
  `RIoT2.Elsa.Server/RIoT/Endpoints/RIoTEndpoints.cs`,
  `RIoT2.Elsa.Server/RIoT/Services/RIoTTriggerGrpcService.cs` and `Program.cs` implement
  `POST /riot/trigger/{id}`, `/riot/workflows`, gRPC `riot.RIoTTriggerService/Trigger`, `/health` and
  `/healthz`. The management messages are the orchestrator's `/api/workflows` bodies too; change
  them additively, and in step with RIoT2.UI's `models/workflow.ts`.
- [mqtt-topics.md](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/mqtt-topics.md):
  `WorkflowMqttService` subscribes to orchestrator online/configuration messages and announces
  `NodeType.Workflow` on `riot2/node/{id}/online`.
- [env-vars.md](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/contracts/env-vars.md):
  `WorkflowOptions` (every setting, validated at startup), `WorkflowEndpointConfiguration`,
  `Program.cs` and `Dockerfile` define the Elsa variables, web port 8080, gRPC port 5003 and `/app/Data` persistence path.

The proto file `RIoT2.Elsa.Server/RIoT/Protos/riot.proto` must keep the same package, service,
messages and field numbers as `RIoT2.Net.Orchestrator/Protos/riot_trigger.proto`. Only
`option csharp_namespace` differs, intentionally. Changes must be additive and must touch both
repositories.

## Rules

- Do not reintroduce the old internal rule engine, NCalc rules, rule models or rule UI. Elsa is
  the only automation engine ([ADR 0003](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/adr/0003-elsa-sole-automation-engine.md)).
- Keep the custom activities' type names (`RIoT2.RIoTTrigger`, …, set by `[Activity(Namespace = "RIoT2", …)]`),
  their input names and their outcome names: saved workflows refer to them. The C# namespaces are
  `RIoT2.Elsa.Workflows.*`; the type names don't follow them.
- Every type a workflow stores in a variable or input must be registered in `RIoTFeature`
  (`RegisterTypeAlias`, plus `RegisterLegacyTypeName` for names Studio or 1.x wrote, such as
  `RIoT2.Elsa.Studio.Models.RIoTTemplateItem, RIoT2.Elsa.Studio`). Elsa 3.9 writes an unregistered type
  as `UnregisteredClrType:…` and reads it back as an exception. `RIoTConfirmedCommand`'s
  timeout stays below the orchestrator client's 60 s (`MaxTimeoutSeconds`).
- `RIoTWaitForState` waits on the same bookmark name and stimulus as `RIoTTrigger` (the report id,
  without the activity instance id); keep them in step, or reports no longer resume it.
- The templates in `templates/` are generated: change `RIoT2.Elsa.Tests/WorkflowTemplates.cs` and run
  the tests with `RIOT2_UPDATE_TEMPLATES=1`; `WorkflowTemplateTests` fails when the files differ and
  imports them through the management service, as the UI does. Triggers need
  `customProperties.canStartWorkflow: true`, or the workflow never starts.
- New activities get a colour and icon in RIoT2.UI's `src/composables/workflowAppearance.ts` and a
  row in the [automation guide](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/guides/automation.md).
  An input that picks a report, command or variable uses the `riot-output-selector` UI hint and
  `RIoTSelectorUIHandlers`; the UI writes Studio's literal (PascalCase, numeric enums).
- Don't put host-specific code in `RIoT2.Elsa.Workflows`: what differs between the orchestrator and
  the container goes behind `IRIoTDataService` or `ILlmRunner`. Run LLM never sees an API key; the
  orchestrator's `ILlmRunner` does.
- Keep workflow work asynchronous. Do not block on `.Result`, `.Wait()` or `Task.WaitAll`.
- Keep `RIOT2_WORKFLOW_GRPC_URL` required and absolute. The orchestrator needs this advertised
  endpoint to trigger workflows over the dedicated HTTP/2 listener.
- Don't bring back Elsa Studio, Elsa Identity or the Elsa Workflows API (`UseWorkflowsApi`): the
  UI talks to the orchestrator only, and the host has no login of its own (ADR 0016).
- Keep `/health` and `/healthz` anonymous and lightweight.
- Do not copy hub contract tables into this repository. Link to the hub and update the hub only
  when a contract changes.
- Keep the Core packages as package references, and only the narrow ones (plan M1 step 4): this
  repository pins `RIoT2.Core.Contracts`, `RIoT2.Core.Mqtt`, `RIoT2.Core.Http` and `RIoT2.Core.Hosting`
  `2.0.0`. Don't add `RIoT2.Core.Node`.
- Every gRPC trigger runs through the singleton `TriggerDeduplicator`. Keep `message_id` (field 3)
  in `riot.proto` and in the orchestrator's `riot_trigger.proto`; never reuse a field number.
- Keep `PackageReference` items versionless; package versions belong in `Directory.Packages.props`.
- Logging comes from `builder.AddRIoT2Logging()` (RIoT2.Core.Hosting): don't add Serilog sinks,
  logging providers or `Logging` sections to `appsettings.json`; `RIOT2_LOG_FORMAT` and
  `RIOT2_LOG_LEVEL` are the settings. Log through `ILogger`, and wrap work for one command or
  trigger in `logger.BeginIdScope(LogScope.CorrelationId | LogScope.MessageId, id)`.
- Metrics come from `builder.AddRIoT2Telemetry()` (`OTEL_EXPORTER_OTLP_ENDPOINT`, `RIOT2_TRACING`);
  a new `riot2.*` instrument is added to the hub's `contracts/metrics.md` and pinned in a test.

## Pitfalls

- Plaintext HTTP/2 gRPC is on a dedicated listener. Do not try to serve REST and gRPC from one
  plaintext `Http1AndHttp2` port.
- Docker exposes the web API on 8080 and gRPC on 5003. `RIOT2_WORKFLOW_URL` and
  `RIOT2_WORKFLOW_GRPC_URL` must be reachable by other containers or devices, not `localhost`.
- `WorkflowMqttService` announces both `NodeBaseUrl` and `GrpcBaseUrl` when connected and when the
  retained orchestrator-online message arrives. Install handlers before subscribing.
- Workflow state lives in `Data/elsa.sqlite.db` (`/app/Data/elsa.sqlite.db` in the image; the
  orchestrator's `StoredObjects/elsa.sqlite.db` when embedded). Bind-mounted data must be writable by
  UID 1654. A 1.x database still has Studio's identity tables; leave them.
- `Program.cs` uses permissive CORS (from `AddRIoT2Security` while the mode is `off`) until S4 (I52);
  don't tighten it elsewhere.
- The RIoT endpoints carry RIoT2 policies: `/riot/trigger` and the gRPC service `Operator`,
  `/riot/workflows` reads `Viewer`, changes `Admin` and cancelling a run `Operator`.
- JsonSchema.Net (an Elsa dependency) has a global `Json` namespace that hides
  `RIoT2.Core.Utils.Json`: write `RIoT2.Core.Utils.Json` in full here, or alias the package
  (`Aliases="JsonSchemaNet"`) as the orchestrator does.
- `MapGroup` routes with an empty template (`workflows.MapGet("")`) answer at `/riot/workflows/` and
  `/riot/workflows`; the docs-check finds the group prefix only from a `x = y.MapGroup("…")` line.
- Runs, activity records and cancellations are written by Elsa in the background: a test that reads
  them right after a run must poll.
- The Core packages `2.0.0` are published. Use `C:\Src\RIoT2\.localfeed` as an extra NuGet source only
  to try an unreleased Core version.
- The trigger deduplication window lives in memory: after an Elsa restart, a redelivery of a
  trigger that already ran runs again. The gRPC service doesn't pass the call's cancellation to
  the workflow, so a run whose caller timed out still completes and is remembered.

## Related work

- Backlog item 14 (configuration read by hand from environment variables) is done (I07).
- Backlog item 17 (cross-repository contract and integration tests) is done (I05, I06). The
  end-to-end tests use a stub of `riot.RIoTTriggerService` in place of Elsa; Elsa's golden tests
  pin the messages it reads.
- [S7](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/backlog/optional-hardening.md):
  optional Elsa hardening for CORS (Studio's identity and antiforgery went with 2.0.0).
- [M4](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/plans/m04-typed-configuration.md):
  typed validated configuration (steps 1–5 done in I07).
- [M7](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/plans/m07-contract-integration-tests.md):
  contract and integration test plan.
- [M8](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/plans/m08-dotnet10-migration.md):
  .NET 10 alignment and central package management; Elsa already targets `net10.0`.
- [Design 7.1](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/design/reliable-delivery.md):
  workflow outbox and trigger `message_id` deduplication (phase 1 done in I11); phase 4 adds a
  "command with confirmation" activity.
- [I48](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-3-4.md#i48-elsa-activity-pack-m): the activity pack and the workflow templates ([automation guide](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/guides/automation.md)).
- [I55](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-3-4.md#i55-integrated-workflows-and-run-llm-m): the library, the designer in the UI, the embedded mode and Run LLM (2.0.0).
