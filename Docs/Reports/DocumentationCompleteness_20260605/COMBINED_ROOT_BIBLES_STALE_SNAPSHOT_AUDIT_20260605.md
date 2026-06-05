# Combined Root Bibles Stale Snapshot Audit - 2026-06-05

Status: `STATIC_DOC_AUDIT / REGENERATED_STATIC_PASS`.
Evidence class: `STATIC_DOC`.
Current front: root route-bible combined snapshot freshness.
First-20 route impact: prevents agents from reading stale concatenated doctrine that lacks the current first-route hooks.

This report does not prove implementation, Unity import, Play Mode, profiler, GC, player build, platform readiness, visual quality, or route acceptance.

## Mandates Followed

- `QA_Evidence_Text_Filter_Audit`

## Finding

`Docs/PROJECT_ROOT_BIBLES_COMBINED.md` was a generated concatenation snapshot and was stale after the 2026-06-05 route-bible hook wave. It has now been regenerated from an explicit source list.

Controller static scan:

- Selected live root source files in the regenerated combined snapshot index: `71`.
- Live selected root source `First-20 Route Hook` section count: `57`.
- Regenerated combined snapshot `First-20 Route Hook` section count: `57`.
- Generator check: `python -B Tools/Docs/BuildProjectRootBiblesCombined.py --check` passed.

## Regeneration

Laplace added:

- `Tools/Docs/BuildProjectRootBiblesCombined.py`
- regenerated `Docs/PROJECT_ROOT_BIBLES_COMBINED.md`

The regenerated snapshot now has:

- `Status: GENERATED_SNAPSHOT / STATIC_DOC / RUNTIME_PROOF_NOT_IMPLIED`
- `Evidence class: STATIC_DOC`
- generator path
- freshness boundary
- source policy excluding reports, task logs, status files, archives, deprecated docs, generated evidence, and edited AGENTS derivatives.

## Remaining Follow-Up

- Keep the generator source list aligned when `PROJECT_BIBLES.md`, `VISION_LOCKS.md`, `writing.md`, or standing root route-bible policy changes.
- Do not hand-edit the combined file; regenerate.
- Rerun hook-count and overclaim scans after any source-list change.

## Rejected Claims

- The combined file is not binding authority while marked stale.
- The stale header is not route-bible content regeneration.
- This audit is not runtime, compile, import, profiler, visual, or player-build proof.

## Regression Model

- CPU: static documentation scan only.
- GC: no runtime code changed. No `0 B/frame` claim.
- Memory: no runtime memory or Unity import changed.
- Cadence: no runtime cadence changed.
- Correctness: reduces stale-doc ambiguity; combined snapshot still requires regeneration.

Final status: `REGENERATED_STATIC_PASS / RUNTIME_PROOF_PENDING`.
