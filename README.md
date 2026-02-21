# MetadataStudio

MetadataStudio is a deterministic CLI for editing metadata models and metadata instance data, then generating artifacts (SQL, C#, SSDT).

The project is built around one workspace contract:

- `metadata/workspace.json`
- `metadata/model.xml`
- `metadata/instance/<Entity>.xml`

## Why this exists

- Keep metadata in git as plain XML.
- Make model and data edits fast and scriptable.
- Fail hard on integrity issues.
- Generate stable outputs with minimal diff noise.

## Install and run

Build:

```powershell
dotnet build Metadata.Framework.sln
```

Run directly:

```powershell
dotnet run --project MetadataStudio.Cli/MetadataStudio.Cli.csproj -- help
```

Or use the launcher in repo root:

```powershell
.\meta.cmd help
```

Optional PowerShell profile install (so `meta ...` works without `./`):

```powershell
powershell -ExecutionPolicy Bypass -File .\install-meta.ps1
```

## Top-level command map

```powershell
meta help
```

Main groups:

- Workspace: `init`, `status`
- Model/Inspect: `check`, `list`, `view`, `query`, `graph`, `model`
- Instance mutations: `insert`, `bulk-insert`, `row update`, `row relationship`, `delete`
- Diff/merge: `instance diff`, `instance merge`, `instance diff-aligned`, `instance merge-aligned`
- Pipeline: `import`, `generate`

## Workspace quick start

Create a workspace:

```powershell
meta init .\Samples\MyWorkspace
```

Inspect it:

```powershell
meta status --workspace .\Samples\MyWorkspace
```

## Model XML contract (current)

`Id` is implicit on every entity.

- `Property name="Id"` is not written.
- `dataType="string"` is default and omitted.
- `isRequired="true"` is default and omitted.
- `isRequired` replaces legacy `isNullable`.

Example:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Model name="EnterpriseBIPlatform">
  <Entities>
    <Entity name="Cube">
      <Properties>
        <Property name="CubeName" />
        <Property name="Purpose" isRequired="false" />
        <Property name="RefreshMode" isRequired="false" />
      </Properties>
    </Entity>
    <Entity name="Measure">
      <Properties>
        <Property name="MeasureName" />
      </Properties>
      <Relationships>
        <Relationship entity="Cube" />
      </Relationships>
    </Entity>
  </Entities>
</Model>
```

## Instance XML contract (current)

Per row:

- Mandatory `Id` attribute.
- Relationship usages are attributes named `<TargetEntity>Id`.
- Non-relationship properties are child elements.
- Missing element means unset.
- Empty element means explicit empty string.

Example:

```xml
<?xml version="1.0" encoding="utf-8"?>
<EnterpriseBIPlatform>
  <Cubes>
    <Cube Id="1">
      <CubeName>Sales Performance</CubeName>
      <Purpose>Monthly revenue and margin tracking.</Purpose>
      <RefreshMode>Scheduled</RefreshMode>
    </Cube>
  </Cubes>
  <Measures>
    <Measure Id="1" CubeId="1">
      <MeasureName>Sales Amount</MeasureName>
    </Measure>
  </Measures>
</EnterpriseBIPlatform>
```

## Core workflows with examples

### 1) Inspect

```powershell
meta status --workspace .\Samples\CommandExamples
meta list entities --workspace .\Samples\CommandExamples
meta view entity Cube --workspace .\Samples\CommandExamples
meta view row Cube 1 --workspace .\Samples\CommandExamples
meta query Cube --contains CubeName Sales --workspace .\Samples\CommandExamples
meta graph stats --workspace .\Samples\CommandExamples
meta check --workspace .\Samples\CommandExamples
```

### 2) Model edits

```powershell
meta model add-entity SourceSystem --workspace .\Samples\CommandExamples
meta model add-property SourceSystem Name --required true --workspace .\Samples\CommandExamples
meta model add-relationship System SourceSystem --workspace .\Samples\CommandExamples
meta model rename-property Cube Purpose BusinessPurpose --workspace .\Samples\CommandExamples
```

### 3) Row edits

Insert explicit Id:

```powershell
meta insert Cube 10 --set "CubeName=Ops Cube" --set "RefreshMode=Manual" --workspace .\Samples\CommandExamples
```

Insert with identity allocation:

```powershell
meta insert Cube --auto-id --set "CubeName=Auto Cube" --workspace .\Samples\CommandExamples
```

Update one row:

```powershell
meta row update Cube 10 --set "Purpose=Operations reporting" --workspace .\Samples\CommandExamples
```

Set/clear/list relationship usage:

```powershell
meta row relationship set Measure 1 --to Cube 10 --workspace .\Samples\CommandExamples
meta row relationship list Measure 1 --workspace .\Samples\CommandExamples
meta row relationship clear Measure 1 --to-entity Cube --workspace .\Samples\CommandExamples
```

Delete one row:

```powershell
meta delete Cube 10 --workspace .\Samples\CommandExamples
```

### 4) Bulk insert

TSV example file (`cube-upsert.tsv`):

```text
CubeName	Purpose	RefreshMode
Planning Cube	Annual planning	Manual
Daily Ops	Daily operational cube	Scheduled
```

Load with auto identity:

```powershell
meta bulk-insert Cube --from tsv --file .\cube-upsert.tsv --auto-id --workspace .\Samples\CommandExamples
```

### 5) Import and generate

Import XML into a new workspace:

```powershell
meta import xml .\Samples\SampleModel.xml .\Samples\SampleInstance.xml --new-workspace .\Samples\ImportedXml
```

Import SQL into a new workspace:

```powershell
meta import sql "Server=.;Database=EnterpriseBIPlatform;Trusted_Connection=True;TrustServerCertificate=True;" dbo --new-workspace .\Samples\ImportedSql
```

Generate artifacts:

```powershell
meta generate sql --out .\out\sql --workspace .\Samples\CommandExamples
meta generate csharp --out .\out\csharp --workspace .\Samples\CommandExamples
meta generate ssdt --out .\out\ssdt --workspace .\Samples\CommandExamples
```

## Instance diff and merge

### Equal-model mode

Requires left and right `model.xml` files to be identical.

```powershell
meta instance diff .\Samples\CommandExamplesDiffLeft .\Samples\CommandExamplesDiffRight
meta instance merge .\Samples\CommandExamplesDiffLeft .\Samples\CommandExamplesDiffRight.instance-diff
```

### Aligned mode

Compares mapped subsets across different models using an explicit alignment workspace.

```powershell
meta instance diff-aligned .\LeftWs .\RightWs .\AlignmentWs
meta instance merge-aligned .\TargetWs .\RightWs.instance-diff-aligned
```

## Determinism and integrity notes

- Writers are deterministic (stable ordering and formatting).
- Commands fail hard on invalid model/instance state.
- Missing value and explicit empty value are distinct.
- Relationship targets are single-target and validated.

## Command references

- Full surface and contracts: `COMMANDS.md`
- Real command transcript examples: `COMMANDS-EXAMPLES.md`
- Human output grammar: `OUTPUT-GRAMMAR.md`

## Tests

Run all tests:

```powershell
dotnet test Metadata.Framework.sln
```

Run CLI-focused tests only:

```powershell
dotnet test MetadataStudio.Core.Tests/MetadataStudio.Core.Tests.csproj
```
