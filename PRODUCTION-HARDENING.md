# Production Hardening Plan

Status is tracked for the core `MetadataStudio.*` toolchain.

## 1) Large-scale load tests with budgets

Status: implemented

- `MetadataStudio.Core.Tests/LargeWorkspacePerformanceTests.cs`
- Enforced scenario: save+load `100,000` rows with time/allocation budgets.
- Optional scenario: save+load `1,000,000` rows behind `METADATASTUDIO_ENABLE_1M_PERF_TEST=1`.
- Budgets are configurable via environment variables for CI tuning.

## 2) Determinism golden tests

Status: implemented

- Added byte-hash golden tests for:
  - `metadata/model.xml`
  - `metadata/instance/*.xml`
  - `metadata/workspace.json`
  - generated SQL and C# outputs
- Tests now re-run generation/write twice and assert identical bytes and fixed golden hashes.

## 3) Workspace-level atomic commit

Status: implemented

- Save now writes metadata into `metadata.__staging.<guid>` and swaps directory atomically.
- Existing metadata is moved to `metadata.__backup.<guid>` and removed after successful swap.
- On swap failure, previous metadata is restored.
- Staging/backup leftovers are cleaned up after save.

## 4) Single-writer lock

Status: implemented

- Save acquires workspace lock file `.meta.lock` with PID/machine/start-time metadata.
- Active lock causes fail-fast write rejection.
- Stale lock detection removes dead-PID locks and continues.

## 5) CI quality gates

Status: implemented

- `.github/workflows/ci.yml` now has dedicated jobs:
  - `correctness`
  - `determinism`
  - `perf_100k`
  - `perf_1m_optional` (workflow-dispatch opt-in)
- Any failed gate fails CI.
