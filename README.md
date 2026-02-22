# isomorphic-metadata

`isomorphic-metadata` is a deterministic, git-first backend for metadata. You keep a **model** and **instance data** in a workspace on disk, validate and edit it with `meta`, and generate artifacts (SQL, C#, SSDT) with minimal diff noise.

This repo ships two CLI tools:

`meta` (MetadataStudio CLI): workspace/model/instance operations, diff/merge, import, generate.  
`meta-schema` (MetaSchema CLI): schema extraction and sanctioned catalogs (for example `TypeConversionCatalog`).

## Workspace contract

A workspace is a directory containing:

`metadata/workspace.xml`  
`metadata/model.xml`  
`metadata/instance/...`

Instance data may be sharded: multiple instance files can contain rows for the same entity; load merges those shards and save preserves existing shard file layout (new rows for an entity are written to that entity's primary shard).

## What `meta` supports

`meta` operates on workspaces and provides four broad capabilities.

Workspace operations: create and inspect workspaces (`init`, `status`).  
Validation and inspection: check integrity and explore model/instance (`check`, `list`, `view`, `query`, `graph`).  
Edits: mutate models and instance data (`model ...`, `insert`, `delete`, `bulk-insert`, `instance update`, `instance relationship set|list|clear`, `instance diff`, `instance merge`).  
Pipelines: import and generate (`import ...`, `generate ...`).

## One sample across XML, SQL, and C#

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

### Generated SQL (`meta generate sql`)

`meta generate sql` writes `schema.sql` and `data.sql` (deterministic DDL and INSERT scripts).

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
```

### Generated C# (`meta generate csharp`)

Generated models have two usage modes.

Consumer usage: `<ModelName>.<EntityPlural>` iterates over a built-in generated snapshot (no disk I/O).  
Tooling usage: `<ModelName>Model.LoadFromXmlWorkspace(...)` loads an explicit instance object from a workspace and can be saved back.

```csharp
using GeneratedModel;
using System;

// Consumer usage (built-in generated snapshot; no disk I/O):
foreach (var m in EnterpriseBIPlatform.Measures)
{
    Console.WriteLine(m.Id);
    Console.WriteLine(m.Cube.Name); // relationships are required
}

// Tooling usage (explicit workspace I/O):
var model = EnterpriseBIPlatformModel.LoadFromXmlWorkspace(@"C:\repo\Metadata\Samples");
EnterpriseBIPlatformModel.SaveToXmlWorkspace(model, @"C:\repo\Metadata\Samples");
```

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

## XML contracts summary

### Model XML

`Id` is implicit on every entity (so `Property name="Id"` is not written).  
`dataType="string"` is the default and omitted.  
`isRequired="true"` is the default and omitted.  
`Entity plural="..."` is optional; default is `<EntityName>s`.

Relationships are declared as:

`<Relationship entity="TargetEntity" />`

To support multiple relationships to the same target, or to control the instance attribute / SQL column name, relationships may specify a usage name and/or explicit column name:

`<Relationship entity="TargetEntity" name="UsageName" column="ColumnName" />`

Defaults are:

If `name` is omitted, usage name defaults to the target entity name.  
If `column` is omitted, the column/attribute defaults to `${name}Id`.

### Instance XML

Root element is the model name (for example `<EnterpriseBIPlatform>`).  
Under root, each entity uses a plural container element (for example `<Cubes>`, `<Measures>`).  
Inside each container, each instance uses the singular entity name (for example `<Cube ...>`, `<Measure ...>`).

Instance `Id` attribute is mandatory.  
Declared relationships are required and stored as instance attributes named by the relationship column (defaults described above).  
Scalar properties are child elements. Missing property element means unset; empty property element means explicit empty string.

## Core workflows with examples

Inspect:

```powershell
meta status --workspace .\Samples\CommandExamples
meta list entities --workspace .\Samples\CommandExamples
meta view entity Cube --workspace .\Samples\CommandExamples
meta view instance Cube 1 --workspace .\Samples\CommandExamples
meta query Cube --contains CubeName Sales --workspace .\Samples\CommandExamples
meta graph stats --workspace .\Samples\CommandExamples
meta check --workspace .\Samples\CommandExamples
```

Model edits:

```powershell
meta model add-entity SourceSystem --workspace .\Samples\CommandExamples
meta model add-property SourceSystem Name --required true --workspace .\Samples\CommandExamples
meta model add-relationship System SourceSystem --workspace .\Samples\CommandExamples
meta model rename-property Cube Purpose BusinessPurpose --workspace .\Samples\CommandExamples
```

Instance edits:

```powershell
meta insert Cube 10 --set "CubeName=Ops Cube" --set "RefreshMode=Manual" --workspace .\Samples\CommandExamples
meta insert Cube --auto-id --set "CubeName=Auto Cube" --workspace .\Samples\CommandExamples

meta instance update Cube 10 --set "Purpose=Operations reporting" --workspace .\Samples\CommandExamples

meta instance relationship set Measure 1 --to Cube 10 --workspace .\Samples\CommandExamples
meta instance relationship list Measure 1 --workspace .\Samples\CommandExamples
meta instance relationship clear <FromEntity> <FromId> --to-entity <ToEntity> --workspace <path>

meta delete Cube 10 --workspace .\Samples\CommandExamples
```

`meta instance relationship clear` is available for usage cleanup, but declared relationships are required; clearing a required relationship usage fails validation.

Import and generate:

```powershell
meta import xml .\Samples\SampleModel.xml .\Samples\SampleInstance.xml --new-workspace .\Samples\ImportedXml
meta import sql "Server=.;Database=EnterpriseBIPlatform;Trusted_Connection=True;TrustServerCertificate=True;" dbo --new-workspace .\Samples\ImportedSql

meta generate sql --out .\out\sql --workspace .\Samples\CommandExamples
meta generate csharp --out .\out\csharp --workspace .\Samples\CommandExamples
meta generate ssdt --out .\out\ssdt --workspace .\Samples\CommandExamples
```

Instance diff and merge:

```powershell
meta instance diff .\LeftWs .\RightWs
meta instance merge .\TargetWs .\RightWs.instance-diff
```

## MetaSchema

MetaSchema is the separate schema/canonical-catalog toolchain (schema extraction plus sanctioned catalogs like `TypeConversionCatalog`).

```powershell
meta-schema help
meta-schema extract sqlserver --help
meta-schema seed type-conversion --new-workspace .\MetaSchema.Catalogs\TypeConversionCatalog
```

## References

Full command surface and contracts: `COMMANDS.md`  
Transcript-style examples: `COMMANDS-EXAMPLES.md`

## Tests

```powershell
dotnet test Metadata.Framework.sln
```
