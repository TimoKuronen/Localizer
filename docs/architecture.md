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
- Approve or invalidate translations, including translations of repeated sources.
- List catalog entries that a source file no longer contains.
- Import and export catalogs (compact locale JSON, source strings JSON import, and Unity CSV).
- Produce coverage and validation summaries.
- Persist project-folder bindings for host handoff.

Application ports include `ICatalogStore`, `ITranslationDraftProvider`, `ICatalogExporter`, `IDraftPreviewWriter`, `ISourceStringsReader`, `IStringTableCsvReader`, `IStringTableCsvExporter`, `IProjectFolderSettingsStore`, `IDraftModelCatalog`, `IDraftModelPreference`, and `IClock` where time is part of recorded provenance.

### Localizer.Infrastructure

Infrastructure implements application ports:

- Transactional JSON catalog storage.
- Transactional project-folder settings JSON storage.
- Transactional draft model preference JSON storage.
- Local model HTTP integration (Ollama).
- Compact locale JSON exporter, draft preview writer, source strings JSON reader, and the Unity Localization String Table CSV reader and exporter, which implement the engine-neutral string table CSV ports. The Unity-specific headers and the `unity` external id namespace live here; the merge use case takes the namespace from the document the reader returns.
- Provider response parsing and prompt construction, kept in separate types from the HTTP client.

Infrastructure types must not become domain types.

### Hosts

The Avalonia desktop app is the host and composition root.

No embedded server, background service, or terminal host is required. The desktop app calls Application use cases directly and reaches the user's already-running local model service over HTTP for drafting.

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
- Developer notes and structured context.
- Constraints that affect wording.

It must not include operational metadata such as timestamps or validation results.

Changing a fingerprint input never deletes or overwrites translations. Existing translations become stale until approved against the new fingerprint, either as revised text or as the unchanged text. Approving unchanged Stale text takes a second confirmation and passes the same blocking validation; a blocked approval leaves the translation Stale.

## Repeated sources

Entries repeat a source when their source text, ignoring newline style, and syntax profile are equal. The grouping is derived from the catalog and never stored.

Approving a translation offers the repeating entries whose translation for that locale is not Approved. Entries with the same fingerprint are preselected unless their stored text was approved earlier; entries whose notes, context, or constraints differ are not. Each chosen entry receives the text and its provenance, and is validated and approved on its own. A currently Approved translation is never overwritten this way.

## Entry removal

An entry leaves the catalog only through an explicit user action. Comparison with a source file lists the entries whose keys the file omits, and the user chooses which of them to remove, together with their translations. The file is taken to hold every key of the catalog. Importing source strings never removes entries.

## Message syntax

There is no message syntax that is natively portable across all consumers. Localizer therefore models syntax as a profile selected by catalog or entry.

Two profiles exist:

- `plain`: no placeholders.
- `composite`: indexed placeholders compatible with .NET composite formatting (`{0}`, `{1}`, with `{{` / `}}` escapes).

Each profile has a parser that produces a structural representation of literal and placeholder segments, which validators compare without requiring translated literal text to remain identical.

Profiles are not converted automatically.

## Validation pipeline

Validation is deterministic and ordered:

1. Catalog invariants: unique keys, distinct target locales that exclude the source locale, required fields.
2. Syntax parse: balanced and valid constructs for the selected profile.
3. Constraints: configured length, line, required-term, or forbidden-term policies.
4. Placeholder structure: placeholder indices and multiplicity for indexed `composite` text.
5. Export policy: required locales are current, approved, and error-free.

Validators return diagnostics rather than booleans. A diagnostic contains a stable code, severity, message, key, locale, and optional source location.

Grapheme count, UTF-8 byte count, and line count are separate constraints. Each configured constraint identifies its measurement.

## Model integration boundary

Model output is untrusted input.

The drafting workflow:

1. Selects Missing and Stale work, or every required translation when all locales are drafted.
2. Builds a structured request from source text, context, constraints, glossary, and locale.
3. Requests structured output through an application port.
4. Correlates every response with the requested key and locale.
5. Rejects malformed, incomplete, duplicated, or unexpected results.
6. Stores accepted results only as Draft with provenance.
7. Runs deterministic validation and reports diagnostics.

An accepted result replaces the stored translation for its entry and locale. This includes Stale text approved earlier and, when all locales are drafted, current Approved translations.

Queued entries with the same fingerprint share one request per locale, and its result is stored for each of them. When drafting Missing and Stale work, an Approved translation with the same fingerprint is copied as a Draft instead of generating text, unless the target already holds that text. Copies are stored only when the provider run succeeds.

The catalog-level project context and glossary travel with every request and reach the model as part of its standing instructions. Source text is delimited from developer notes and context, which are labelled as reference only. A result that repeats prompt text, has more lines than the source, or is far longer than the source is rejected for that item and reported with the other failures. Sampling uses a low temperature and a bounded context window.

The model cannot approve translations, mutate keys, alter source text, or authorize export.

The drafting model is the environment override when one is set, otherwise the saved preference, otherwise the sole chat-capable model the server reports. Several installed models with no override or preference fail with the list of candidates rather than guessing.

## Persistence and interchange

The authoritative project file is versioned UTF-8 JSON. It favors complete round-tripping and reviewable changes over minimal size.

Generated production formats are separate:

- Compact locale JSON for simple runtime lookup.
- Unity Localization String Table CSV for Unity interchange.

Source-side input from consumers without an engine format is a source strings JSON file; it carries source text and authoring metadata and never translations.

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
