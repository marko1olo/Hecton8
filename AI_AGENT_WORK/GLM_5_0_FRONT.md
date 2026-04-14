# GLM 5.0 Front

Status: `PENDING VERIFICATION`
Owner: `GLM 5.0`
Mode: `safe editor/data/test work only`

## Mission

Produce useful engineering artifacts without touching critical runtime ownership.
Primary value: stronger validation lanes, better editor tests, cleaner authoring diagnostics.

## Explicit allowed zones

- `Assets/_Project/Scripts/Editor`
- `Assets/_Project/Tests/Editor`
- `Docs/`
- root `.md` ledgers/reports

## Explicit blocked zones

- `Assets/_Project/Scripts/World` runtime ownership logic
- `Assets/_Project/Scripts/Bootstrap`
- `Assets/_Project/Scripts/UI` runtime shell controllers
- `Assets/_Project/Scripts/Gameplay` runtime systems
- `Assets/_Project/Scripts/Tools` runtime logic

Reading blocked zones is allowed only for mapping and validator coverage, not for runtime rewrites.

## Batch plan (run in this exact order)

### Batch 1 - Validation gap closure

Goal:

- Extend existing `Hecton/Validation/...` menu paths where open tasks currently have weak or no formal checks.

Deliverables:

- Small targeted updates in existing validator files in `Assets/_Project/Scripts/Editor`.
- One short report file: `Docs/VALIDATION_GAP_CLOSURE_REPORT.md`.

Definition of done:

- No new validator framework created.
- Existing validators still run and print deterministic PASS/COMPLETE style output.
- Report maps: `open tail -> validator path -> status`.

### Batch 2 - EditMode tests for pure contracts

Goal:

- Add deterministic EditMode tests for pure data/utility validation logic.

Deliverables:

- New tests under `Assets/_Project/Tests/Editor`.
- One short report file: `Docs/EDITMODE_TEST_COVERAGE_REPORT.md`.

Definition of done:

- Tests avoid scene dependence and playmode dependence.
- Tests can run headless and are deterministic.
- Report lists test names and what contract each test protects.

### Batch 3 - Editor reload hook map

Goal:

- Build a hard map of editor/reload hooks and classify cleanup safety.

Deliverables:

- `Docs/EDITOR_RELOAD_HOOK_MAP.md`.

Required classification buckets:

- `Protected`
- `Safe To Defer`
- `Safe To Disable In Editor`
- `Risky`

Definition of done:

- Each entry includes file path, owner class, hook type, and why it is in that bucket.
- No direct disabling/removal in this batch unless explicitly requested later.

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

Stop and escalate immediately if any task would require:

- public API change
- runtime owner rewrite
- scene/prefab mass edits
- package/asmdef/project setting changes
- uncertain architecture ownership
