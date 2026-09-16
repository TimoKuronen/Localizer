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

Application coordinates use cases and defines the ports they require. The project exists; ports and use cases land with each milestone:

- Create and update catalog entries.
- Merge an incoming source catalog.
- Determine missing and stale work.
- Request translation drafts.
- Run validation.
- Approve or return drafts for revision.
- Import and export catalogs.
- Produce coverage and validation summaries.

Examples of application ports include `ICatalogStore`, `ITranslationDraftProvider`, `ICatalogImporter`, `ICatalogExporter`, and `IClock` where time is part of recorded provenance.

### Localizer.Infrastructure

Infrastructure implements application ports:

- Transactional JSON catalog storage.
- Local model HTTP integration.
- Compact JSON and CSV importers and exporters (Unity Localization String Table CSV adapter implemented; additional adapters as required).
- Provider response parsing.
- Optional translation memory storage in a later milestone.

Infrastructure types must not become domain types.

### Hosts

The Avalonia desktop app is the primary host and composition root for version 1. An optional CLI host may reuse the same Application use cases later for automation and continuous integration.

No embedded server, background service, or terminal host is required. The desktop app calls Application use cases directly and reaches the user's already-running local model service over HTTP when drafting is enabled.

Hosts are responsible for configuration, dependency registration, cancellation, progress reporting, and presentation. Business rules do not belong in view models or optional command handlers.

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
Localizer.Desktop (primary) or optional Localizer.Cli
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

The translation input fingerprint must be deterministic and versioned. It should include fields whose change can invalidate a translation:

- Source text.
- Message syntax profile.
- Developer notes or structured context marked as translation-relevant.
- Constraints that affect wording.
- Glossary or voice profile revision when those features are introduced.

It must not include operational metadata such as timestamps or validation results.

Changing a fingerprint input never deletes or overwrites translations. Existing translations become stale until revised and approved against the new fingerprint.

## Message syntax

There is no message syntax that is natively portable across all consumers. Localizer therefore models syntax as a profile selected by catalog or entry.

Initial profiles are limited:

- `plain`: no placeholders.
- `composite`: indexed placeholders compatible with .NET composite formatting (`{0}`, `{1}`, with `{{` / `}}` escapes). Named placeholders are out of scope for version 1.

Additional profiles can provide parsers for nested selectors, formatters, plurals, and conditions. A profile must expose a structural representation that validators can compare without requiring translated literal text to remain identical.

Automatic conversion between profiles is out of scope unless a converter can prove that the source and target semantics are equivalent.

## Validation pipeline

Validation is deterministic and ordered:

1. Catalog invariants: unique keys, valid locales, required fields.
2. Syntax parse: balanced and valid constructs for the selected profile.
3. Placeholder structure: required selectors and multiplicity for indexed `composite` text.
4. Constraints: configured length, line, or forbidden-term policies.
5. Export policy: required locales are current, approved, and error-free.

Version 1 defers markup validation and advanced formatter-option parity until a profile that requires them is introduced.

Validators return diagnostics rather than booleans. A diagnostic contains a stable code, severity, message, key, locale, and optional source location.

Character count, grapheme count, UTF-8 byte count, and rendered width are different constraints. Each configured constraint must identify its measurement. Rendered width is deferred because it requires fonts and a rendering environment.

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
- Configurable CSV for spreadsheet and tool interchange when a later milestone adds it.
- Additional adapters introduced only when a real consumer requires them.

Consumers should not depend on the authoritative catalog schema when a smaller production contract is sufficient.

## Compatibility strategy

The utility and its consumers may use different runtimes and programming languages. Compatibility is provided by documented UTF-8 file contracts, not shared binaries.

The utility currently targets .NET 10. If a future integration genuinely requires a shared managed assembly, define a small separate contract package targeting the required portable API surface. Do not lower or multi-target the entire application speculatively.

## Accepted decisions

- Engine-neutral terminology throughout the core product.
- File exchange as the primary integration boundary.
- JSON as the initial authoritative store.
- GUI-first vertical slice with Avalonia as the primary host.
- Explicit human approval.
- Derived Missing and Stale statuses.
- Parser-based, profile-aware message validation for `plain` and indexed `composite`.
- Deterministic, policy-controlled export.

## Deferred decisions

- Full CLI automation surface.
- Configurable generic CSV interchange beyond the Unity Localization adapter.
- Translation memory storage.
- Glossary and voice profile schema.
- Additional message syntax profiles.
- Markup validation beyond current profile needs.
- Portable Object and other specialized interchange formats.
- Typed key generation.
- Project scanning adapters.
- Visual layout validation.
- Multi-user history and collaboration.
