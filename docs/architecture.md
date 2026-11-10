# Architecture and Decisions

## Product definition

Localizer is a compiler-like content pipeline for localized text:

```text
Sources -> Neutral catalog -> Drafting and QA -> Review -> Generated outputs
```

Authors and a local language model can propose text. Deterministic code owns catalog identity, lifecycle, validation, approval eligibility, persistence, and export.

The application is a separate utility. Consuming applications exchange files with it and do not need to load its assemblies.

## Architectural boundaries

### Localizer.Core

Core contains behavior that remains true regardless of user interface, storage format, model provider, or consuming platform:

- Catalog, entry, translation, locale, and context value objects.
- Translation lifecycle and source fingerprint rules.
- Validation policies, diagnostics, and pure validators.
- Message syntax abstractions and parsed structural models.
- Domain errors and invariants.

Core has no file, network, serialization, logging, UI, or provider SDK dependencies.

### Localizer.Application

Application coordinates use cases and defines the ports they require:

- Create and update catalog entries and catalog settings.
- Determine missing and stale work.
- Request translation drafts.
- Run validation.
- Approve or invalidate translations.
- Import and export catalogs (compact locale JSON and Unity CSV).
- Produce coverage and validation summaries.
- Persist project-folder bindings for host handoff.

Application ports include `ICatalogStore`, `ITranslationDraftProvider`, `ICatalogExporter`, `IUnityCsvReader`, `IUnityCsvExporter`, `IProjectFolderSettingsStore`, and `IClock` where time is part of recorded provenance.

### Localizer.Infrastructure

Infrastructure implements application ports:

- Transactional JSON catalog storage.
- Transactional project-folder settings JSON storage.
- Local model HTTP integration (Ollama).
- Compact locale JSON exporter and Unity Localization String Table CSV import/export.
- Provider response parsing.

Infrastructure types must not become domain types.

### Hosts

The Avalonia desktop app is the host and composition root.

No embedded server, background service, or terminal host is required. The desktop app calls Application use cases directly and reaches the user's already-running local model service over HTTP when drafting is enabled.

Hosts are responsible for configuration, dependency registration, cancellation, progress reporting, and presentation. Business rules do not belong in view models.

## Dependency direction

```text
Localizer.Core
      ^
      |
Localizer.Application
      ^
      |
Localizer.Infrastructure
      ^
      |
Localizer.Desktop
```

Infrastructure may also reference Core to implement serializers and validators, but no inner layer references an outer layer.

## Domain model

A catalog contains project-level settings and uniquely keyed entries.

An entry contains:

- Stable string key.
- Optional category.
- Source text and source locale.
- Developer notes and structured translation context.
- Optional constraints.
- Message syntax profile.
- Optional external identifiers stored by namespace.
- Translations keyed by normalized locale.

A translation contains:

- Localized text.
- Workflow state: Draft or Approved.
- Fingerprint of the translation inputs on which it was based.
- Provenance such as origin, provider, model, and timestamps.
- Latest validation summary or enough information to reproduce it.

`Missing` and `Stale` are derived statuses rather than mutable workflow states:

- Missing: no translation exists for a required locale.
- Draft: a translation exists but is not approved.
- Approved: the translation is approved and its fingerprint is current.
- Stale: a translation exists but its stored fingerprint differs from the current fingerprint.

This avoids contradictory states such as an approved translation also being stored as stale. Approved content remains preserved while the derived status blocks release.

## Fingerprints and staleness

The translation input fingerprint must be deterministic and versioned. It includes fields whose change can invalidate a translation:

- Source text.
- Message syntax profile.
- Developer notes or structured context marked as translation-relevant.
- Constraints that affect wording.

It must not include operational metadata such as timestamps or validation results.

Changing a fingerprint input never deletes or overwrites translations. Existing translations become stale until revised and approved against the new fingerprint.

## Message syntax

There is no message syntax that is natively portable across all consumers. Localizer therefore models syntax as a profile selected by catalog or entry.

Two profiles exist:

- `plain`: no placeholders.
- `composite`: indexed placeholders compatible with .NET composite formatting (`{0}`, `{1}`, with `{{` / `}}` escapes).

A profile provides parsers for nested selectors, formatters, plurals, and conditions. A profile must expose a structural representation that validators can compare without requiring translated literal text to remain identical.

Profiles are not converted automatically.

## Validation pipeline

Validation is deterministic and ordered:

1. Catalog invariants: unique keys, valid locales, required fields.
2. Syntax parse: balanced and valid constructs for the selected profile.
3. Placeholder structure: required selectors and multiplicity for indexed `composite` text.
4. Constraints: configured length, line, or forbidden-term policies.
5. Export policy: required locales are current, approved, and error-free.

Validators return diagnostics rather than booleans. A diagnostic contains a stable code, severity, message, key, locale, and optional source location.

Character count, grapheme count, UTF-8 byte count, and rendered width are different constraints. Each configured constraint must identify its measurement.

## Model integration boundary

Model output is untrusted input.

The drafting workflow:

1. Selects Missing or Stale work.
2. Builds a structured request from source text, context, constraints, glossary, and locale.
3. Requests structured output through an application port.
4. Correlates every response with the requested key and locale.
5. Rejects malformed, incomplete, duplicated, or unexpected results.
6. Stores accepted results only as Draft with provenance.
7. Runs deterministic validation and reports diagnostics.

The model cannot approve translations, mutate keys, alter source text, or authorize export.

## Persistence and interchange

The authoritative project file is versioned UTF-8 JSON. It favors complete round-tripping and reviewable changes over minimal size.

Generated production formats are separate:

- Compact locale JSON for simple runtime lookup.
- Unity Localization String Table CSV for Unity interchange.

Consumers depend on the generated production contracts, not the authoritative catalog schema.

## Compatibility strategy

The utility and its consumers may use different runtimes and programming languages. Compatibility is provided by documented UTF-8 file contracts, not shared binaries.

The utility targets .NET 10.

## Accepted decisions

- Engine-neutral terminology throughout the core product.
- File exchange as the primary integration boundary.
- JSON as the authoritative store.
- Avalonia desktop app as the host.
- Explicit human approval.
- Derived Missing and Stale statuses.
- Parser-based, profile-aware message validation for `plain` and indexed `composite`.
- Deterministic, policy-controlled export.
