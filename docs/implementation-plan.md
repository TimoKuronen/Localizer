# Implementation Plan

Implement one tested vertical slice before adding a graphical interface or specialized integrations.

## Milestone 0: Solution foundation

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

### Work

- Define diagnostic codes, severity, location, key, and locale.
- Implement catalog-level validators.
- Implement `plain` and the first required placeholder syntax profile.
- Implement parser-based placeholder structure comparison.
- Implement balanced markup and allowed-tag validation.
- Implement grapheme, UTF-8 byte, and line constraints.
- Implement approval and export validation policies.

### Tests

- Missing, duplicated, renamed, malformed, and nested placeholders.
- Escaped delimiters and literal braces.
- Balanced, unbalanced, moved, and attribute-bearing markup.
- Unicode combining characters and multi-byte text.
- Multiple diagnostics returned in stable order.

### Acceptance

- Blocking diagnostics prevent approval.
- Validation never mutates catalog content.
- Every diagnostic has a stable machine-readable code.

## Milestone 4: CLI catalog workflow

### Work

- Add commands to initialize, inspect, validate, approve, and report catalog status.
- Make file locations and required locales explicit configuration.
- Support cancellation and non-zero exit codes for validation or I/O failures.
- Keep command handlers thin by invoking Application use cases.

### Acceptance

- A user can manage a small catalog without editing serialized workflow metadata manually.
- Human-readable output explains failures.
- Machine-readable exit codes support scripts and continuous integration.

## Milestone 5: Local model drafting

### Work

- Define a provider-neutral draft request and response contract in Application.
- Implement the first local HTTP provider in Infrastructure.
- Build structured prompts from source, context, notes, constraints, locale, and syntax.
- Validate response correlation and schema.
- Store accepted generations as Draft with sanitized provenance.
- Add timeout, cancellation, retry, and partial-batch policies.

### Tests

- Use a fake provider for deterministic application tests.
- Reject malformed, missing, duplicate, and unexpected results.
- Verify generated text can never enter Approved state automatically.
- Verify provider failures do not corrupt the catalog.
- Keep live-provider tests optional and separate from the default test suite.

### Acceptance

- Missing or Stale entries can receive local drafts.
- Every accepted draft is validated and remains unapproved.
- No provider SDK type crosses into Application or Core.

## Milestone 6: Compact runtime export

### Work

- Implement exporter selection through an Application port.
- Implement compact per-locale JSON.
- Add preflight reporting for unreleasable entries.
- Write generated files transactionally.
- Define deterministic filename and newline settings in host configuration.

### Tests

- Golden files for multiple locales and Unicode.
- Stable key ordering.
- Correct escaping.
- Metadata is absent.
- Missing, Draft, Stale, and invalid translations block export.

### Acceptance

- A complete approved catalog exports byte-for-byte reproducible runtime files.
- A failed export leaves previous production files intact.

## Milestone 7: Configurable CSV interchange

### Work

- Implement configurable columns, locale mapping, delimiter, newline, and external identifiers.
- Define safe merge policies.
- Produce row-level diagnostics.
- Preserve approved content during imports unless an explicit reviewed operation replaces it.

### Tests

- Quoted delimiters, quotes, line breaks, and non-ASCII text.
- Duplicate and conflicting rows.
- Round-trip supported metadata.
- Golden files for each supported dialect configuration.

### Acceptance

- CSV is usable with spreadsheet tools and configurable external consumers.
- Format-specific column names remain outside Core.

## Milestone 8: Usability evaluation

Before selecting a desktop UI framework or adding integrations:

- Use the CLI on one real catalog.
- Record friction in authoring, review, diagnostics, and export.
- Confirm whether translation memory, glossary, or batch review is the next highest-value feature.
- Stabilize Application use cases required by more than one host.

A desktop UI is justified only after these workflows and contracts are proven.

## Cross-cutting quality requirements

- All repository text files use UTF-8 without byte order mark.
- Public APIs use neutral terminology.
- I/O and network operations accept cancellation tokens.
- Generated output is deterministic.
- Errors identify the operation, file or entry, and safe recovery action.
- Unit tests do not require network access or a running local model.
- Integration tests use temporary directories and clean up after themselves.
- Documentation changes accompany contract changes.

## Features intentionally deferred

- Graphical desktop interface.
- Translation memory.
- Glossary and character voice management.
- Additional syntax profiles.
- Specialized external adapters.
- Typed key generation.
- Automatic project scanning.
- Screenshot or rendered-width validation.
- Multi-user editing, cloud sync, and approval history.
