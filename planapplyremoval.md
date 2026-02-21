# TASK — Remove plan/apply from MetadataStudio CLI (execute-only mental model)

## Goal

Remove the entire **plan/apply** concept from the product surface so the CLI matches the BI-native workflow:

Run command → it executes immediately → success or failure.
Rollback is handled externally (git / workspace state), not via staged plans.

## Decision

* **Delete**: `--plan`, `--apply`, `--out-plan`, plan JSON artifacts, plan fingerprint checks, and the `meta apply <planfile>` command.
* Do **not** “hide” plan/apply. Do **not** keep it as an “advanced mode”. It should not exist as a concept in user-facing CLI or docs.
* Keep the rest of the CLI behavior and outputs as-is unless required by the removal.

## Scope

* CLI argument parsing and help/usage strings.
* Any code paths that generate, serialize, read, validate, or apply plan files.
* Any error codes/messages whose only purpose is plan/apply (e.g., fingerprint mismatch).
* Documentation + examples:

  * `COMMANDS.md`
  * `COMMANDS-EXAMPLES.md`

## Non-goals (explicit)

* Do not redesign other UX aspects (tone, blockers, row addressing, relationship messaging) in this task.
* Do not change `--json` behavior or documentation in this task.
* Do not add new safety workflows to “replace” plan/apply (no interactive confirmations, no new rollback mechanism, no snapshots).

## Current references that must be removed (audit targets)

### `COMMANDS.md`

* Lifecycle line: `inspect -> plan -> apply -> validate -> generate`
* Rules mentioning `--plan` preview and “Plans can be written and applied later.”
* Global flags list containing `--plan`, `--apply`, `--out-plan`
* “Plan apply” section: `meta apply <planfile>`
* “Plan file contract” section (including fingerprint + `--force` mismatch override)

### `COMMANDS-EXAMPLES.md`

* `normalize ... --plan` examples and their outputs
* Error “Hint: usage ... [--out-plan <path>] [--plan|--apply]” in:

  * `ensure` error example
  * `upsert` error example
* `apply <planfile>` example including `E_FINGERPRINT_MISMATCH` and `--force` override guidance

## Work breakdown (do in order)

### 1) Remove flags and command surfaces

* Remove CLI flags: `--plan`, `--apply`, `--out-plan`
* Remove CLI command: `apply` (i.e., `meta apply <planfile>`)
* Remove any parsing, validation, or dispatch paths attached to these.

### 2) Remove plan artifact system

Delete/strip code for:

* Plan generation
* Plan JSON schema/serialization
* Plan storage paths / touchedFiles / operations lists tied to plan files
* Workspace fingerprinting that only exists for plan apply
* `--force` override for plan apply (remove only if it’s exclusively used here)

### 3) Remove plan-only error codes/messages

* Remove `E_FINGERPRINT_MISMATCH` (and any other plan-only diagnostics)
* Ensure errors no longer suggest plan/apply actions

### 4) Update docs and examples

* `COMMANDS.md`: remove plan/apply mentions and sections entirely
* `COMMANDS-EXAMPLES.md`:

  * replace `normalize --plan` runs with execute-only runs (no `--plan`)
  * remove `apply <planfile>` example section
  * remove `--plan|--apply|--out-plan` from any “Hint: usage” lines

### 5) Final “grep gate” (acceptance)

* Repo search for these strings should return **zero** matches (unless they exist in commit history only):

  * `--plan`
  * `--apply`
  * `--out-plan`
  * `meta apply`
  * `E_FINGERPRINT_MISMATCH`
  * `fingerprintBefore` / `planWorkspaceFingerprint` (if plan-only)

(If any of these remain, annotate why they remain and confirm they are not user-facing concepts.)

## Acceptance criteria

* Running any mutating command executes immediately by default (no staged plan path).
* `meta --help` / command usage output contains **no** plan/apply/out-plan references.
* No command or error message suggests “generate a fresh plan then apply”.
* `COMMANDS.md` and `COMMANDS-EXAMPLES.md` contain **no** plan/apply references.

## Manual verification (minimal)

* Run a couple of representative mutators (see examples file): ensure they execute and return existing success/failure behavior without mentioning plan/apply.
* Confirm help/usage for `ensure`, `upsert`, `normalize` does not list removed switches.

## Implementation notes for Codex

* Make changes as a single coherent removal, not partial deprecation.
* Keep diffs minimal outside the plan/apply removal.
* If removal touches shared code, prefer deletion over leaving dead branches.
* If `--force` is used elsewhere, keep it; otherwise remove it with the plan/apply removal.

## Deliverables

* Updated codebase with plan/apply removed.
* Updated `COMMANDS.md` and `COMMANDS-EXAMPLES.md` reflecting execute-only workflow.
* Short summary of what was removed and any follow-on cleanup suggestions (separate from this task’s scope).
