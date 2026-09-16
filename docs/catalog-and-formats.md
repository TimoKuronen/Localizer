# Catalog and Format Contract

This document defines the intended version 1 semantics. Exact C# type names may evolve during implementation, but changes must preserve these rules or update this document deliberately.

## Authoritative catalog

The catalog is a human-readable UTF-8 JSON document. It stores authoring metadata, workflow state, provenance, and translations. It is not optimized for runtime delivery.

Illustrative shape:

```json
{
  "schemaVersion": 1,
  "catalogId": "runner-game",
  "sourceLocale": "en",
  "requiredLocales": ["es", "fi"],
  "defaultSyntaxProfile": "composite",
  "entries": [
    {
      "key": "menu_start_button",
      "category": "ui",
      "sourceText": "Start",
      "developerNotes": "Main menu button. Use a short verb.",
      "context": {
        "surface": "main-menu",
        "intent": "begin a new run"
      },
      "constraints": {
        "maxGraphemes": 12
      },
      "externalIds": {},
      "translations": {
        "fi": {
          "text": "Aloita",
          "state": "approved",
          "basedOnFingerprint": "sha256:example",
          "origin": "human"
        }
      }
    }
  ]
}
```

The example fingerprint is a placeholder and is not a valid production value.

## Catalog-level fields

### `schemaVersion`

Required positive integer. Readers reject unsupported future versions with a clear error. Schema migrations are explicit and tested.

### `catalogId`

Required stable identifier for the catalog. It is not a file path or display name.

### `sourceLocale`

Required BCP 47 language tag identifying the source language.

### `requiredLocales`

Ordered set of target BCP 47 language tags. A source locale cannot also be a target locale. Duplicate or differently cased equivalents are invalid.

### `defaultSyntaxProfile`

Required profile applied when an entry does not override it.

### `entries`

Required collection of uniquely keyed entries. Persistence and export use ordinal key ordering for deterministic output.

## Entry fields

### `key`

Required stable string identifier. Keys are independent of source wording. Version 1 allows ASCII letters, digits, underscore, hyphen, and period.

Version 1 policy: case-sensitive storage with a case-insensitive collision diagnostic. This prevents ambiguous use on consumers with different key rules.

### `category`

Optional organizational label. It must not control core behavior unless a documented policy refers to it explicitly.

### `sourceText`

Required source-language text. Empty source text is allowed only when the entry sets `allowEmptyText` to true; otherwise it is a validation error.

### `allowEmptyText`

Optional boolean entry flag defaulting to false. When false, empty `sourceText` and empty translation `text` are validation errors. When true, empty strings are permitted for that entry.

### `developerNotes`

Optional human-authored translation guidance. It contains intent, ambiguity resolution, speaker information, or layout guidance. The name remains neutral and maps to format-specific comment fields only at import or export boundaries.

### `context`

Optional structured string values used for prompting and review. Initial implementations should support a small, extensible map rather than a large fixed hierarchy.

Context fields that influence translation participate in the source fingerprint.

### `constraints`

Optional deterministic restrictions. Version 1 may support:

- Maximum grapheme count.
- Maximum UTF-8 byte count.
- Maximum line count.
- Required or forbidden terms.

Every length constraint names its measurement. Do not label a UTF-16 code-unit count as a character count.

### `syntaxProfile`

Optional entry override of the catalog default. Validators use the selected profile to parse source and translation messages.

### `externalIds`

Optional mapping from a namespace to an opaque string value. These identifiers support round-tripping with external tools without introducing their terminology or numeric assumptions into the domain.

An importer or exporter owns the interpretation of its namespace. Core enforces only configured uniqueness and length rules.

### `translations`

Mapping from normalized target locale to translation object. Absence means Missing. Do not create placeholder translation objects with empty text to represent missing work.

## Translation fields

### `text`

Localized text. Empty text requires the entry `allowEmptyText` flag to be true.

### `state`

Stored workflow state:

- `draft`: generated or human-edited text awaiting approval.
- `approved`: explicitly approved text.

Missing and Stale are derived statuses and are never persisted as workflow states.

### `basedOnFingerprint`

Required fingerprint of the translation-relevant source inputs used to produce or approve this text.

If it differs from the entry's current calculated fingerprint, the translation is Stale regardless of its stored workflow state.

### `origin`

Required provenance category such as `human`, `model`, or `import`.

Model-generated drafts should also record provider name, model name, and generation time in an optional provenance object. Provider-specific response objects must not be persisted directly.

## Effective status

Status is calculated in this order:

1. No translation object: Missing.
2. Fingerprint mismatch: Stale.
3. Stored state is Draft: Draft.
4. Stored state is Approved: Approved.

Validation results are separate from status. An Approved translation can acquire a validation error after policy changes; export policy must still block it.

## Fingerprint contract

Version 1 fingerprints use a named algorithm and a canonical input representation:

```text
sha256:<lowercase hexadecimal digest>
```

Canonical input includes:

- fingerprint contract version;
- source locale;
- exact source text;
- effective syntax profile;
- translation-relevant context and notes;
- wording constraints.

Map keys are sorted ordinally. Before hashing, normalize newlines by converting `\r\n` and `\r` to `\n`. Operational metadata and translations are excluded.

Changing canonicalization requires a new fingerprint contract version and migration tests.

## Locale handling

- Accept valid BCP 47 tags at boundaries.
- Store one canonical representation: trim whitespace, convert underscores to hyphens, lowercase the language subtag, title-case 4-letter script subtags, uppercase 2-letter region subtags, and lowercase other subtags.
- Compare locale identity using the stored canonical value.
- Do not assume every locale is a two-letter language code.
- Keep fallback behavior outside the translation lifecycle unless explicitly configured.

## Message syntax profiles

Version 1 ships two profiles:

### `plain`

No placeholders. Source and translation text are literal strings subject only to plain-text constraints.

### `composite`

Indexed placeholders compatible with .NET composite string formatting:

- Placeholders use zero-based indices: `{0}`, `{1}`, and so on.
- Literal braces are escaped as `{{` and `}}`.
- Validators compare placeholder index sequences and multiplicity between source and translation.
- Named placeholders, ICU MessageFormat selectors, and plural branches are out of scope for version 1.

Additional profiles can be added later when a real consumer requires them.

Each profile provides:

- Source and translation parser.
- Structural node model.
- Rules for selector and formatter equivalence.
- Escaping rules.
- Diagnostic codes and source locations.

The profile compares executable structure while permitting translated literals to change. Locale-specific plural branches may differ where the profile defines that behavior.

Regular expressions may help tokenize simple syntax, but they must not be the sole parser for nested expressions.

## Production runtime JSON

The initial production exporter writes one file per locale as a compact key-value object:

```json
{"menu_start_button":"Aloita","score_label":"Pisteet: {0}"}
```

Contract:

- UTF-8 without byte order mark.
- Deterministic ordinal key ordering.
- No authoring metadata, provenance, workflow state, or comments.
- JSON escaping performed by a standards-compliant serializer.
- Only current, Approved, error-free translations.
- Export fails with structured diagnostics when required entries are not releasable.
- Each exported file ends with one trailing newline and is covered by golden tests.

The filename pattern is host configuration, not domain behavior.

## CSV interchange

CSV is configurable interchange, not authoritative storage.

Minimum capabilities:

- Key and source text columns.
- Optional developer notes and external identifier columns.
- One or more locale columns.
- Configurable headers and delimiter.
- Standards-compliant quoting of delimiters, quotes, and line breaks.
- Explicit encoding and newline policy.
- Deterministic row and locale ordering.
- Import diagnostics for duplicate keys, locales, malformed rows, and conflicting external identifiers.

The Unity Localization preset below defines merge behavior explicitly. A fully configurable CSV layout beyond that preset remains deferred (see roadmap Band D); the only required rule for any CSV import is that it must never silently overwrite current Approved content.

## Unity Localization String Table CSV

Adapter for `com.unity.localization` String Table CSV export/import. This is a consumer-specific interchange format, not authoritative catalog storage.

Illustrative shape:

```text
Key,Id,English(en),Spanish(es)
ui.example.title,10547617792,You won!,
ui.example.score,11222900737,Score: {0},
```

Contract:

- UTF-8 without byte order mark; one trailing newline on export.
- Required columns: `Key`, `Id`, one source locale column, zero or more target locale columns.
- Locale column headers use `{LanguageName}({localeCode})` (for example `English(en)`, `Spanish(es)`).
- `Id` maps to catalog `externalIds.unity` for round-trip with Unity-generated identifiers.
- Source import reads `Key`, `Id`, and source locale text only. Target locale columns from a source-side export are ignored so empty or stale consumer cells cannot overwrite catalog translations.
- Merge adds new keys, updates changed source text and Unity ids, and preserves existing translations (changed source marks translations Stale via fingerprints).
- Merge must never silently overwrite current Approved translations.
- Export is permissive: every catalog entry is written; source text and `Id` are always present; target locale cells contain approved translation text only, otherwise an empty cell.
- Deterministic ordinal row ordering by key.
- Import and export file names are host configuration (project folder binding), not domain behavior.

Additional engine adapters (Unreal, Portable Object, and others) should be separate importers/exporters rather than extensions of this Unity CSV contract.

## Save safety

Catalog persistence uses a safe replacement sequence:

1. Serialize and validate the complete new document.
2. Write it to a temporary file in the destination directory.
3. Flush and close the temporary file.
4. Replace the destination atomically where supported.
5. Preserve or report recovery information if replacement fails.

Do not partially update the authoritative file in place.

## Format evolution

- Every authoritative document carries `schemaVersion`.
- Unknown required fields or unsupported versions produce clear errors.
- Readers may ignore unknown optional fields only when the schema rules permit it.
- Migrations preserve keys, approved translations, fingerprints, and provenance.
- Golden fixtures cover reading old supported versions and writing the current version.
