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
Model analysis and guided refactor: read-only relationship inference (`model suggest`) and atomic promotion command (`model refactor property-to-relationship`).  
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

`meta generate csharp` always emits dependency-free POCOs in `GeneratedMetadata`:
- `<ModelName>.cs` (model container)
- `<Entity>.cs` (one file per entity)

Optional tooling helpers are emitted only when `--tooling` is passed:
- `<ModelName>.Tooling.cs` with workspace/import helper methods backed by Meta runtime services.

```csharp
using GeneratedMetadata;
using System.Threading.Tasks;

// Dependency-free consumer model classes:
var model = new EnterpriseBIPlatform();
model.Cube.Add(new Cube { Id = "1", CubeName = "Sales" });

// Optional tooling helpers (generated with --tooling):
public static async Task LoadWorkspaceAsync()
{
    var workspace = await EnterpriseBIPlatformTooling.LoadWorkspaceAsync(@".\Samples\CommandExamples");
}
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

Or use the executable in repo root:

```powershell
.\meta.exe help
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
meta model suggest --workspace .\Samples\CommandExamples
meta model suggest --print-commands --workspace .\Samples\CommandExamples
meta model suggest --show-keys --explain --workspace .\Samples\CommandExamples
meta model suggest --show-blocked --explain --workspace .\Samples\CommandExamples
```

Model edits:

```powershell
meta model add-entity SourceSystem --workspace .\Samples\CommandExamples
meta model add-property SourceSystem Name --required true --default-value Unknown --workspace .\Samples\CommandExamples
meta model add-relationship System SourceSystem --default-id 1 --workspace .\Samples\CommandExamples
meta model refactor property-to-relationship --source Order.WarehouseCode --target Warehouse --lookup WarehouseCode --drop-source-property --workspace .\Samples\SuggestDemo\Workspace
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
meta import csv .\Samples\landing.csv --entity Landing --new-workspace .\Samples\ImportedCsv

meta generate sql --out .\out\sql --workspace .\Samples\CommandExamples
meta generate csharp --out .\out\csharp --workspace .\Samples\CommandExamples
meta generate csharp --out .\out\csharp --tooling --workspace .\Samples\CommandExamples
meta generate ssdt --out .\out\ssdt --workspace .\Samples\CommandExamples
```

### Full example: CSV import -> suggest -> refactor

This is the intended landing workflow: import flat CSVs, run suggest, then apply an atomic model+instance refactor.

```cmd
cd /d C:\Users\jimmy\Desktop\Metadata
rmdir /s /q C:\Users\jimmy\Desktop\Metadata\Samples\SuggestDemo\Workspace

meta import csv C:\Users\jimmy\Desktop\Metadata\Samples\SuggestDemo\demo-csv\products.csv --entity Product --new-workspace C:\Users\jimmy\Desktop\Metadata\Samples\SuggestDemo\Workspace
cd /d C:\Users\jimmy\Desktop\Metadata\Samples\SuggestDemo\Workspace
meta import csv ..\demo-csv\suppliers.csv --entity Supplier
meta import csv ..\demo-csv\categories.csv --entity Category
meta import csv ..\demo-csv\warehouses.csv --entity Warehouse
meta import csv ..\demo-csv\orders.csv --entity Order

meta model suggest
meta model suggest --print-commands

meta model refactor property-to-relationship --source Order.WarehouseCode --target Warehouse --lookup WarehouseCode --drop-source-property

meta model suggest
meta check
```

Model change example (`metadata/model.xml`) for `Order`:

Before (flat property):

```xml
<Entity name="Order" plural="Orders">
  <Properties>
    <Property name="OrderNumber" />
    <Property name="ProductCode" />
    <Property name="SupplierCode" />
    <Property name="WarehouseCode" />
    <Property name="StatusText" />
  </Properties>
</Entity>
```

After `property-to-relationship --source Order.WarehouseCode --target Warehouse --lookup WarehouseCode --drop-source-property`:

```xml
<Entity name="Order" plural="Orders">
  <Properties>
    <Property name="OrderNumber" />
    <Property name="ProductCode" />
    <Property name="SupplierCode" />
    <Property name="StatusText" />
  </Properties>
  <Relationships>
    <Relationship entity="Warehouse" />
  </Relationships>
</Entity>
```

This promotion rewrites each `Order` row from scalar `WarehouseCode` to required relationship usage `WarehouseId`, then removes the source scalar property when `--drop-source-property` is used.

Instance diff and merge:

```powershell
meta instance diff .\LeftWs .\RightWs
meta instance merge .\TargetWs .\RightWs.instance-diff
```

## MetaSchema

MetaSchema is the separate schema/canonical-catalog toolchain.

It handles schema extraction and sanctioned catalogs that `meta` can treat as metadata workspaces.

Sanctioned model references are kept as XML on disk and loaded by core runtime code:
- `Meta.Core/WorkspaceConfig/Models/MetaWorkspace.model.xml`
- `MetaSchema.Core/Models/SchemaCatalog.model.xml`
- `MetaSchema.Catalogs/TypeConversionCatalog/metadata/model.xml`

To regenerate sanctioned model C# APIs through the same public CLI surface, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Generate-SanctionedModelApis.ps1
```

This script uses `meta generate csharp --tooling` for each sanctioned model.

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
