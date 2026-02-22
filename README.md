# MetadataStudio

MetadataStudio is a deterministic CLI for editing metadata models and metadata instance data, then generating artifacts (SQL, C#, SSDT).

The project is built around one workspace contract:

- `metadata/workspace.xml`
- `metadata/model.xml`
- `metadata/instance/<Entity>.xml`

## Format first: one sample across XML, SQL, and C#

### XML model (`metadata/model.xml`)

```xml
<?xml version="1.0" encoding="utf-8"?>
<Model name="EnterpriseBIPlatform">
  <Entities>
    <Entity name="Cube" plural="Cubes">
      <Properties>
        <Property name="CubeName" />
        <Property name="Purpose" isRequired="false" />
        <Property name="RefreshMode" isRequired="false" />
      </Properties>
    </Entity>
    <Entity name="Measure" plural="Measures">
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

### XML instance (`metadata/instance/*.xml`)

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

### Equivalent SQL model + instance

```sql
CREATE TABLE [dbo].[Cube] (
  [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
  [CubeName] NVARCHAR(256) NOT NULL,
  [Purpose] NVARCHAR(256) NULL,
  [RefreshMode] NVARCHAR(256) NULL
);

CREATE TABLE [dbo].[Measure] (
  [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
  [MeasureName] NVARCHAR(256) NOT NULL,
  [CubeId] INT NOT NULL
);

ALTER TABLE [dbo].[Measure]
ADD CONSTRAINT [FK_Measure_Cube_CubeId]
FOREIGN KEY ([CubeId]) REFERENCES [dbo].[Cube]([Id]);

INSERT INTO [dbo].[Cube] ([CubeName], [Purpose], [RefreshMode])
VALUES (N'Sales Performance', N'Monthly revenue and margin tracking.', N'Scheduled');

INSERT INTO [dbo].[Measure] ([MeasureName], [CubeId])
VALUES (N'Sales Amount', 1);
```

### Equivalent generated C# shape

```csharp
public sealed class Cube
{
    public int Id { get; }
    public string CubeName { get; }
    public string Purpose { get; }
    public string RefreshMode { get; }
    public string Name { get; } // alias from CubeName
}

public sealed class Measure
{
    public int Id { get; }
    public string MeasureName { get; }
    public int? CubeId { get; }
    public Cube Cube { get; }
    public string Name { get; } // alias from MeasureName
}

public sealed class Cubes : IEnumerable<Cube>
{
    public Cube GetId(int id);
    public bool TryGetId(int id, out Cube instance);
}

public sealed class Measures : IEnumerable<Measure>
{
    public Measure GetId(int id);
    public bool TryGetId(int id, out Measure instance);
}

public sealed class EnterpriseBIPlatform
{
    public Cubes Cubes { get; }
    public Measures Measures { get; }
}

public static class EnterpriseBIPlatformModel
{
    public static EnterpriseBIPlatform Current { get; }
    public static EnterpriseBIPlatform LoadFromXml(string workspacePath);
    public static EnterpriseBIPlatform LoadFromSql(DbConnection connection, string schemaName = "dbo");
}
```

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
- Model: `check`, `graph`, `list`, `model`, `view`
- Instance: `instance`, `insert`, `delete`, `query`, `bulk-insert`
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

## XML contracts summary

Model XML:

- `dataType="string"` is default and omitted.
- `isRequired="true"` is default and omitted.
- `Entity plural="..."` is optional; default is `<EntityName>s`.

Instance XML (strict contract):

- Root element is the model name (for example `<EnterpriseBIPlatform>`).
- Under root, each entity uses a plural container element (for example `<Cubes>`, `<Measures>`).
- Inside each container, each instance uses the singular entity name (for example `<Cube ...>`, `<Measure ...>`).
- Sharded instance mode supports split entity files: multiple `metadata/instance/*.xml` files may each contain instances for the same entity container, and load merges them.
- Save preserves split layout by instance shard origin; new instances are assigned to that entity's primary shard file.
- Direct instance elements under root are invalid; instances must be inside their plural container.
- Instance `Id` attribute is mandatory.
- Declared relationships are required and stored as instance attributes named `<TargetEntity>Id`.
- Missing relationship attributes for declared relationships are invalid.
- Scalar properties are child elements.
- Missing property element means unset; empty property element means explicit empty string.

## Core workflows with examples

### 1) Inspect

```powershell
meta status --workspace .\Samples\CommandExamples
meta list entities --workspace .\Samples\CommandExamples
meta view entity Cube --workspace .\Samples\CommandExamples
meta view instance Cube 1 --workspace .\Samples\CommandExamples
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

### 3) Instance edits

Insert explicit Id:

```powershell
meta insert Cube 10 --set "CubeName=Ops Cube" --set "RefreshMode=Manual" --workspace .\Samples\CommandExamples
```

Insert with identity allocation:

```powershell
meta insert Cube --auto-id --set "CubeName=Auto Cube" --workspace .\Samples\CommandExamples
```

Update one instance:

```powershell
meta instance update Cube 10 --set "Purpose=Operations reporting" --workspace .\Samples\CommandExamples
```

Set/list relationship usage:

```powershell
meta instance relationship set Measure 1 --to Cube 10 --workspace .\Samples\CommandExamples
meta instance relationship list Measure 1 --workspace .\Samples\CommandExamples
```

Declared relationships are required; use `instance relationship set` to retarget, or delete the instance if it should no longer exist.

Delete one instance:

```powershell
meta delete Cube 10 --workspace .\Samples\CommandExamples
```

### 4) Bulk insert

TSV example file (`cube-insert.tsv`):

```text
CubeName	Purpose	RefreshMode
Planning Cube	Annual planning	Manual
Daily Ops	Daily operational cube	Scheduled
```

Load with auto identity:

```powershell
meta bulk-insert Cube --from tsv --file .\cube-insert.tsv --auto-id --workspace .\Samples\CommandExamples
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

Mental model:

- `meta instance diff <left> <right>` produces a diff artifact that describes how to transform Left instance data into Right instance data.
- `meta instance merge <target> <diffWorkspace>` applies that Left -> Right transformation to a target workspace (typically the same workspace used as Left when the diff was created).
- The diff artifact is a deterministic, scoped change-set: it captures only instance/property/relationship differences, not a full copy of the right workspace instance.
- Merge applies that scoped change-set to the target workspace and validates integrity while applying.

What the diff captures:

- Instance identity by `(Entity, Id)`.
- Scalar property value differences.
- Relationship usage differences via `<TargetEntity>Id` attributes.
- Instance-level insert/update/delete outcomes implied by Left-only and Right-only identities.

Instance semantics are preserved:

- Unset property (missing element) and explicit empty string (`<PropertyName></PropertyName>`) are different states.
- Diff and merge preserve that distinction; they do not collapse missing into empty.

Constraints and failure modes:

- Left and Right `model.xml` files must be identical for equal-model mode.
- Merge fails hard if preconditions or integrity rules are violated (for example, applying a change that would delete a referenced instance).

```powershell
meta instance diff .\Samples\CommandExamplesDiffLeft .\Samples\CommandExamplesDiffRight
meta instance merge .\Samples\CommandExamplesDiffLeft .\Samples\CommandExamplesDiffRight.instance-diff
```

### Aligned mode

Aligned mode supports model differences by introducing an explicit alignment workspace.

- The alignment workspace defines correspondence (and therefore comparison scope) between left and right models.
- `meta instance diff-aligned` includes only aligned scope in the diff artifact.
- `meta instance merge-aligned` applies only within that aligned scope on the target workspace.
- Integrity rules still apply; merge-aligned fails hard on invalid operations.
- In practice this means aligned mode can merge mapped subsets across different models while leaving out-of-scope data unchanged.

```powershell
meta instance diff-aligned .\LeftWs .\RightWs .\AlignmentWs
meta instance merge-aligned .\TargetWs .\RightWs.instance-diff-aligned
```

Determinism:

- Diff artifacts are written with stable ordering and formatting.
- Merged instance output is produced by deterministic writers, so identical logical state yields stable file output.

## Determinism and integrity notes

- Writers are deterministic (stable ordering and formatting).
- Commands fail hard on invalid model/instance state.
- Missing value and explicit empty value are distinct.
- Relationship targets are single-target and validated.
- Declared relationships are required; missing relationship usage is invalid.

## MetaSchema

MetaSchema is the separate schema/canonical-catalog toolchain (schema extraction plus sanctioned catalogs like `TypeConversionCatalog`).

```powershell
meta-schema help
meta-schema extract sqlserver --help
meta-schema seed type-conversion --new-workspace .\MetaSchema.Catalogs\TypeConversionCatalog
```

## Generated C# API usage

Generated model API shape:

- Root model class named by model (example: `EnterpriseBIPlatform`)
- Loader class `<ModelName>Model` (example: `EnterpriseBIPlatformModel`) owns one static instance via `Current`
- Entity collections exposed as plural names (`<EntityName>s` by default, or model `plural=` override)
- Rows expose `Id`, scalar properties, `<TargetEntity>Id`, and `<TargetEntity>` navigation

Example:

```csharp
using GeneratedModel;
using System;
using System.Data.Common;

// Load (or refresh) the loader-owned singleton instance and get it:
var model = EnterpriseBIPlatformModel.LoadFromXml(@"C:\repo\Metadata\Samples");
var current = EnterpriseBIPlatformModel.Current;

// Or load from SQL:
// DbConnection conn = ...;
// var model = EnterpriseBIPlatformModel.LoadFromSql(conn, "dbo");

foreach (var m in model.Measures)
{
    Console.WriteLine(m.Id);
    Console.WriteLine(m.Cube?.Name);
}

var m0 = model.Measures.GetId(1);
var cubeName = m0.Cube?.Name;
```

Notes:

- `EnterpriseBIPlatformModel.Current` returns the same model instance object on every call.
- `LoadFromXml` / `LoadFromSql` populate that same object and return it.
- Collections implement `IEnumerable<T>` and provide `GetId(int)` / `TryGetId(int, out T)`.

## Command references

- Full surface and contracts: `COMMANDS.md`
- Real command transcript examples: `COMMANDS-EXAMPLES.md`

## Tests

Run all tests:

```powershell
dotnet test Metadata.Framework.sln
```

Run CLI-focused tests only:

```powershell
dotnet test MetadataStudio.Core.Tests/MetadataStudio.Core.Tests.csproj
```
