# Mnemotron Super 3 Front

Status: `PENDING VERIFICATION`
Owner: `Mnemotron Super 3`
Mode: `inventory / audit / ledger only`

## Mission

Reduce workspace chaos and produce accurate maps that stronger agents can execute later.
No runtime coding. No architectural changes.

## Allowed zones

- repository root docs and ledgers
- `Docs/`
- `Assets/_Project/Prefabs`
- `Assets/_Project/Materials`
- `Assets/_Project/Data`
- `Assets/_Project/Scripts/Editor` (read-only for mapping)

## Strictly forbidden

- editing runtime `.cs` files in gameplay/world/bootstrap/ui/tool systems
- editing scenes, prefabs, materials, ScriptableObjects
- mass renames
- deleting files
- changing package/asmdef/project settings

## Batch plan (run in this exact order)

### Batch 1 - Workspace noise ledger

Goal:

- Build one map of duplicate/stale/unclear docs and report files that increase execution error risk.

Deliverable:

- `Docs/WORKSPACE_NOISE_LEDGER.md`

Required sections:

- `Authoritative`
- `Duplicate`
- `Stale`
- `Unclear Owner`
- `Junk Risk`

Definition of done:

- Every listed item has full path and one-line reason.
- No file deletions in this batch.

### Batch 2 - Naming contract audit ledger

Goal:

- Audit naming convention compliance against AGENTS contract.

Deliverable:

- `Docs/NAMING_CONTRACT_AUDIT_LEDGER.md`

Required checks:

- prefab prefixes: `PFB_`, `GEN_`
- material prefix: `MAT_`
- world family/profile naming: `ProceduralFamily_`, `ProceduralRule_`

Definition of done:

- Include exact violating path and expected prefix.
- Split candidates into `safe rename candidates` and `risky rename candidates`.
- No renames in this batch.

### Batch 3 - Validation coverage map

Goal:

- Map open work tails to existing validation menu coverage.

Inputs:

- `NEXT_SPRINT_TASKS.md`
- `BUILD_PLAYTEST_ISSUES.md`
- `Assets/_Project/Scripts/Editor` validation menu entries

Deliverable:

- `Docs/VALIDATION_COVERAGE_MAP.md`

Required table columns:

- `Open Task`
- `Has Validator`
- `Validator Path`
- `Needs Manual Check`
- `Notes`

Definition of done:

- Every mapped row is traceable to a concrete file/task reference.
- Missing validator spots are explicit and minimal.

## Submission format per batch

Use this exact compact structure:

```md
## Batch N - <name>
- What changed:
- Evidence:
- Risks:
- Remaining PENDING VERIFICATION:
```

## Stop conditions

Stop and escalate immediately if the task asks for:

- direct code fixes in runtime owners
- scene/prefab/material modifications
- broad file cleanup by deletion
- any ownership decision that is not explicit in existing docs
