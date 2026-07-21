# Localizer

Localizer is a local-first localization authoring and quality assurance utility for .NET applications, games, and other software that consumes text resources.

It keeps one engine-neutral catalog, drafts translations with a locally hosted language model, validates machine-sensitive syntax deterministically, and exports compact files for production use.

## Project status

The repository currently contains the initial .NET solution structure. The domain model, workflows, persistence, model integration, validators, and exporters are not implemented yet.

The first milestone is a complete command-line workflow:

1. Load an authoritative catalog.
2. Identify missing or stale translations.
3. Draft translations through a local model provider.
4. Validate placeholders, message syntax, markup, and configured limits.
5. Approve valid translations explicitly.
6. Export deterministic runtime JSON.

## Design goals

- Remain independent of any game engine, UI toolkit, or translation provider.
- Run locally and keep unpublished source material on the user's machine.
- Treat generated translations as untrusted drafts.
- Preserve stable text keys independently of source wording.
- Detect stale translations without deleting approved work.
- Support multiple message syntaxes through replaceable syntax profiles.
- Produce small, deterministic, UTF-8 runtime files.
- Add importers and exporters without changing the domain model.

## Non-goals for the first release

- A runtime localization framework.
- Direct scanning of engine project files.
- Automatic approval of generated translations.
- Cloud accounts or multi-user collaboration.
- A full translation management platform.
- Transparent conversion between every message syntax.

## Solution structure

- `Localizer.Core`: catalog rules, lifecycle semantics, validation models, and domain services.
- `Localizer.Application`: planned use cases and ports for catalog operations, drafting, validation, approval, and export.
- `Localizer.Infrastructure`: file persistence, local model clients, and concrete importer and exporter implementations.
- `Localizer.Cli`: command-line host and dependency composition.
- `Localizer.Core.Tests`: deterministic domain and validation tests.

Dependencies point inward. Core has no infrastructure or presentation dependencies.

## Formats and compatibility

The authoritative catalog will be a versioned, engine-neutral UTF-8 JSON document. Production files are generated artifacts rather than the source of truth.

No single localization file is natively understood by every consumer. Portability is achieved through:

- one neutral catalog;
- stable documented semantics;
- configurable importers and exporters;
- optional target-specific adapters outside the core.

External applications do not load Localizer assemblies. They consume exported files, so their runtime or language version does not need to match the .NET version used by this utility.

## Build

Requirements:

- .NET 10 SDK
- A local model service only when translation drafting is implemented and enabled

```powershell
dotnet build .\Localizer.slnx
dotnet test .\Localizer.slnx
```

## Documentation

- [Architecture and decisions](docs/architecture.md)
- [Catalog and format contract](docs/catalog-and-formats.md)
- [Implementation plan](docs/implementation-plan.md)
- [Agent instructions](AGENTS.md)

## Guiding principle

The language model proposes text. The deterministic application owns identity, state, validation, approval, and export.
