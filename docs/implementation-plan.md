# Implementation Plan

Deliver one tested desktop vertical slice before adding optional automation hosts or specialized integrations.

## Progress

| Milestone | Status |
|-----------|--------|
| 0 Solution foundation | Done |
| 1 Catalog domain | Done |
| 2 Authoritative JSON persistence | Done |
| 3 Deterministic validation | Next |
| 4 Application use cases | Not started |
| 5 Avalonia desktop shell | Not started |
| 6 Local model drafting | Not started |
| 7 Review and approval | Not started |
| 8 Compact runtime export | Not started |
| 9 GUI dogfooding | Not started |

Optional later milestones: CLI automation host, configurable CSV interchange.

## Milestone 0: Solution foundation

Status: Done.

### Work

- Add `Localizer.Application` targeting .NET 10.
- Reference Core from Application.
- Reference Application and Core from Infrastructure.
- Reference Application and Infrastructure from CLI.
- Add application and infrastructure test projects when their first behaviors are introduced.
- Add repository-wide build settings only when they remove real duplication.
- Add a root `.gitignore` before source control initialization.

### Acceptance

- Dependency direction matches `docs/architecture.md`.
- Every project builds with nullable reference types enabled.
- The full solution builds and tests with one command.
- No inner project references Infrastructure or a host.

## Milestone 1: Catalog domain

Status: Done.

### Work

- Implement value objects for keys, locales, source text, and fingerprints.
- Implement catalog, entry, translation, context, constraints, provenance, and external identifiers.
- Implement Draft and Approved workflow states.
- Derive Missing and Stale effective statuses.
- Implement versioned fingerprint canonicalization.
- Return explicit domain errors for invalid operations.

### Tests

- Key and locale normalization and rejection cases.
- Duplicate key prevention.
- Every effective status case.
- Source, context, syntax, and constraint changes causing staleness.
- Operational metadata changes not causing staleness.
- Approved translations remaining preserved after source changes.

### Acceptance

- Core has no external package dependency unless a package is justified and portable.
- Domain behavior requires no file system, clock, network, or serializer.
- All lifecycle transitions are covered by deterministic tests.

## Milestone 2: Authoritative JSON persistence

Status: Done.

Implemented as Application `ICatalogStore` plus Infrastructure `JsonCatalogStore`, DTO mapping, transactional UTF-8 save, and `Localizer.Infrastructure.Tests` golden fixtures.

### Work

- Define version 1 persistence DTOs separate from domain objects.
- Implement mapping with complete validation diagnostics.
- Implement deterministic UTF-8 JSON reading and writing.
- Implement transactional save and clear unsupported-version errors.
- Add sample catalog fixtures for tests.

### Tests

- Round-trip all supported fields.
- Reject malformed, duplicate, and unsupported data.
- Preserve non-ASCII text and line endings according to policy.
- Verify no byte order mark.
- Golden-file output remains byte-for-byte stable.
- Simulated failed save does not damage the previous catalog.

### Acceptance

- A catalog can be created, saved, loaded, and compared without data loss.
- Persistence details do not appear in Core.

## Milestone 3: Deterministic validation

Status: Next.

Scope for version 1 is limited to what `plain` and indexed `composite` syntax profiles require. Markup validation and advanced formatter-option parity are deferred until a real consumer needs them.

### Work

- Define diagnostic codes, severity, location, key, and locale.
- Implement catalog-level validators.
- Implement `plain` and indexed `composite` syntax profiles.
- Implement parser-based placeholder index and multiplicity comparison.
- Implement grapheme, UTF-8 byte, and line constraints.
- Implement approval and export validation policies.

### Tests

- Missing, duplicated, renamed, and malformed placeholders.
- Escaped delimiters and literal braces.
- Unicode combining characters and multi-byte text.
- Multiple diagnostics returned in stable order.

### Acceptance

- Blocking diagnostics prevent approval.
- Validation never mutates catalog content.
- Every diagnostic has a stable machine-readable code.

## Milestone 4: Application use cases

Host-agnostic orchestration that both the desktop UI and any future automation host can call.

### Work

- Add use cases for create/open/save catalog, entry create/update, and locale configuration.
- Add work-queue queries for Missing and Stale translations.
- Add validation and status summary use cases.
- Keep ports beside the use cases that consume them.

### Tests

- Use in-memory or fake store implementations for deterministic application tests.
- Cover success and failure paths for each use case.

### Acceptance

- Catalog workflows do not require a terminal host or UI framework types.
- Use cases coordinate Core and ports without performing presentation.

## Milestone 5: Avalonia desktop shell

The primary composition root for version 1.

### Work

- Add `Localizer.Desktop` Avalonia project and dependency registration.
- Implement create/open/save catalog flows.
- Implement entry list with locale columns and effective status.
- Implement source and translation text editing.
- Surface validation diagnostics in the UI.
- Report progress and support cancellation for long-running operations.

### Acceptance

- A user can manage a small catalog without editing serialized workflow metadata manually.
- Business rules remain in Application and Core; view models stay thin.
- No embedded server or background service is required.

## Milestone 6: Local model drafting

### Work

- Define a provider-neutral draft request and response contract in Application.
- Implement the first local HTTP provider in Infrastructure.
- Build structured prompts from source, context, notes, constraints, locale, and syntax.
- Validate response correlation and schema.
- Store accepted generations as Draft with sanitized provenance.
- Add timeout, cancellation, retry, and partial-batch policies.
- Expose drafting controls and progress in the desktop UI.

### Tests

- Use a fake provider for deterministic application tests.
- Reject malformed, missing, duplicate, and unexpected results.
- Verify generated text can never enter Approved state automatically.
- Verify provider failures do not corrupt the catalog.
- Keep live-provider tests optional and separate from the default test suite.

### Acceptance

- Missing or Stale entries can receive local drafts from the desktop UI.
- Every accepted draft is validated and remains unapproved.
- No provider SDK type crosses into Application or Core.

## Milestone 7: Review and approval

### Work

- Show validation diagnostics per entry and locale in the desktop UI.
- Allow editing generated or human-authored draft text.
- Require explicit approval after successful blocking validation.
- Make Stale status visible without deleting approved text.

### Acceptance

- Approval is always an explicit human action in the UI.
- Blocking validation errors prevent approval.
- Approved translations remain preserved when source inputs change.

## Milestone 8: Compact runtime export

### Work

- Implement exporter selection through an Application port.
- Implement compact per-locale JSON.
- Add preflight reporting for unreleasable entries.
- Write generated files transactionally.
- Expose export actions and release-blocking feedback in the desktop UI.

### Tests

- Golden files for multiple locales and Unicode.
- Stable key ordering.
- Correct escaping.
- Metadata is absent.
- Missing, Draft, Stale, and invalid translations block export.

### Acceptance

- A complete approved catalog exports byte-for-byte reproducible runtime files.
- A failed export leaves previous production files intact.

## Milestone 9: GUI dogfooding

Before adding CSV interchange, a CLI host, or other integrations:

- Use the desktop app on one real game catalog.
- Record friction in authoring, review, diagnostics, drafting, and export.
- Confirm whether translation memory, glossary, or batch review is the next highest-value feature.
- Stabilize Application use cases required by more than one host.

## Optional later milestones

### CLI automation host

- Thin wrapper over existing Application use cases.
- Useful for scripts and continuous integration, not required for correctness.
- Can be added after Milestone 4 without changing Core or Application contracts.

### Configurable CSV interchange

- Configurable columns, locale mapping, delimiter, newline, and external identifiers.
- Safe merge policies and row-level diagnostics.
- Spreadsheet bridge only; not on the critical path for the first desktop release.

## Cross-cutting quality requirements

- All repository text files use UTF-8 without byte order mark.
- Public APIs use neutral terminology.
- I/O and network operations accept cancellation tokens.
- Generated output is deterministic.
- Errors identify the operation, file or entry, and safe recovery action.
- Unit tests do not require network access or a running local model.
- Integration tests use temporary directories and clean up after themselves.
- `dotnet build` and `dotnet test` are developer safeguards, not end-user terminal features.
- Documentation changes accompany contract changes.

## Features intentionally deferred

- Full CLI automation surface.
- Configurable CSV interchange.
- Translation memory.
- Glossary and character voice management.
- Additional syntax profiles.
- Markup validation beyond current profile needs.
- Specialized external adapters.
- Typed key generation.
- Automatic project scanning.
- Screenshot or rendered-width validation.
- Multi-user editing, cloud sync, and approval history.
