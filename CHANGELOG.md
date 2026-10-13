# Changelog

All notable changes to `RIoT2.Elsa`. A version is released by pushing a git tag. CI then builds and
pushes the Docker image to GitHub Container Registry.

## [Unreleased]

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
