# isomorphic-metadata

`isomorphic-metadata` is a deterministic metadata backend. The canonical representation is an XML workspace on disk (git-friendly), but you can round-trip: import from SQL, generate SQL/C#/SSDT, and load/save model instances via generated C# APIs for tooling.

This repo ships two CLI tools:

`meta` (Meta CLI): workspace/model/instance operations, diff/merge, import, generate.  
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
Edits: mutate models and instance data (`model ...`, `insert`, `delete`, `bulk-insert`, `instance update`, `instance relationship set|list`, `instance diff`, `instance merge`).  
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
dotnet run --project Meta.Cli/Meta.Cli.csproj -- help
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

`metadata/model.xml` defines the schema for your metadata workspace: which entities exist, what properties they have, and how entities relate to each other.

A model has one root `<Model name="...">` and then:

- `<Entity>` defines a record type (like a table).
- `name="Cube"` is the singular name.
- `plural="Cubes"` controls how instances are grouped in instance XML (and exposed in generated APIs). If omitted, plural defaults to `<EntityName>s`.
- `<Properties>` lists scalar fields for the entity. `dataType="string"` is the default and omitted; properties are required by default (`isRequired="true"`), and optional fields use `isRequired="false"`.
- `<Relationships>` lists required foreign-key style references to other entities. A relationship points to a target entity and is required by default. In instance XML it becomes `${TargetEntity}Id` by default. If you need multiple relationships to the same target, specify `role="..."` and it becomes `${Role}Id`.
- `Id` is implicit on every entity, so `Property name="Id"` is not written.

Example:

```xml
<Model name="EnterpriseBIPlatform">
  <Entities>
    <Entity name="Measure" plural="Measures">
      <Properties>
        <Property name="MeasureName" />
      </Properties>
      <Relationships>
        <Relationship entity="Cube" />
        <Relationship entity="Cube" role="SourceCube" />
      </Relationships>
    </Entity>
  </Entities>
</Model>
```

### Instance XML

`metadata/instance/*.xml` stores the data for a model.

The root element is the model name (for example `<EnterpriseBIPlatform>`).

Each entity's instances are grouped under a plural container element (for example `<Cubes>`, `<Measures>`). Inside the container, each record is written as the singular entity element (for example `<Cube ...>`, `<Measure ...>`).

Every record must have an `Id="..."` attribute.

Relationships are stored as `...Id` attributes on the record. By default a relationship to `Cube` is stored as `CubeId="..."`. If the relationship has a role like `role="SourceCube"`, it is stored as `SourceCubeId="..."`.

Scalar properties are written as child elements. Missing element means unset. An empty element means explicit empty string.

Example:

```xml
<Measures>
  <Measure Id="1" CubeId="10" SourceCubeId="11">
    <MeasureName>Sales Amount</MeasureName>
    <Notes />
  </Measure>
</Measures>
```

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
meta model add-property SourceSystem Name --required true --default-value Unknown --workspace .\Samples\CommandExamples
meta model add-relationship System SourceSystem --default-id 1 --workspace .\Samples\CommandExamples
meta model rename-property Cube Purpose BusinessPurpose --workspace .\Samples\CommandExamples
```

Instance edits:

```powershell
meta insert Cube 10 --set "CubeName=Ops Cube" --set "RefreshMode=Manual" --workspace .\Samples\CommandExamples
meta insert Cube --auto-id --set "CubeName=Auto Cube" --workspace .\Samples\CommandExamples

meta instance update Cube 10 --set "Purpose=Operations reporting" --workspace .\Samples\CommandExamples

meta instance relationship set Measure 1 --to Cube 10 --workspace .\Samples\CommandExamples
meta instance relationship list Measure 1 --workspace .\Samples\CommandExamples

meta delete Cube 10 --workspace .\Samples\CommandExamples
```

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

MetaSchema is the separate schema/canonical-catalog toolchain.

It handles schema extraction and sanctioned catalogs that `meta` can treat as metadata workspaces.

Current status: `meta-schema extract sqlserver` is implemented as a scaffold and does not query SQL Server yet.

### TypeConversionCatalog

`TypeConversionCatalog` is a workspace that models a canonical type system (`Meta`) and mappings to/from platform type systems (SqlServer, Synapse, Snowflake, SSIS, CSharp). The model centers around TypeSystems/DataTypes, facets, and mapping rules.

Key entities include `TypeSystem`, `DataType`, `TypeSpec`, `TypeMapping`, `TypeMappingCondition`, `TypeMappingFacetTransform`, `Setting`, and `ConversionImplementation`.

#### Seeded conversion table excerpt: Meta -> SqlServer

| Meta type | SqlServer type | Lossiness | Implementation |
|---|---|---|---|
| AnsiString | varchar | Exact | Sql.Cast |
| AnsiStringFixedLength | char | Exact | Sql.Cast |
| Binary | varbinary | Exact | Sql.Cast |
| Boolean | bit | Exact | Sql.Identity |
| Date | date | Exact | Sql.Identity |
| DateTime2 | datetime2 | Exact | Sql.Cast |
| DateTimeOffset | datetimeoffset | Exact | Sql.Cast |
| Decimal | decimal | Exact | Sql.Cast |
| Guid | uniqueidentifier | Exact | Sql.Identity |
| Int32 | int | Exact | Sql.Identity |
| Int64 | bigint | Exact | Sql.Identity |
| Object | sql_variant | Lossy | Sql.Convert |
| String | nvarchar | Exact | Sql.Cast |
| StringFixedLength | nchar | Exact | Sql.Cast |
| Time | time | Exact | Sql.Cast |
| Xml | xml | Exact | Sql.Identity |

#### Seeded conversion table excerpt: SqlServer -> SSIS

| SqlServer type | SSIS type | Lossiness | Implementation |
|---|---|---|---|
| bigint | DT_I8 | Exact | Ssis.DataConversion |
| bit | DT_BOOL | Exact | Ssis.DataConversion |
| datetime2 | DT_DBTIMESTAMP2 | Exact | Ssis.DataConversion |
| datetimeoffset | DT_DBTIMESTAMPOFFSET | Exact | Ssis.DataConversion |
| decimal | DT_NUMERIC | Exact | Ssis.DataConversion |
| int | DT_I4 | Exact | Ssis.DataConversion |
| nvarchar | DT_WSTR | Exact | Ssis.DataConversion |
| smallint | DT_I2 | Exact | Ssis.DataConversion |
| time | DT_DBTIME2 | Exact | Ssis.DataConversion |
| tinyint | DT_UI1 | Exact | Ssis.DataConversion |
| uniqueidentifier | DT_GUID | Exact | Ssis.DataConversion |
| xml | DT_NTEXT | Lossy | Ssis.DataConversion |

#### Defaulting rules excerpt

Some platform-defaulting rules are encoded as mappings with conditions/transforms. (`Length=-1` means MAX.)

| Meta type | SqlServer type | When | Then |
|---|---|---|---|
| Decimal | decimal | Precision missing | Precision=18 |
| Decimal | decimal | Scale missing | Scale=0 |
| Time | time | TimePrecision missing | TimePrecision=7 |
| DateTime2 | datetime2 | TimePrecision missing | TimePrecision=7 |
| DateTimeOffset | datetimeoffset | TimePrecision missing | TimePrecision=7 |
| AnsiString | varchar | Length >= 8001 | Length=-1 |
| String | nvarchar | Length >= 4001 | Length=-1 |
| Binary | varbinary | Length >= 8001 | Length=-1 |

#### Commands

```powershell
meta-schema help
meta-schema extract sqlserver --help
meta-schema seed type-conversion --new-workspace .\MetaSchema.Catalogs\TypeConversionCatalog

meta check --workspace .\MetaSchema.Catalogs\TypeConversionCatalog
meta list entities --workspace .\MetaSchema.Catalogs\TypeConversionCatalog
meta query TypeMapping --contains Name Meta. --workspace .\MetaSchema.Catalogs\TypeConversionCatalog
```

## References

Full command surface and contracts: `COMMANDS.md`  
Transcript-style examples: `COMMANDS-EXAMPLES.md`

## Tests

```powershell
dotnet test Metadata.Framework.sln
```
