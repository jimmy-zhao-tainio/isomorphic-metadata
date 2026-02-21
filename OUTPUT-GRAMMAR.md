# MetadataStudio CLI Output Grammar (Human Mode)

This spec defines deterministic plain-text output for `meta` in non-`--json` mode.

## Success
- First line: `OK: <summary>`
- Optional detail lines: `Key: Value`
- Keep success output short and command-specific.

## Inspect
- Use compact headings (for example `Entity: Cube`, `Query: Cube`, `Status: ok`).
- Use stable table ordering for lists and result sets.
- Prefer minimal sections over verbose dumps.

## Failure
- First line must be human and direct:
  - `Cannot <action> <target>` for blocked operations.
  - or a short failure sentence for argument/input errors.
- Do not print `Where:` or `Hint:` labels in human mode.
- Do not print internal diagnostic codes/paths in human mode.
- Optional follow-up lines:
  - concrete blocker evidence
  - `Next: <specific command>` (at most one)

## JSON Failure (`--json`)
- Structured diagnostics stay in JSON only:
  - `status=error`
  - `code`
  - `message`
  - optional `where`
  - optional `hints`
  - optional detailed `issues` payload

## Determinism
- Stable ordering for entities/rows/blockers.
- No timestamps in human output.
- No machine-specific noise in checked-in examples.
