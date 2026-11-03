# Changelog

All notable changes to `RIoT2.Elsa`. A version is released by pushing a git tag. CI then builds and
pushes the Docker image to GitHub Container Registry.

## [Unreleased]

## [2.0.0] - 2026-11-03

The integrated designer, the embedded mode and Run LLM ([I55](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-3-4.md#i55-integrated-workflows-and-run-llm-m),
[ADR 0016](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/adr/0016-integrated-workflows-and-llm.md)). Breaking: read the
[upgrade notes](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/guides/upgrading.md#integrated-workflows-and-run-llm-elsa-200-orchestrator-1210-ui-1110)
first. Existing workflows load unchanged.

- Removed: Elsa Studio (`RIoT2.Elsa.Studio`), Elsa Identity with its built-in admin login and
  `ELSA_IDENTITY_SIGNING_KEY`, and the Elsa Workflows API. Workflows are designed on RIoT2.UI's
  *Workflows* page through the orchestrator (Orchestrator 1.21.0, UI 1.11.0).
- Added: the `RIoT2.Elsa.Workflows` package with every activity, `AddRIoT2Workflows()` and the
  workflow management service, so the orchestrator can run workflows itself (its default since
  1.21.0). The container is now for `RIOT2_WORKFLOW_MODE=remote`, under the compose profile
  `remote-workflows`.
- Added: `/riot/workflows` on the host (list, catalog, save, import, export, versions, publish,
  retract, delete, runs and cancel; Viewer, Admin and Operator policies). The orchestrator's
  `/api/workflows` forwards to it in the remote mode.
- Added: the **Run LLM** activity (`RIoT2.RIoTRunLlm`). It sends instructions and input to a
  language model through a connection kept by the orchestrator, answering as text or as JSON checked
  against a schema. Optional tools read values (`ReadValues`) and send allowed commands
  (`SendCommands`), within command, tool call and time limits. The host runs it with
  `POST /api/llm/run` on the orchestrator.
- Changed: Elsa 3.7.1 → 3.9.0; the database is migrated on the first start. Saved workflows' type
  names (`RIoT2.RIoTTemplateItem` and the old `RIoT2.Elsa.Studio.Models.RIoTTemplateItem`, `ElsaReport`,
  `CommandConfirmation`, `Command`, `Variable`) are registered aliases, so they read back as before.
- Changed: the C# namespaces are `RIoT2.Elsa.Workflows.*`; activity type names stay `RIoT2.*`.
- Fixed: the triggers in `templates/` now have `canStartWorkflow`, so imported templates start.
- Changed: also references `RIoT2.Core.Http` 2.0.0 (the outbound URL policy).

## [1.11.0] - 2026-11-03

The activity pack ([I48](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-3-4.md#i48-elsa-activity-pack-m), [automation guide](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/guides/automation.md)).
No port, user or volume changes; existing workflows are unchanged.

- Added: `RIoT Schedule`, a trigger at a local time of day or at sunrise or sunset (with an offset),
  on chosen days, fired by the new `ScheduleService`. New optional settings `RIOT2_LATITUDE` and
  `RIOT2_LONGITUDE` for sunrise and sunset.
- Added: `RIoT Compare` and `RIoT Threshold` (hysteresis), which use the triggering report when no
  value is given; `RIoT Debounce` and `RIoT Throttle`, with state kept between runs by key (in memory).
- Added: `RIoT Wait for state`, which suspends the workflow until a report has a value, or times out;
  it resumes on the same stimulus as `RIoT Trigger`.
- Added: `RIoT Notification` (a push or a mail through a Messaging device's command) and
  `RIoT Webhook` (outbound policy purpose `webhook`).
- Added: the workflow templates `templates/motion-light.json` and `templates/price-based-heater.json`,
  generated from code and tested to import like Elsa Studio's import.

## [1.10.0] - 2026-11-03

- Changed: built on `RIoT2.Core.Contracts`, `.Mqtt` and `.Hosting` 2.0.0 ([I45](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-3-4.md#i45-core-20-remove-compatibility-layers-m)): no Newtonsoft.Json
  through Core. No change to the activities, the wire format or the settings.

## [1.9.0] - 2026-11-02

A command activity that waits for the device ([I36](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-3-4.md#i36-sync-and-command-status-in-the-ui-m), [design 7.1](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/design/reliable-delivery.md) phase 4). No port, user,
volume or variable changes; existing workflows are unchanged.

- Added: "RIoT Command with confirmation" (`RIoTConfirmedCommand`). It sends the command like RIoT
  Output, through `POST /api/v2/commands` with `"wait": true` and a timeout of 1 to 50 seconds
  (default 30), and continues on `Done` (`executed`, `duplicate`, or `sent` to a node that doesn't
  report results) or `Failed` (`failed`, `unknownCommand`, `expired`), with `Status` and `Error` as
  outputs. Needs Orchestrator 1.4.0 or later.
- Security: ASP.NET Core WebAssembly packages 10.0.14 and `Microsoft.AspNetCore.SignalR.Common`
  10.0.14 (pinned over Elsa Studio's 10.0.3): GHSA-g27p-qf6j-c96c, GHSA-qjpx-jqm5-h2c4,
  GHSA-334f-vvp4-7g6p.

## [1.8.0] - 2026-11-02

The optional security mode's seams ([I34](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i34-security-seams-with-the-mode-fixed-at-off-m), [design 7.5](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/design/security-mode.md) phase S0), with RIoT2.Core 1.18.0 (was 1.14.1).
No port, user, volume or JSON changes; with the default mode `off` nothing is enforced, and Elsa
Studio's login is unchanged.

- Added: `POST /riot/trigger/{id}` and the gRPC trigger service have the `Operator` policy, which
  the `LocalTrust` scheme passes while the mode is `off`. Elsa's own authentication stays the
  default scheme.
- Added: `RIOT2_SECURITY_MODE` (only `off` is accepted; `audit` or `on` stops the start),
  `RIOT2_SECURITY_FEATURES`, `RIOT2_API_KEY` (sent in `X-RIoT2-Key` on calls to the orchestrator)
  and `RIOT2_CORS_ORIGINS` (used once the `api` feature exists; any origin while `off`, as before).
- Added: `RIOT2_MQTT_TLS` and `RIOT2_MQTT_CA_FILE` connect to a TLS broker.
- Changed: the orchestrator client passes the outbound URL policy (allow-all while `off`).
- Changed: an empty configuration command (`null`) is logged as a warning, where it threw a
  `NullReferenceException` that was logged as an error. Built with the nullable-annotated Core.

## [1.7.2] - 2026-11-02

Supply chain ([I33](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i33-supply-chain-s)). No port, user, volume, variable or JSON changes.

- Build: the Dockerfile's base images are pinned by digest (`<image>:<tag>@sha256:...`), rebuilt on
  the current .NET 10 / base image patches. Renovate updates the digests.
- The image carries an SPDX SBOM and a provenance attestation; the tag now names an image index.
  `docker pull` and compose are unaffected.
- Build: `renovate.json` extends the shared Renovate preset ([I33](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i33-supply-chain-s)). Renovate opens grouped
  update pull requests once the app is installed (MA7).
- Build: the shared `Directory.Build.props` sets `Nullable=enable` for every project ([I32](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i32-nullable-reference-types-m)).
  The projects here already had it, so nothing changes.
- Build: the threading analyzers ([I31](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i31-async-cleanup-part-2-and-threading-analyzers-m)) run in every project from the shared
  `Directory.Build.props` and `.editorconfig`: a blocking wait, `async void` or an unobserved task
  fails the CI build. No change to the shipped binaries.

## [1.7.1] - 2026-11-01

- Fixed: the image starts again (1.7.0 failed with `FileNotFoundException` for
  `Microsoft.Bcl.AsyncInterfaces`): built with RIoT2.Core 1.14.1, whose `RIoT2.Core.Mqtt` declares
  that dependency ([I28](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i28-narrow-references-and-generated-models-m)). Don't run 1.7.0.

## [1.7.0] - 2026-11-01

Narrow Core references ([I28](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i28-narrow-references-and-generated-models-m), plan M1 step 4), with RIoT2.Core 1.14.0. No port, user, volume,
variable or JSON changes.

- Changed: references `RIoT2.Core.Contracts`, `RIoT2.Core.Mqtt` and `RIoT2.Core.Hosting` instead of
  the `RIoT2.Core` facade, so the image no longer carries the device SDK, the node runtime or Quartz.

## [1.6.0] - 2026-11-01

System.Text.Json on the wire ([I25](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i25-systemtextjson-on-the-wire-m)), with RIoT2.Core 1.11.0. No port, user or volume
changes; the orchestrator responses and MQTT messages Elsa reads are the same.

- Changed: the report value an `RIoTData` activity returns holds a `JsonElement` for an entity
  (it was a Newtonsoft `JObject`), the same as the trigger data of `RIoTTrigger`; numbers, text and
  booleans are unchanged.
- Tests: `GoldenMessageTests` reads the orchestrator's template listings from the new golden files
  `http/templates-*.json` ([I23](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i23-orchestrator-restructure-m)). No code change.

## [1.5.0] - 2026-11-01

Metrics ([I22](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i22-metrics-m), design 7.4 phase 4), with RIoT2.Core 1.10.0.
No port, user or volume changes.

- Added: the meter `RIoT2.Elsa`: `riot2.workflow.triggers` (by result, `received` or `duplicate`, and
  transport) and `riot2.workflow.faults` (faulted workflow executions, by definition id).
- Added: `OTEL_EXPORTER_OTLP_ENDPOINT` exports them, with `riot2.mqtt.*`, ASP.NET Core and runtime
  metrics, over OTLP; `RIOT2_TRACING=true` adds traces. Unset, nothing is exported.

## [1.4.0] - 2026-11-01

Consistent logging ([I20](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-2.md#i20-consistent-logging-s), design 7.4 phase 2), with RIoT2.Core 1.9.0.

- Changed: Elsa logs through `AddRIoT2Logging()` (RIoT2.Core.Hosting) like the other services:
  Serilog to the console and to `Logs/RIoT2.log` (`/app/Logs`; the image creates it), rolled
  daily and at 50 MB, 14 files kept, text or JSON (`RIOT2_LOG_FORMAT`), at `RIOT2_LOG_LEVEL`.
  Before, it logged to the console only. An invalid setting is now also logged as critical.
- Added: a gRPC trigger starts its workflow in a `MessageId` log scope.
- Removed: the `Logging` sections of `appsettings.json`.

## [1.3.0] - 2026-10-14

Deduplicated workflow triggers ([I11](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-0-1.md#i11-durable-workflow-delivery-m),
[design 7.1](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/design/reliable-delivery.md) phase 1).
Built on `RIoT2.Core` 1.5.0. No deployment change is needed; release it before RIoT2.Net.Orchestrator
1.3.0, which redelivers triggers.

- Added `message_id` (field 3) to the gRPC `TriggerRequest`. A trigger whose id was seen in the
  last ten minutes is answered with success and not run again. A duplicate that arrives while the
  first run is in progress waits for it, and a failed run is forgotten so its redelivery runs.
  Requests without an id (older orchestrators, `POST /riot/trigger/{id}`) always run.
- Changed: a gRPC trigger whose deadline has already passed when it arrives is rejected with
  `DeadlineExceeded` and not run. The orchestrator sets the deadline to the trigger's expiry.
- Removed the unused `SetRIoTAppLifetime` extension, which blocked on the MQTT stop with `.Wait()`
  ([I09](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-0-1.md#i09-async-cleanup-part-1-s)). The hosted `MqttBackgroundService` already stops MQTT in `StopAsync`. No behaviour
  change.

## [1.2.0] - 2026-10-13

MQTT client robustness ([I08](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-0-1.md#i08-mqtt-client-robustness-m)). Built on `RIoT2.Core` 1.3.0. No deployment change is needed.

- Changed: the workflow engine publishes and subscribes at QoS 1, keeps at most 10,000 outgoing messages
  while the broker is unreachable (the oldest are dropped and logged), and logs a failing MQTT
  handler instead of losing it.
- Added `RIOT2_MQTT_PORT` (default 1883) for a broker on another port.
- Changed: MQTT messages are handled in arrival order; they used to be handled in parallel
  background tasks.

## [1.1.0] - 2026-10-13

Typed configuration ([I07](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-0-1.md#i07-typed-configuration-s)). Built on `RIoT2.Core` 1.2.0. No deployment change: the variable names
are the same.

- Changed: `WorkflowOptions` reads and validates every setting at startup, including
  `RIOT2_WORKFLOW_GRPC_PORT` and `ELSA_IDENTITY_SIGNING_KEY`. A start with missing or invalid
  settings fails with one message that lists them all, for example `Invalid workflow configuration:
  RIOT2_WORKFLOW_ID is required; ELSA_IDENTITY_SIGNING_KEY is required outside Development.`
  `RIoTConfigurationService` receives `IOptions<WorkflowOptions>`.
- Changed: with `RIOT2_MQTT_USERNAME` unset, Elsa now connects without a user name, like the
  orchestrator and the node; it used to send an empty one. Brokers that allow anonymous clients
  accept both.
- Added `GoldenMessageTests` ([I05](https://github.com/Revolutionized-IoT2/.github/blob/main/docs/increments/phase-0-1.md#i05-golden-files-and-route-contract-m)): Elsa reads the golden report, command and variable
  responses, and posts commands in the golden format. CI clones RIoT2.Core for the files.

## [1.0.2] - 2026-10-12

No deployment change.

- Changed the Dockerfile: the GitHub Packages token is the BuildKit secret `nuget_token` and is no
  longer stored in an image layer. The `NUGET_AUTH_TOKEN` build argument is gone.
- CI: every push runs the tests, the image build and a secret scan; a tag validates before it
  pushes the image. `.gitignore` no longer hides `.github/workflows`.

## [1.0.1] - 2026-10-12

Built on `RIoT2.Core` 1.0.1 and .NET 10. Tag `1.0.0` failed the Docker build and was never
published.

- Changed package management to central `Directory.Packages.props`, with `RIoT2.Core` 1.0.1,
  `Grpc.AspNetCore` 2.84.0 and ASP.NET Core / Entity Framework package versions at 10.0.12.
- Changed `RIoT2.Elsa.Tests` to `MSTest.Sdk` 4.4.1.
- Updated the Dockerfile restore layer to copy `Directory.Build.props` and
  `Directory.Packages.props`.
- Fixed the Docker build: the build stage uses the Debian SDK image, because the glibc-linked
  `protoc` from `Grpc.Tools` cannot run on Alpine. The runtime image is still Alpine. Added a
  `.dockerignore`, so local `bin`/`obj` and SQLite files are not copied into the image.
- Documentation: `AGENTS.md` is the AI instruction file, `CLAUDE.md` imports it, and README
  upgrade notes moved into this file.

## Earlier versions

Tags `0.1.1`-`0.1.3`. See `git log` and the tags; there are no release notes for them.
