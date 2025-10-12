# Isomorphic Metadata

> Round-trip metadata definitions across XML, C# POCOs, and SQL Server while keeping database instances in sync.

This repo shows how to treat metadata and the data it governs as *isomorphic* representations. It lets you move seamlessly between a generic metadata model, generated typed code, SQL schema/data, and a live SQL Server database.

## Highlights

- **Generic metadata model:** Plain classes (`Model`, `Entity`, `Property`, `ModelInstance`, etc.) with XML readers/writers.
- **SQL Server integration:** Schema generator, data generator, and readers that rebuild metadata from an existing database.
- **Generated code:** `Transforms.Console` emits strongly typed POCOs from XML metadata.
- **Reflection materializer:** Hydrates generated classes from a `ModelInstance` without regenerating materializers per schema.
- **Sync console:** One command to pull a SQL database into XML, regenerate code/scripts, and (optionally) compare with the prior metadata snapshot.
- **Extensibility hooks:** Partial classes and extension files make it easy to add domain helpers without touching generated code.

## Projects

| Project | Description |
| --- | --- |
| `Generic` | Core metadata types, XML reader (`Reader`), SQL reader (`ReadFromDatabase`), reflection materializer, model comparer, database instance reader. |
| `Transforms` | Code generators: C# converter, SQL Server schema/data emitters, XML writers. |
| `Transforms.Console` | CLI that reads XML and regenerates C#/SQL artifacts. |
| `Samples` | Generated POCO sources compiled into the `SampleModel` assembly for reuse. |
| `Samples.Console` | Harness that loads `SampleModel.xml` and `SampleInstance.xml`, materializes the generated classes, and prints them. |
| `Sync.Console` | Orchestrates the SQL → metadata → XML/code round trip and prints schema diffs. |

## Sample workflow

1. **Build everything**

   ```
   dotnet build Metadata.Framework.sln
   ```

2. **Generate classes/schema/data from XML**

   ```
   dotnet run --project Transforms.Console/Metadata.Transformations.Console.csproj
   ```

   Produces `Samples/SampleModel.cs`, `Samples/SampleModel.sql`, and `Samples/SampleInstance.sql`.

3. **Apply to SQL Server**

   ```
   sqlcmd -S localhost -i Samples/SampleModel.sql
   sqlcmd -S localhost -d EnterpriseBIPlatform -i Samples/SampleInstance.sql
   ```

4. **Round-trip back from SQL**

   ```
   dotnet run --project Sync.Console/Sync.Console.csproj
   ```

   - Reads schema/data from SQL.
   - Compares to existing XML.
   - Writes updated `SampleModel.xml` and `SampleInstance.xml`.
   - Regenerates `SampleModel.cs`, `SampleModel.sql`, and `SampleInstance.sql`.

5. **Inspect the typed model**

   ```
   dotnet run --project Samples.Console/Samples.Console.csproj
   ```

   Uses the reflection materializer to print the hydrated POCO graph.

## Reflection materializer

`ReflectionModelMaterializer.Materialize<T>` constructs the generated classes from a `ModelInstance`, reusing the same object for each record and wiring relationships via foreign keys. That means you can hydrate the model without generating a new materializer each time.

## Schema & data analysis

- `SqlServerSchemaGenerator` walks the generic model and emits deterministic DDL.
- `SqlServerDataGenerator` topologically sorts entities so foreign keys are populated safely.
- `ModelComparer` surfaces differences between two metadata definitions (added/removed/changed entities, properties, relationships).

## Custom logic

Keep custom domain helpers outside the generated file. For example, `Samples/SampleModelExtensions.cs` adds `SystemsWithCube()` without touching the regenerated POCOs.

## Requirements

- .NET SDK (net48 target).
- SQL Server (Developer or Express/LocalDB). Update connection strings in `Sync.Console` or pass them as args.
