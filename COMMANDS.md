# Meta CLI Command Spec v1

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
- `metadata/workspace.xml`
- `metadata/model.xml`
- `metadata/instance/<Entity>.xml`

## Instance XML contract

Every instance element uses one strict shape:

- Entity element name is the model entity name.
- Mandatory identity attribute: `Id="<positive-integer-string>"`.
- Relationship usage is single-target and stored only as attributes:
  - attribute name: `<TargetEntity>Id`
  - value: positive integer string
  - omitted attribute means relationship usage is missing (and fails `meta check` for required relationships)
- Non-relationship properties are stored only as child elements:
  - `<PropertyName>text</PropertyName>`
  - missing element means property is unset
  - present empty element means explicit empty string

Writer/reader rules:

- No other instance attributes are allowed in strict mode.
- No relationship child elements are allowed in strict mode.
- No null-to-empty coercion at persistence boundaries.

## Determinism contract

Writers are byte-stable for identical logical state.

- UTF-8 (no BOM), LF line endings.
- Stable ordering for entities, properties, relationships, shards, instances, and attributes.
- Mutating commands normalize implicitly before save.

## Diff and merge identity rules

- Equal-model instance diff:
  - `meta instance diff <leftWorkspace> <rightWorkspace>`
  - hard-fails unless left/right `model.xml` files are byte-identical.
  - writes a normal workspace using fixed model template `Meta.Cli/Templates/InstanceDiffModel.Equal.xml`.
  - merge command: `meta instance merge <targetWorkspace> <diffWorkspace>`.
- Aligned instance diff:
  - `meta instance diff-aligned <leftWorkspace> <rightWorkspace> <alignmentWorkspace>`
  - supports model differences using explicit entity/property mappings from fixed alignment contract.
  - writes a normal workspace using fixed model template `Meta.Cli/Templates/InstanceDiffModel.Aligned.xml`.
  - merge command: `meta instance merge-aligned <targetWorkspace> <diffWorkspace>`.
- Diff semantics:
  - no persisted booleans, hashes, fingerprints, or timestamps.
  - no packed field formats.
  - set differences are represented by instance presence (`*NotIn*` entities).
  - FK-like references use `<Entity>Id` naming.

## Instance and relationship addressing

Instance addressing:
- `<Entity> <Id>` only.

Relationship usage:
- `meta instance relationship set <FromEntity> <FromId> --to <ToEntity> <ToId>`
- `meta instance relationship list <FromEntity> <FromId>`

Semantics:
- `set` leaves exactly one usage to the target entity.

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
- `--strict`

## Command surface

Workspace:
- `meta init [<path>]`
- `meta status [--workspace <path>]`

Inspect:
- `meta list <entities|properties|relationships|tasks> ...`
- `meta view <entity|instance> ...`
- `meta query <Entity> [--equals <Field> <Value>]... [--contains <Field> <Value>]... [--top <n>] [--workspace <path>]`
- `meta graph <stats|inbound> ...`
- `meta check [--workspace <path>]`
- `meta instance diff <leftWorkspace> <rightWorkspace>`
- `meta instance merge <targetWorkspace> <diffWorkspace>`
- `meta instance diff-aligned <leftWorkspace> <rightWorkspace> <alignmentWorkspace>`
- `meta instance merge-aligned <targetWorkspace> <diffWorkspace>`

Modify:
- `meta model <suggest|refactor|add-entity|rename-entity|add-property|rename-property|add-relationship|drop-property|drop-relationship|drop-entity> ...`
  - `suggest` usage: `meta model suggest [--show-keys] [--show-blocked] [--explain] [--print-commands] [--workspace <path>]`
  - default suggest output is actionable-only (eligible relationship suggestions + compact summary)
  - `--show-keys` includes candidate business keys
  - `--show-blocked` includes blocked relationship candidates
  - `--explain` includes Evidence/Stats/Why detail blocks
  - `--print-commands` prints copy/paste `meta model refactor property-to-relationship ...` commands for eligible suggestions
  - `refactor` usage: `meta model refactor property-to-relationship --source <Entity.Property> --target <Entity> --lookup <Property> [--role <Role>] [--drop-source-property] [--workspace <path>]`
  - refactor is atomic (model + instance): if any precondition fails, nothing is written.
  - `add-property` usage: `meta model add-property <Entity> <Property> [--required true|false] [--default-value <Value>] [--workspace <path>]`
  - `--default-value` is required when adding a required property to an entity that already has rows (used for backfill).
  - `add-relationship` usage: `meta model add-relationship <FromEntity> <ToEntity> [--role <RoleName>] [--default-id <ToId>] [--workspace <path>]`
  - `--default-id` is required when `<FromEntity>` already has rows (used for backfill).
- `meta insert <Entity> [<Id>|--auto-id] --set Field=Value [--set Field=Value ...] [--workspace <path>]`
- `meta bulk-insert <Entity> [--from tsv|csv] [--file <path>|--stdin] [--key Field[,Field2...]] [--auto-id] [--workspace <path>]`
- `meta instance update <Entity> <Id> --set Field=Value [--set Field=Value ...] [--workspace <path>]`
- `meta delete <Entity> <Id> [--workspace <path>]`
- `meta instance relationship <set|list> ...`

Generate:
- `meta generate <sql|csharp|ssdt> --out <dir> [--workspace <path>]`
  - `meta generate csharp --out <dir> [--workspace <path>] [--tooling]`
  - `--tooling` emits optional `<ModelName>.Tooling.cs` helpers (C# mode only).
- `meta import xml <modelXmlPath> <instanceXmlPath> --new-workspace <path>`
- `meta import sql <connectionString> <schema> --new-workspace <path>`
- `meta import csv <csvFile> --entity <EntityName> [--workspace <path> | --new-workspace <path>]`

## Command quick reference (what for + example)

Workspace:

| Command | Use when | Example |
|---|---|---|
| `meta init [<path>]` | You need a new workspace scaffold. | `meta init .` |
| `meta status` | You need a quick workspace/model/instance summary. | `meta status` |

Inspect and validate:

| Command | Use when | Example |
|---|---|---|
| `meta check` | You need integrity validation before commit/publish. | `meta check` |
| `meta list entities` | You need the entity inventory. | `meta list entities` |
| `meta list properties <Entity>` | You need scalar schema of one entity. | `meta list properties Cube` |
| `meta list relationships <Entity>` | You need outgoing relationship schema of one entity. | `meta list relationships Measure` |
| `meta list tasks` | You need task file inventory from `metadata/tasks`. | `meta list tasks` |
| `meta view entity <Entity>` | You need a schema card view for one entity. | `meta view entity Cube` |
| `meta view instance <Entity> <Id>` | You need a row-level view by identity. | `meta view instance Cube 1` |
| `meta query <Entity> ...` | You need filtered row lookup. | `meta query Cube --contains CubeName Sales --top 20` |
| `meta graph stats` | You need relationship graph health/complexity stats. | `meta graph stats --top 10 --cycles 5` |
| `meta graph inbound <Entity>` | You need to see inbound references before model edits. | `meta graph inbound Cube --top 20` |

Model mutation and refactor:

| Command | Use when | Example |
|---|---|---|
| `meta model suggest` | You need read-only eligible relationship promotions. | `meta model suggest` |
| `meta model suggest --print-commands` | You need copy/paste refactor commands for eligible suggestions. | `meta model suggest --print-commands` |
| `meta model refactor property-to-relationship ...` | You need atomic model+instance promotion from scalar property to required relationship. | `meta model refactor property-to-relationship --source Order.ProductCode --target Product --lookup ProductCode --drop-source-property` |
| `meta model add-entity <Name>` | You need a new entity definition. | `meta model add-entity SourceSystem` |
| `meta model rename-entity <Old> <New>` | You need to rename an entity. | `meta model rename-entity SourceSystem Source` |
| `meta model drop-entity <Entity>` | You need to remove an entity (when not blocked by data/references). | `meta model drop-entity SourceSystem` |
| `meta model add-property <Entity> <Property> ...` | You need to add scalar schema (with optional required/backfill controls). | `meta model add-property Cube Purpose --required true --default-value Unknown` |
| `meta model rename-property <Entity> <Old> <New>` | You need to rename a scalar property. | `meta model rename-property Cube Purpose BusinessPurpose` |
| `meta model drop-property <Entity> <Property>` | You need to remove scalar schema. | `meta model drop-property Cube Description` |
| `meta model add-relationship <From> <To> ...` | You need a required relationship (with optional role/backfill id). | `meta model add-relationship Measure Cube --default-id 1` |
| `meta model drop-relationship <From> <To>` | You need to remove declared relationship schema. | `meta model drop-relationship Measure Cube` |

Instance mutation:

| Command | Use when | Example |
|---|---|---|
| `meta insert <Entity> <Id> --set ...` | You need to insert one row with explicit Id. | `meta insert Cube 10 --set "CubeName=Ops Cube"` |
| `meta insert <Entity> --auto-id --set ...` | You need to insert one row with generated Id. | `meta insert Cube --auto-id --set "CubeName=Ops Cube"` |
| `meta bulk-insert <Entity> ...` | You need to insert many rows from tsv/csv. | `meta bulk-insert Cube --from tsv --file .\\cube.tsv --key Id` |
| `meta instance update <Entity> <Id> --set ...` | You need to update one row by Id. | `meta instance update Cube 1 --set RefreshMode=Manual` |
| `meta instance relationship set <FromEntity> <FromId> --to <ToEntity> <ToId>` | You need to set exact-one relationship usage for one row. | `meta instance relationship set Measure 1 --to Cube 2` |
| `meta instance relationship list <FromEntity> <FromId>` | You need to inspect relationship usages on one row. | `meta instance relationship list Measure 1` |
| `meta delete <Entity> <Id>` | You need to delete one row by Id. | `meta delete Cube 10` |

Diff and merge:

| Command | Use when | Example |
|---|---|---|
| `meta instance diff <left> <right>` | Models are byte-identical and you need instance diff artifact. | `meta instance diff .\\LeftWorkspace .\\RightWorkspace` |
| `meta instance merge <target> <diffWorkspace>` | You need to apply equal-model diff artifact. | `meta instance merge .\\TargetWorkspace .\\RightWorkspace.instance-diff` |
| `meta instance diff-aligned <left> <right> <alignment>` | Models differ and you have explicit alignment workspace. | `meta instance diff-aligned .\\LeftWorkspace .\\RightWorkspace .\\AlignmentWorkspace` |
| `meta instance merge-aligned <target> <diffWorkspace>` | You need to apply aligned diff artifact. | `meta instance merge-aligned .\\TargetWorkspace .\\RightWorkspace.instance-diff-aligned` |

Import and generate:

| Command | Use when | Example |
|---|---|---|
| `meta import xml <modelXml> <instanceXml> --new-workspace <path>` | Source-of-truth is XML model+instance files. | `meta import xml .\\model.xml .\\instance.xml --new-workspace .\\ImportedWorkspace` |
| `meta import sql <connectionString> <schema> --new-workspace <path>` | Source-of-truth is SQL metadata. | `meta import sql "Server=...;Database=...;..." dbo --new-workspace .\\ImportedWorkspace` |
| `meta import csv <csvFile> --entity <EntityName> ...` | Landing import of one file into one entity + rows. | `meta import csv .\\landing.csv --entity Landing --new-workspace .\\ImportedWorkspace` |
| `meta generate sql --out <dir>` | You need deterministic SQL schema/data scripts. | `meta generate sql --out .\\out\\sql` |
| `meta generate csharp --out <dir>` | You need dependency-free generated consumer C# API. | `meta generate csharp --out .\\out\\csharp` |
| `meta generate csharp --out <dir> --tooling` | You need optional generated tooling helpers for workspace/io. | `meta generate csharp --out .\\out\\csharp --tooling` |
| `meta generate ssdt --out <dir>` | You need SSDT project output. | `meta generate ssdt --out .\\out\\ssdt` |

## Diff/merge example

```powershell
meta instance diff .\Samples\CommandExamplesDiffLeft .\Samples\CommandExamplesDiffRight
# Output includes: DiffWorkspace: <path>

meta instance merge .\Samples\CommandExamplesDiffLeft "<path from diff output>"
```


