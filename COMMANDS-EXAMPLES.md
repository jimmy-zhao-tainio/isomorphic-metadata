# Meta CLI Real Command Examples

All examples below were executed against local workspaces in this repository. Each section includes one successful run and one failing run with captured output and exit code.

## help

Success:
```powershell
> meta help
[exit 0]
Workspace is discovered from current directory; use --workspace to override.

Usage:
  meta <command> [options]

Global options:

  --workspace <path>  Override workspace root.
  --strict            Treat warnings as errors for mutating commands.

Workspace:

  init    Initialize workspace.
  status  Show workspace summary.

Model:

  check  Check model and instance integrity.
  graph  Graph stats and inbound relationships.
  list   List entities, properties, and relationships.
  model  Mutate model entities, properties, and relationships.
  view   View entity or instance details.

Instance:

  instance     Diff and merge instance artifacts.
  insert       Insert one instance: <Entity> <Id> or --auto-id.
  delete       Delete one instance: <Entity> <Id>.
  query        Search instances with equals/contains filters.
  bulk-insert  Insert many instances from tsv/csv input (supports --auto-id).

Pipeline:

  import    Import into a NEW workspace.
  generate  Generate artifacts from the workspace.

Examples:

  meta status
  meta model add-entity SourceSystem
  meta insert Cube 10 --set "CubeName=Ops Cube"

Next: meta <command> help
```

Failure:
```powershell
> meta help unknown-topic
[exit 1]
Error: unknown help topic 'unknown-topic'.

Usage: meta help [<command> ...]

Next: meta help
```

## command help

Success:
```powershell
> meta model --help
[exit 0]
Edit model entities, properties, and relationships.

Usage:
  meta model <subcommand> [arguments] [options]

Options:

  --workspace <path>  Workspace root override.

Subcommands:

  add-entity         Create an entity.
  rename-entity      Rename an entity.
  drop-entity        Remove an entity (must be empty).
  add-property       Add a property to an entity.
  rename-property    Rename a property.
  drop-property      Remove a property.
  add-relationship   Add a relationship.
  drop-relationship  Remove a relationship.

Examples:

  meta model add-entity SalesCube
  meta model rename-entity OldName NewName
  meta model add-property Cube Purpose --required true --default-value Unknown

Next: meta model <subcommand> help
```

Failure:
```powershell
> meta model add-entity
[exit 1]
Error: missing required argument <Name>.

Usage: meta model add-entity <Name> [--workspace <path>]

Next: meta model add-entity help
```

## init

Success:
```powershell
> meta init Samples\CommandExamplesInit
[exit 0]
OK: workspace initialized
Path: <repo>\Samples\CommandExamplesInit
```

Failure:
```powershell
> meta init "Samples\Bad|Path"
[exit 4]
Error: Path is invalid for Windows.

Illegal character: '|'.

Next: use a valid Windows path and retry.
```

## status

Success:
```powershell
> meta status --workspace Samples\CommandExamples
[exit 0]
Status: ok
Workspace:
  Path: <repo>\Samples\CommandExamples
  Metadata: <repo>\Samples\CommandExamples\metadata
Model:
  Name: EnterpriseBIPlatform
  Entities: 9
  Rows: 15
Data:
  Model: 2.38 KB (2438 B)
  Instance: 3.2 KB (3281 B)
Contract:
  Version: 1.0
```

Failure:
```powershell
> meta status --workspace Samples\CommandExamplesBroken
[exit 4]
Error: Cannot parse metadata/model.xml.

Location: line 1, position 58.

Next: meta check
```

## instance diff

Success:
```powershell
> meta instance diff Samples\CommandExamplesDiffLeft Samples\CommandExamplesDiffRight
[exit 1]
Instance diff: differences found.
DiffWorkspace: <repo>\Samples\CommandExamplesDiffRight.instance-diff
Rows: left=15, right=16  Properties: left=46, right=49
NotIn: left-not-in-right=0, right-not-in-left=0
```

Failure:
```powershell
> meta instance diff Samples\CommandExamplesDiffLeft Samples\MissingWorkspace
[exit 4]
Error: Workspace was not found.

Next: meta init .
```

## list entities

Success:
```powershell
> meta list entities --workspace Samples\CommandExamples
[exit 0]
Entities (9):
  Name             Rows  Properties  Relationships
  Cube             2     3           0
  Dimension        2     3           0
  Fact             1     4           0
  Measure          1     2           1
  System           2     3           1
  SystemCube       2     1           2
  SystemDimension  2     1           2
  SystemFact       1     1           2
  SystemType       2     2           0
```

Failure:
```powershell
> meta list entities --workspace Samples\CommandExamplesBroken
[exit 4]
Error: Cannot parse metadata/model.xml.

Location: line 1, position 58.

Next: meta check
```

## list properties

Success:
```powershell
> meta list properties Cube --workspace Samples\CommandExamples
[exit 0]
Properties: Cube
  Name         Type    Required
  Id           string  yes
  CubeName     string  yes
  Purpose      string  no
  RefreshMode  string  no
```

Failure:
```powershell
> meta list properties MissingEntity --workspace Samples\CommandExamples
[exit 4]
Error: Entity 'MissingEntity' was not found.

Next: meta list entities
```

## list relationships

Success:
```powershell
> meta list relationships Measure --workspace Samples\CommandExamples
[exit 0]
Relationships: Measure (1)
Required: (n/a)
  Name  Target  Column
  Cube  Cube    CubeId
```

Failure:
```powershell
> meta list relationships MissingEntity --workspace Samples\CommandExamples
[exit 4]
Error: Entity 'MissingEntity' was not found.

Next: meta list entities
```

## check

Success:
```powershell
> meta check --workspace Samples\CommandExamples
[exit 0]
OK: check (0 errors, 0 warnings)
```

Failure:
```powershell
> meta check --workspace Samples\CommandExamplesBroken
[exit 4]
Error: Cannot parse metadata/model.xml.

Location: line 1, position 58.

Next: meta check
```

## view entity

Success:
```powershell
> meta view entity Cube --workspace Samples\CommandExamples
[exit 0]
Entity: Cube
Rows: 2
Properties:
  Name         Type    Required
  Id           string  required
  CubeName     string  required
  Purpose      string  optional
  RefreshMode  string  optional
Relationships: 0
RelationshipTargets:
  (none)
```

Failure:
```powershell
> meta view entity MissingEntity --workspace Samples\CommandExamples
[exit 4]
Error: Entity 'MissingEntity' was not found.

Next: meta list entities
```

## view instance

Success:
```powershell
> meta view instance Cube 1 --workspace Samples\CommandExamples
[exit 0]
Instance: Cube 1
  Field        Value
  CubeName     Sales Performance
  Purpose      Monthly revenue and margin tracking.
  RefreshMode  Scheduled
```

Failure:
```powershell
> meta view instance Cube 999 --workspace Samples\CommandExamples
[exit 4]
Error: Instance 'Cube 999' was not found.

Next: meta query Cube --contains Id 999
```

## query

Success:
```powershell
> meta query Cube --workspace Samples\CommandExamples --contains CubeName Sales
[exit 0]
Query: Cube
Filter: CubeName contains Sales
Matches: 1
  Id  CubeName           Purpose
  1   Sales Performance  Monthly revenue and margin tracking.
```

Failure:
```powershell
> meta query Cube --workspace Samples\CommandExamples --contains MissingField Value
[exit 4]
Error: Property 'Cube.MissingField' was not found.

Next: meta list properties Cube
```

## graph stats

Success:
```powershell
> meta graph stats --workspace Samples\CommandExamples --top 3 --cycles 3
[exit 0]
Graph: EnterpriseBIPlatform
Nodes: 9
Edges: declared=8 unique=8 dup=0 missingTarget=0
Components: 1  Roots: 4  Sinks: 4  Isolated: 0
Cycles: no  MaxDepth: 2
AvgDegree: in=0.889 out=0.889
Top out-degree (3):
  Entity           OutDegree
  SystemCube       2
  SystemDimension  2
  SystemFact       2
Top in-degree (3):
  Entity     InDegree
  System     3
  Cube       2
  Dimension  1
```

Failure:
```powershell
> meta graph stats --workspace Samples\CommandExamplesBroken --top 3 --cycles 3
[exit 4]
Error: Cannot parse metadata/model.xml.

Location: line 1, position 58.

Next: meta check
```

## graph inbound

Success:
```powershell
> meta graph inbound Cube --workspace Samples\CommandExamples --top 10
[exit 0]
Inbound relationships: Cube (2)
  FromEntity  ToEntity
  Measure     Cube
  SystemCube  Cube
```

Failure:
```powershell
> meta graph inbound MissingEntity --workspace Samples\CommandExamples
[exit 4]
Error: Entity 'MissingEntity' was not found.

Next: meta list entities
```

## model add-entity

Success:
```powershell
> meta model add-entity CmdEntity --workspace Samples\CommandExamples
[exit 0]
OK: entity created
Entity: CmdEntity
```

Failure:
```powershell
> meta model add-entity Cube --workspace Samples\CommandExamples
[exit 4]
Error: Entity 'Cube' already exists.

Next: meta list entities
```

## model rename-entity

Success:
```powershell
> meta model rename-entity CmdEntity CmdEntityRenamed --workspace Samples\CommandExamples
[exit 0]
OK: entity renamed
From: CmdEntity
To: CmdEntityRenamed
```

Failure:
```powershell
> meta model rename-entity MissingEntity Anything --workspace Samples\CommandExamples
[exit 4]
Error: Entity 'MissingEntity' was not found.

Next: meta list entities
```

## model add-property

Success:
```powershell
> meta model add-property CmdEntityRenamed Label --required true --default-value Unknown --workspace Samples\CommandExamples
[exit 0]
OK: property added
Entity: CmdEntityRenamed
Property: Label (required)
DefaultValue: Unknown
```

Failure:
```powershell
> meta model add-property MissingEntity Label --workspace Samples\CommandExamples
[exit 4]
Error: Entity 'MissingEntity' was not found.

Next: meta list entities
```

## model rename-property

Success:
```powershell
> meta model rename-property CmdEntityRenamed Label LabelText --workspace Samples\CommandExamples
[exit 0]
OK: property renamed
Entity: CmdEntityRenamed
From: Label
To: LabelText
```

Failure:
```powershell
> meta model rename-property CmdEntityRenamed MissingProp Anything --workspace Samples\CommandExamples
[exit 4]
Error: Property 'CmdEntityRenamed.MissingProp' was not found.

Next: meta list properties CmdEntityRenamed
```

## model add-relationship

Success:
```powershell
> meta model add-relationship CmdEntityRenamed Cube --default-id 1 --workspace Samples\CommandExamples
[exit 0]
OK: relationship added
From: CmdEntityRenamed
To: Cube
Name: CubeId
DefaultId: 1
```

Failure:
```powershell
> meta model add-relationship CmdEntityRenamed MissingTarget --default-id 1 --workspace Samples\CommandExamples
[exit 4]
Error: Entity 'MissingTarget' was not found.

Next: meta list entities
```

## model drop-relationship

Success:
```powershell
> meta model drop-relationship CmdEntityRenamed Cube --workspace Samples\CommandExamples
[exit 0]
OK: relationship removed
From: CmdEntityRenamed
To: Cube
Name: Cube
```

Failure:
```powershell
> meta model drop-relationship Measure Cube --workspace Samples\CommandExamples
[exit 4]
Error: Relationship 'Measure->Cube' is in use.

Relationship usage exists in 1 instance(s).

Relationship usage blockers:
  Entity   Instance
  Measure  Measure 1

Next: meta instance relationship set Measure 1 --to Cube <ToId>
```

## model drop-property

Success:
```powershell
> meta model drop-property CmdEntityRenamed LabelText --workspace Samples\CommandExamples
[exit 0]
OK: property removed
Entity: CmdEntityRenamed
Property: LabelText
```

Failure:
```powershell
> meta model drop-property CmdEntityRenamed MissingProp --workspace Samples\CommandExamples
[exit 4]
Error: Property 'CmdEntityRenamed.MissingProp' was not found.

Next: meta list properties CmdEntityRenamed
```

## model drop-entity

Success:
```powershell
> meta model drop-entity CmdEntityRenamed --workspace Samples\CommandExamples
[exit 0]
OK: entity removed
Entity: CmdEntityRenamed
```

Failure:
```powershell
> meta model drop-entity Cube --workspace Samples\CommandExamples
[exit 4]
Error: Cannot drop entity Cube

Cube has 2 instances.

Next: meta view instance Cube 1
```

## insert

Success:
```powershell
> meta insert Cube 10 --set "CubeName=Ops Cube" --set "Purpose=Operational reporting" --set RefreshMode=Scheduled --workspace Samples\CommandExamples
[exit 0]
OK: created Cube 10
CubeName: Ops Cube
```

Failure:
```powershell
> meta insert Cube 10 --set CubeName=Duplicate --workspace Samples\CommandExamples
[exit 4]
Error: Instance 'Cube 10' already exists.

Next: meta instance update Cube 10 --set <Field>=<Value>
```

## insert auto-id

Success:
```powershell
> meta insert Cube --auto-id --set "CubeName=Auto Id Cube" --set "Purpose=Autogenerated id sample" --set RefreshMode=Manual --workspace Samples\CommandExamples
[exit 0]
OK: created Cube 11
CubeName: Auto Id Cube
```

Failure:
```powershell
> meta insert Cube 11 --auto-id --set CubeName=Conflict --workspace Samples\CommandExamples
[exit 1]
Error: --auto-id cannot be combined with positional <Id>.

Usage: meta insert <Entity> [<Id>|--auto-id] --set Field=Value [--set Field=Value ...] [--workspace <path>]

Next: meta insert help
```

## instance update

Success:
```powershell
> meta instance update Cube 10 --set RefreshMode=Manual --workspace Samples\CommandExamples
[exit 0]
OK: updated Cube 10
```

Failure:
```powershell
> meta instance update Cube 1 --set MissingField=BadValue --workspace Samples\CommandExamples
[exit 4]
Error: Property 'Cube.MissingField' was not found.

Next: meta list properties Cube
```

## instance relationship set

Success:
```powershell
> meta instance relationship set Measure 1 --to Cube 2 --workspace Samples\CommandExamples
[exit 0]
OK: relationship usage updated
FromInstance: Measure 1
ToInstance: Cube 2
```

Failure:
```powershell
> meta instance relationship set Measure 1 --to Cube 999 --workspace Samples\CommandExamples
[exit 4]
Error: Instance 'Cube 999' was not found.

Next: meta query Cube --contains Id 999
```

## instance relationship list

Success:
```powershell
> meta instance relationship list Measure 1 --workspace Samples\CommandExamples
[exit 0]
Relationships:
  FromInstance: Measure 1
  Relationship  ToEntity  ToInstance
  Cube          Cube      Cube 2
```

Failure:
```powershell
> meta instance relationship list Measure 999 --workspace Samples\CommandExamples
[exit 4]
Error: Instance 'Measure 999' was not found.

Next: meta query Measure --contains Id 999
```

## bulk-insert

Success:
```powershell
> meta bulk-insert Cube --from tsv --file Samples\CommandExamples\input\cube-bulk-insert.tsv --key Id --workspace Samples\CommandExamples
[exit 0]
OK: bulk insert Cube
Inserted: 1
Updated: 1
Total: 2
```

Failure:
```powershell
> meta bulk-insert Cube --from tsv --file Samples\CommandExamples\input\cube-bulk-insert-invalid.tsv --key Id --workspace Samples\CommandExamples
[exit 4]
Error: Property 'Cube.UnknownColumn' was not found.

Next: meta list properties Cube
```

## bulk-insert auto-id

Success:
```powershell
> meta bulk-insert Cube --from tsv --file Samples\CommandExamples\input\cube-bulk-insert-auto-id.tsv --auto-id --workspace Samples\CommandExamples
[exit 0]
OK: bulk insert Cube
Inserted: 2
Updated: 0
Total: 2
```

Failure:
```powershell
> meta bulk-insert Cube --from tsv --file Samples\CommandExamples\input\cube-bulk-insert-auto-id.tsv --auto-id --key Id --workspace Samples\CommandExamples
[exit 1]
Error: --auto-id cannot be combined with --key.

Usage: meta bulk-insert <Entity> [--from tsv|csv] [--file <path>|--stdin] [--key Field[,Field2...]] [--auto-id] [--workspace <path>]

Next: meta bulk-insert help
```

## delete

Success:
```powershell
> meta delete Cube 10 --workspace Samples\CommandExamples
[exit 0]
OK: deleted Cube 10
```

Failure:
```powershell
> meta delete Cube 2 --workspace Samples\CommandExamples
[exit 4]
Error: Cannot delete Cube 2

Blocked by existing relationships (2).
Measure 1 references Cube 2
SystemCube 2 references Cube 2

Next: meta delete help
```

## generate sql

Success:
```powershell
> meta generate sql --out Samples\CommandExamplesOut\sql --workspace Samples\CommandExamples
[exit 0]
OK: generated sql
Out: <repo>\Samples\CommandExamplesOut\sql
Files: 2
```

Failure:
```powershell
> meta generate sql --out Samples\CommandExamplesOut\sql-broken --workspace Samples\CommandExamplesBroken
[exit 4]
Error: Cannot parse metadata/model.xml.

Location: line 1, position 58.

Next: meta check
```

## generate csharp

Success:
```powershell
> meta generate csharp --out Samples\CommandExamplesOut\csharp --workspace Samples\CommandExamples
[exit 0]
OK: generated csharp
Out: <repo>\Samples\CommandExamplesOut\csharp
Files: 10
```

Failure:
```powershell
> meta generate csharp --out Samples\CommandExamplesOut\csharp-broken --workspace Samples\CommandExamplesBroken
[exit 4]
Error: Cannot parse metadata/model.xml.

Location: line 1, position 58.

Next: meta check
```

## generate ssdt

Success:
```powershell
> meta generate ssdt --out Samples\CommandExamplesOut\ssdt --workspace Samples\CommandExamples
[exit 0]
OK: generated ssdt
Out: <repo>\Samples\CommandExamplesOut\ssdt
Files: 4
```

Failure:
```powershell
> meta generate ssdt --out Samples\CommandExamplesOut\ssdt-broken --workspace Samples\CommandExamplesBroken
[exit 4]
Error: Cannot parse metadata/model.xml.

Location: line 1, position 58.

Next: meta check
```

## import xml

Success:
```powershell
> meta import xml Samples\SampleModel.xml Samples\SampleInstance.xml --new-workspace Samples\CommandExamplesImportedXml
[exit 0]
OK: imported xml
Workspace: <repo>\Samples\CommandExamplesImportedXml
```

Failure:
```powershell
> meta import xml Samples\SampleModel.xml Samples\SampleInstance.xml --new-workspace Samples\CommandExamples
[exit 4]
Error: new workspace target directory must be empty.

Directory contains entries such as: input, metadata

Next: choose a new folder path, for example: --new-workspace .\ImportedWorkspace2
```

## instance merge

Success:
```powershell
> meta instance merge Samples\CommandExamplesDiffLeft <repo>\Samples\CommandExamplesDiffRight.instance-diff
[exit 0]
OK: instance merge applied
Target: <repo>\Samples\CommandExamplesDiffLeft
```

Failure:
```powershell
> meta instance merge Samples\CommandExamplesDiffLeft <repo>\Samples\CommandExamplesDiffRight.instance-diff
[exit 1]
Error: instance merge precondition failed: target does not match the diff left snapshot.

Next: re-run meta instance diff on the current target and intended right workspace.
```




