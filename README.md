# Localizer

Localizer is a local-first localization authoring and quality assurance utility for .NET applications, games, and other software that consumes text resources.

It keeps one engine-neutral catalog, drafts translations with a locally hosted language model, validates machine-sensitive syntax deterministically, and exports compact files for production use.

## Project status

Available today:

- Solution foundation with Application, Core, Infrastructure, CLI stub, and test projects.
- Core catalog domain: keys, locales, Draft/Approved workflow, derived Missing/Stale status, and source fingerprints.
- Authoritative catalog JSON persistence via `ICatalogStore` and `JsonCatalogStore` (UTF-8 without BOM, transactional save, golden-file tests).
- Deterministic validation in Core: plain and composite syntax parsing, placeholder parity, entry constraints, and approval/export policies.

Still ahead for the first usable desktop slice: Application use cases, Avalonia desktop shell, local model drafting, review and approval UI, runtime export, and CSV interchange.

Target desktop workflow:

1. Open or create an authoritative catalog.
2. Review entries and see Missing or Stale translations.
3. Draft translations through a local model provider.
4. Validate placeholders and configured limits for `plain` and indexed `composite` text.
5. Approve valid translations explicitly in the UI.
6. Export deterministic runtime JSON.

## Design goals

- Remain independent of any game engine, UI toolkit, or translation provider in Core and Application.
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
- A required terminal workflow for end users.

## Solution structure

- `Localizer.Core`: catalog rules, lifecycle semantics, and domain services.
- `Localizer.Application`: use-case ports (for example `ICatalogStore`); use cases grow as the desktop slice lands.
- `Localizer.Infrastructure`: JSON catalog persistence; later local model clients and exporters.
- `Localizer.Desktop`: planned Avalonia desktop host and composition root for version 1.
- `Localizer.Cli`: optional stub host for future automation; not required for the first release.
- `Localizer.Core.Tests`: deterministic domain tests.
- `Localizer.Infrastructure.Tests`: persistence round-trip and golden-file tests.

Dependencies point inward. Core has no infrastructure or presentation dependencies.

## Formats and compatibility

The authoritative catalog is a versioned, engine-neutral UTF-8 JSON document. Production files are generated artifacts rather than the source of truth.

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

`dotnet build` and `dotnet test` are developer safeguards. End users interact through the desktop app, not a terminal workflow.

## Docs

- [Architecture](docs/architecture.md)
- [Catalog and format contract](docs/catalog-and-formats.md)

## Guiding principle

The language model proposes text. The deterministic application owns identity, state, validation, approval, and export.
