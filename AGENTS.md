# Agent Instructions

Read these files before implementing features:

1. `README.md`
2. `docs/architecture.md`
3. `docs/catalog-and-formats.md`
4. `docs/implementation-plan.md`

## Product boundary

Localizer is a standalone, engine-neutral localization authoring and QA utility.

Do not introduce engine names, engine types, editor APIs, UI framework types, or model-client types into Core or Application. Integration details belong in replaceable infrastructure implementations or separate adapters.

The utility exchanges files with consuming applications. Do not assume that consumers load Localizer assemblies or share its .NET runtime.

## Dependency rules

- Core depends only on the base class library.
- Application depends on Core.
- Infrastructure depends on Application and Core.
- CLI and future UI hosts compose dependencies and invoke Application use cases.
- Core must not perform file I/O, network I/O, serialization, logging, or process management.
- Application ports belong beside the use cases that consume them, not in Infrastructure.

## Domain rules

- A key is stable and independent of source text.
- Locale identifiers follow BCP 47 and are normalized consistently.
- A missing translation is represented by absence, not a stored empty translation.
- Generated text always enters Draft state.
- Approval is an explicit human action and requires successful blocking validation.
- Stale is derived when a translation's source fingerprint differs from the current translation input fingerprint.
- Never overwrite or delete approved text when source text or relevant context changes.
- Store provider and model provenance for generated drafts, but do not make the domain depend on a provider SDK.
- Use neutral names such as `ExternalId`, `DeveloperNotes`, and `MessageSyntaxProfile`.

## Validation rules

- Parse supported message syntax. Do not validate nested messages with regular expressions alone.
- Compare data dependencies such as selectors, formatter names, and required formatter options.
- Allow human-language literals and locale-specific plural forms to differ.
- Validate markup balance and allowed tag and attribute sets without assuming identical word order.
- Treat malformed model output as rejected input.
- Validators return structured diagnostics with stable codes, severity, entry key, locale, and location where available.
- Blocking validation errors prevent approval and production export.

## Persistence and export rules

- The versioned catalog JSON is authoritative.
- CSV and compact runtime JSON are interchange or generated outputs.
- Write text files as UTF-8 without a byte order mark.
- Use deterministic property, entry, and locale ordering in generated files.
- Use invariant culture for machine-readable values.
- Save the catalog transactionally so an interrupted write cannot corrupt the previous version.
- Export must report invalid, missing, Draft, or Stale entries. Do not skip them silently.
- Import and export formats must not leak format-specific fields into the neutral domain when integration metadata can represent them.

## Implementation discipline

- Follow the milestones in `docs/implementation-plan.md`.
- Keep the first vertical slice usable from the CLI before adding a graphical interface.
- Add unit tests for every lifecycle transition and validation invariant.
- Add golden-file tests for each serializer and exporter.
- Avoid design patterns that do not answer a demonstrated change point.
- Keep public contracts small and document intentional compatibility breaks.
- Do not implement speculative engine project scanning.

## Definition of done

A feature is complete when:

- its behavior follows the documented domain rules;
- deterministic tests cover success and failure paths;
- cancellation and I/O failures are handled where relevant;
- generated output is reproducible;
- documentation is updated when a contract or decision changes;
- `dotnet build .\Localizer.slnx` and `dotnet test .\Localizer.slnx` pass.
