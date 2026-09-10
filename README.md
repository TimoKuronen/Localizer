# Localizer

Local-first localization authoring and QA utility for .NET applications, games, and other software that consumes text resources. It keeps an engine-neutral catalog, validates machine-sensitive message syntax deterministically, and treats model output as untrusted drafts while deterministic code owns identity, lifecycle, approval eligibility, and export.

Consumers exchange files with Localizer. They do not load its assemblies or share its .NET runtime.

This repository currently ships the catalog domain, authoritative JSON persistence, and validation for `plain` and indexed `composite` text. Desktop authoring, local-model drafting, and production exporters are not in the tree yet.

## Highlights

- Engine-neutral catalog model with stable keys, BCP 47 locales, and Draft/Approved workflow state
- Derived Missing and Stale status from fingerprints so approved text is preserved when source inputs change
- Authoritative versioned catalog JSON (UTF-8 without BOM) with transactional save
- Deterministic validators for `plain` and indexed `.NET`-style `composite` placeholders (`{0}`, `{1}`, with escapes)
- Entry constraints (grapheme, UTF-8 byte, line, term) plus approval and export validation policies
- Layered Core / Application / Infrastructure solution; dependencies point inward
- `ICatalogStore` port with Infrastructure `JsonCatalogStore` and golden-file round-trip tests
- 68 xUnit tests across Core and Infrastructure (no network required)

## Architecture

```text
Localizer.Core
      ^
Localizer.Application
      ^
Localizer.Infrastructure
      ^
Localizer.Cli (stub host)
```

Pipeline intent:

```text
Sources -> Neutral catalog -> Drafting and QA -> Review -> Generated outputs
```

Projects: `Localizer.Core` (domain, lifecycle, validation), `Localizer.Application` (ports such as `ICatalogStore`; use cases grow with hosts), `Localizer.Infrastructure` (JSON catalog persistence), `Localizer.Cli` (thin stub), plus `Localizer.Core.Tests` and `Localizer.Infrastructure.Tests`.

Details: [docs/architecture.md](docs/architecture.md)

## Stack

- C# / .NET 10
- System.Text.Json
- xUnit

## Build

```powershell
dotnet build .\Localizer.slnx
dotnet test .\Localizer.slnx
```

## Docs

- [Architecture](docs/architecture.md)
- [Catalog and format contract](docs/catalog-and-formats.md)