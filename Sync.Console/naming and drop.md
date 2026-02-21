Codex â€” terminology discipline + add missing relationship/destructive commands.

Vocabulary rule (hard):
The system vocabulary is strictly limited to:
entity, row, property, relationship, relationship usage (or relationship participation).

The CLI must NOT introduce any new concepts/terms such as:
reference, link, role, pointer, foreign key, join, connect, bind.

No abbreviations in command names or output text.

No changes to core storage formats.
No cascade behavior in primitives.

â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
A) Terminology correction pass
â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

1. Rename any command/help/error text that uses:
   ref / reference, link, bind, connect
   to relationship terminology.

2. Presenter wording:
   Always say â€œrelationshipâ€, â€œrelationship usageâ€, â€œrelationship participationâ€.
   Never say â€œreferenceâ€ or â€œlinkâ€.

3. Documentation and examples:
   Update COMMANDS.md and regenerate COMMANDS-EXAMPLES.md from real runs.

â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
B) Add strict destructive model primitives (fail hard)
â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

4. meta model drop-relationship <FromEntity> <ToEntity>


Meaning:
Remove the relationship definition FromEntity -> ToEntity.

Rules:

* MUST fail if any instance rows currently use/participate in this relationship.
* No cascade. Do not remove relationship usage automatically.

Failure output must include:

* Error[E_RELATIONSHIP_IN_USE]
* Where: fromEntity=..., toEntity=..., occurrences=<n>
* A deterministic table listing up to 20 offending rows, for example:
  Entity | Row
  Cube   | Cube 10
  Cube   | Cube 11
* Hints (max 3) that are actionable and specific, for example:
  Hint: list relationship usage: meta query <FromEntity> --top 20
  Hint: remove relationship usage on those rows, then retry.
  Hint: validate after changes: meta validate

Success output:
OK: relationship removed
From: <FromEntity>
To: <ToEntity>

â€”

5. meta model drop-entity <Entity>


Meaning:
Remove the entity definition.

Rules:

* MUST fail if the entity has any rows.
* MUST fail if any inbound relationships exist (any entity has a relationship targeting this entity).
* No cascade. Do not delete rows or relationships automatically.

Failure output must include:

* Error[E_ENTITY_NOT_EMPTY] if rows > 0
  Where: entity=..., rows=<n>
  Hint: delete rows first: meta row delete <Entity> <Id>
  Hint: inspect rows: meta query <Entity> --top 20

* Error[E_ENTITY_HAS_INBOUND_RELATIONSHIPS] if inbound relationships exist
  Where: entity=..., inboundRelationships=<n>
  Table: FromEntity | ToEntity
  Hint: remove inbound relationships first, then retry.
  Hint: inspect graph inbound: meta graph inbound <Entity>   (add this if missing; see section D)

Success output:
OK: entity removed
Entity: <Entity>

â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
C) Add instance-level relationship participation commands
â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

These commands manipulate instance data only (rows and their relationship usage).
They do not change the model.

6. meta row relationship set <FromEntity> <FromId>
   --to <ToEntity> <ToId>


Meaning:
Ensure the selected row participates in the relationship FromEntity -> ToEntity by targeting the specified ToEntity row.

Rules:

* Validate relationship exists in the model.
* Validate target row exists.
* Deterministic behavior if relationship usage already exists: update/replace to match the target.

Success output:
OK: relationship usage updated
FromRow: <FromEntity> <FromId>
ToRow: <ToEntity> <ToId>

Failure must include clear hints:

* If relationship does not exist: suggest meta list relationships <FromEntity> and meta model add-relationship <FromEntity> <ToEntity>
* If target row does not exist: suggest meta query <ToEntity> ... and meta new <ToEntity> ...

â€”

7. meta row relationship clear <FromEntity> <FromId>
   --to-entity <ToEntity>


Meaning:
Remove relationship usage FromEntity -> ToEntity from the selected row.

Rules:

* Fail if relationship usage not present? Prefer idempotent behavior:
  If not present, report OK with â€œno changesâ€ (deterministic).

Success output:
OK: relationship usage removed
FromRow: ...
ToEntity: ...

â€”

8. meta row relationship list <FromEntity> <FromId>

Meaning:
List relationship usage for a row.

Output table:
ToEntity | ToRowId (or ToRow address)

Example:
Relationships for Cube 1
ToEntity   ToRow
System     System 2

â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
D) Graph support for destructive command hints
â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

9. meta graph inbound <Entity> [--top <n>]

Meaning:
List inbound relationships targeting <Entity>.

Output table:
FromEntity | ToEntity

This command is used by drop-entity failure hints.

â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
E) Output + hint rules (continue current standard)
â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

* Keep canonical error shape:
  Error[E_*]: message
  Where: key=value,...
  Hint: ... (max 3)
* No generic fallback hints like â€œrun: meta helpâ€.
* Hints must be specific next steps and preferably include concrete commands.
* Use presenter tables for blocker lists and relationship usage listings.
* JSON mode unchanged.

â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
F) Verification
â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

* Update COMMANDS.md
* Regenerate COMMANDS-EXAMPLES.md from real runs including success+failure for new commands.
* Add tests for:

  * drop-relationship blocked by relationship usage
  * drop-entity blocked by rows
  * drop-entity blocked by inbound relationships
  * row relationship set/clear/list round-trip correctness
  * graph inbound output determinism

All tests must pass.
