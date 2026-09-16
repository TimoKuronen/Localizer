# Localizer

Local-first localization authoring and QA utility for .NET applications, games, and other software that consumes text resources. It keeps an engine-neutral catalog, validates machine-sensitive message syntax deterministically, and treats model output as untrusted drafts while deterministic code owns identity, lifecycle, approval eligibility, and export.

Consumers exchange files with Localizer. They do not load its assemblies or share its .NET runtime.

This repository currently ships the catalog domain, Application authoring use cases, authoritative JSON persistence, validation for `plain` and indexed `composite` text, local-model drafting via an Ollama HTTP adapter, human approval of drafts, compact per-locale production export, Unity Localization String Table CSV import/merge and export, per-catalog project folder bindings, and an Avalonia desktop shell for catalog create/open/save, entry editing, draft, approve, and export.

## Highlights

- Engine-neutral catalog model with stable keys, BCP 47 locales, and Draft/Approved workflow state
- Derived Missing and Stale status from fingerprints so approved text is preserved when source inputs change
- Authoritative versioned catalog JSON (UTF-8 without BOM) with transactional save
- Deterministic validators for `plain` and indexed `.NET`-style `composite` placeholders (`{0}`, `{1}`, with escapes)
- Entry constraints (grapheme, UTF-8 byte, line, term) plus approval and export validation policies
- Explicit human approval gated by `ApprovalValidationPolicy`; export gated by `ExportValidationPolicy`
- Compact per-locale runtime JSON exporter (`ICatalogExporter` / `CompactLocaleJsonExporter`)
- Unity Localization String Table CSV import/merge and permissive CSV export (`IUnityCsvReader` / `IUnityCsvExporter`)
- Per-catalog project folder bindings with configurable import/export file names
- Layered Core / Application / Infrastructure / Desktop solution; dependencies point inward
- Application use cases for catalog lifecycle, entry edits, human drafts, approval, export, work-queue/status queries, and validation
- Avalonia desktop host as the composition root for authoring
- Local-model drafting through `ITranslationDraftProvider` (Ollama HTTP); drafts store sanitized provenance and stay unapproved until a human Approves
- `ICatalogStore` port with Infrastructure `JsonCatalogStore` and golden-file round-trip tests
- NUnit tests across Core, Application, and Infrastructure (default suite needs no network or running model)

## Architecture

```text
Localizer.Core
      ^
Localizer.Application
      ^
Localizer.Infrastructure
      ^
Localizer.Desktop
```

Pipeline intent:

```text
Sources -> Neutral catalog -> Drafting and QA -> Review -> Generated outputs
```

Projects: `Localizer.Core` (domain, lifecycle, validation), `Localizer.Application` (ports and use cases), `Localizer.Infrastructure` (JSON persistence, Ollama drafting, compact locale export, Unity CSV adapters), `Localizer.Desktop` (Avalonia composition root), plus Core/Application/Infrastructure test projects.

Details: [docs/architecture.md](docs/architecture.md)

## Stack

- C# / .NET 10
- Avalonia
- System.Text.Json
- NUnit

## Build

```powershell
dotnet build .\Localizer.slnx
dotnet test .\Localizer.slnx
dotnet run --project .\Localizer.Desktop\Localizer.Desktop.csproj
```

Open/Save dialogs start in the repo `Storage/` folder (gitignored working catalogs and local test files).

Local drafting expects a running Ollama service at `http://127.0.0.1:11434/` by default. Preferred model is `llama3.2` when installed; if that name is missing and exactly one model is installed, Localizer uses that model automatically. Override with `LOCALIZER_OLLAMA_URL` and `LOCALIZER_OLLAMA_MODEL` when needed. In the desktop app, **Draft Missing/Stale** targets unfinished locales; **Draft All** re-drafts every required locale (including Approved). Use **Invalidate** on a locale to mark it Stale for re-draft without deleting text. Review and **Approve** before **Export to Project**.

## Docs

- [Architecture](docs/architecture.md)
- [Catalog and format contract](docs/catalog-and-formats.md)
