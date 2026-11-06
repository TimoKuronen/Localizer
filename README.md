# Localizer

Local-first localization authoring and QA utility for .NET applications and games. A local model proposes drafts; deterministic code owns identity, validation, approval, and export. Consumers exchange files — they do not load Localizer assemblies.

[![CI](https://github.com/TimoKuronen/Localizer/actions/workflows/ci.yml/badge.svg)](https://github.com/TimoKuronen/Localizer/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

![Localizer desktop app](docs/images/desktop.png)

## Highlights

- Engine-neutral catalog with stable keys, BCP 47 locales, and Draft/Approved workflow
- Derived Missing and Stale status from fingerprints so approved text survives source changes
- Deterministic validators for `plain` and indexed `composite` placeholders (`{0}`, `{1}`)
- Local Ollama drafting through a provider port; drafts stay unapproved until a human Approves
- Compact per-locale runtime JSON export and Unity Localization String Table CSV import/export
- Avalonia desktop host for catalog authoring, review, and project-folder handoff
- Keyboard review: F8 next draft, Shift+F8 previous draft, F9 approve the highlighted locale
- Layered Core / Application / Infrastructure / Desktop solution; dependencies point inward

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

Pipeline intent: `Sources -> Neutral catalog -> Drafting and QA -> Review -> Generated outputs`.

Projects: `Localizer.Core` (domain, lifecycle, validation), `Localizer.Application` (ports and use cases), `Localizer.Infrastructure` (JSON persistence, Ollama drafting, compact locale export, Unity CSV adapters), `Localizer.Desktop` (Avalonia composition root), plus Core/Application/Infrastructure/Desktop test projects.

Details: [docs/architecture.md](docs/architecture.md)

## Stack

- C# / .NET 10
- Avalonia
- [Ollama](https://ollama.com/) local HTTP API
- System.Text.Json
- NUnit

## Configuration

Drafting needs a running Ollama server with at least one chat-capable model installed. Embedding-only models are ignored.

| Variable | Default | Purpose |
|----------|---------|---------|
| `LOCALIZER_OLLAMA_URL` | `http://127.0.0.1:11434/` | Ollama server address |
| `LOCALIZER_OLLAMA_MODEL` | Sole chat-capable model | Model to draft with; required when several are installed |

## Docs

- [Architecture](docs/architecture.md)
- [Catalog and format contract](docs/catalog-and-formats.md)

## License

[MIT](LICENSE)
