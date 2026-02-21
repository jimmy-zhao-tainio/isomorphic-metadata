# MetadataStudio CLI Command Spec v1

This spec defines the canonical `meta` command surface.

## Purpose

`meta` manages metadata model + instance data in git and generates SQL/C#/SSDT artifacts.

- Developer flow: pull -> edit metadata -> commit.
- CI/CD flow: build SSDT artifact -> drop metadata DB -> recreate metadata DB.
- Out of scope: reconcile/merge/migration engine.

## Workspace contract

Workspace discovery:
- Default: search upward from current directory.
- Override: `--workspace <path>`.

Canonical files:
- `metadata/workspace.json`
- `metadata/model.xml`
- `metadata/instance/<Entity>.xml`

## Instance XML contract

Every instance row uses one strict shape:

- Entity element name is the model entity name.
- Mandatory row identity attribute: `Id="<positive-integer-string>"`.
- Relationship usage is single-target and stored only as attributes:
  - attribute name: `<TargetEntity>Id`
  - value: positive integer string
  - omitted attribute means relationship is unset
- Non-relationship properties are stored only as child elements:
  - `<PropertyName>text</PropertyName>`
  - missing element means property is unset
  - present empty element means explicit empty string

Writer/reader rules:

- No other row attributes are allowed in strict mode.
- No relationship child elements are allowed in strict mode.
- No null-to-empty coercion at persistence boundaries.

## Determinism contract

Writers are byte-stable for identical logical state.

- UTF-8 (no BOM), LF line endings.
- Stable ordering for entities, properties, relationships, shards, rows, and attributes.
- Mutating commands normalize implicitly before save.

## Diff and merge identity rules

- Equal-model instance diff:
  - `meta instance diff <leftWorkspace> <rightWorkspace>`
  - hard-fails unless left/right `model.xml` files are byte-identical.
  - writes a normal workspace using fixed model template `MetadataStudio.Cli/Templates/InstanceDiffModel.Equal.xml`.
  - merge command: `meta instance merge <targetWorkspace> <diffWorkspace>`.
- Aligned instance diff:
  - `meta instance diff-aligned <leftWorkspace> <rightWorkspace> <alignmentWorkspace>`
  - supports model differences using explicit entity/property mappings from fixed alignment contract.
  - writes a normal workspace using fixed model template `MetadataStudio.Cli/Templates/InstanceDiffModel.Aligned.xml`.
  - merge command: `meta instance merge-aligned <targetWorkspace> <diffWorkspace>`.
- Diff semantics:
  - no persisted booleans, hashes, fingerprints, or timestamps.
  - no packed field formats.
  - set differences are represented by row presence (`*NotIn*` entities).
  - FK-like references use `<Entity>Id` naming.

## Row and relationship addressing

Row addressing:
- `<Entity> <Id>` only.

Relationship usage:
- `meta row relationship set <FromEntity> <FromId> --to <ToEntity> <ToId>`
- `meta row relationship clear <FromEntity> <FromId> --to-entity <ToEntity>`
- `meta row relationship list <FromEntity> <FromId>`

Semantics:
- `set` leaves exactly one usage to the target entity.
- `clear` removes all usage to the target entity (idempotent).

## Query filters

`meta query` supports:
- `--equals <Field> <Value>` (repeatable)
- `--contains <Field> <Value>` (repeatable)
- `--top <n>`

Filters are ANDed in provided order.

## Errors and exit codes

Human output:
- one clear failure line
- optional blocker/details
- optional single `Next:` line (last line)

JSON output (`--json`) keeps structured details.

Exit codes:
- `0` success
- `1` usage/argument error, diff has differences, or merge precondition conflict
- `2` integrity check failure
- `3` fingerprint mismatch
- `4` workspace/data/model/operation error
- `5` generation failure
- `6` internal error

## Global flags

- `--workspace <path>`
- `--json`
- `--strict`

## Command surface

Workspace:
- `meta init [<path>]`
- `meta status [--workspace <path>]`

Inspect:
- `meta list <entities|properties|relationships|tasks> ...`
- `meta view <entity|row> ...`
- `meta query <Entity> [--equals <Field> <Value>]... [--contains <Field> <Value>]... [--top <n>] [--workspace <path>]`
- `meta graph <stats|inbound> ...`
- `meta check [--workspace <path>]`
- `meta instance diff <leftWorkspace> <rightWorkspace>`
- `meta instance merge <targetWorkspace> <diffWorkspace>`
- `meta instance diff-aligned <leftWorkspace> <rightWorkspace> <alignmentWorkspace>`
- `meta instance merge-aligned <targetWorkspace> <diffWorkspace>`

Modify:
- `meta model <add-entity|rename-entity|add-property|rename-property|add-relationship|drop-property|drop-relationship|drop-entity> ...`
- `meta insert <Entity> [<Id>|--auto-id] --set Field=Value [--set Field=Value ...] [--workspace <path>]`
- `meta bulk-insert <Entity> [--from tsv|csv|jsonl] [--file <path>|--stdin] [--key Field[,Field2...]] [--auto-id] [--workspace <path>]`
- `meta row update <Entity> <Id> --set Field=Value [--set Field=Value ...] [--workspace <path>]`
- `meta delete <Entity> <Id> [--workspace <path>]`
- `meta row relationship <set|clear|list> ...`

Generate:
- `meta generate <sql|csharp|ssdt> --out <dir> [--workspace <path>]`
- `meta import xml <modelXmlPath> <instanceXmlPath> --new-workspace <path>`
- `meta import sql <connectionString> <schema> --new-workspace <path>`

Utility:
- `meta random create [options]`

## Diff/merge example

```powershell
meta instance diff .\Samples\CommandExamplesDiffLeft .\Samples\CommandExamplesDiffRight
# Output includes: DiffWorkspace: <path>

meta instance merge .\Samples\CommandExamplesDiffLeft "<path from diff output>"
```

## Compatibility rule

Docs and help prefer `meta instance ...` commands for diff/merge.
