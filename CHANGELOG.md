# Changelog

All notable changes to `RIoT2.Elsa`. A version is released by pushing a git tag. CI then builds and
pushes the Docker image to GitHub Container Registry.

## [Unreleased]

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
