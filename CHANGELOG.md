# Changelog

All notable changes to `RIoT2.Elsa`. A version is released by pushing a git tag. CI then builds and
pushes the Docker image to GitHub Container Registry.

## [Unreleased]

- Changed package management to central `Directory.Packages.props`, with `RIoT2.Core` 1.0.1,
  `Grpc.AspNetCore` 2.84.0 and ASP.NET Core / Entity Framework package versions at 10.0.12.
- Changed `RIoT2.Elsa.Tests` to `MSTest.Sdk` 4.4.1.
- Updated the Dockerfile restore layer to copy `Directory.Build.props` and
  `Directory.Packages.props`.
- Documentation: `AGENTS.md` is the AI instruction file, `CLAUDE.md` imports it, and README
  upgrade notes moved into this file.

## Earlier versions

Tags `0.1.1`-`0.1.3`. See `git log` and the tags; there are no release notes for them.
