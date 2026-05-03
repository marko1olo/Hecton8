# Scatter Runtime Docs

Date: `2026-05-02`
Status: `PENDING VERIFICATION`

Purpose: canonical active bundle for scatter runtime refactor planning, baseline, and DOTS scope.

Current-state boundary:

- Read `Docs/Reports/2026-05-02_DOCUMENTATION_ACTUALITY_SWEEP.md` and `Docs/Reports/2026-05-01_CURRENT_PROJECT_STATE.md` before using this bundle as current project truth.
- Scatter runtime remains owned by `WorldProceduralScatterDirector` and adjacent scatter backend seams.
- DOTS/Entities scatter work is currently a disabled placeholder seam unless package, define, profiler parity, and runtime validation prove otherwise.
- Current source/package check: `com.unity.entities` is not declared in `Packages/manifest.json`.
- `Assets/_Project/Scripts/World/Dots/Hecton8.World.Dots.asmdef` exists, but is gated by defines and is not auto-referenced.
- No current first-party source path under `Assets/_Project/Scripts` uses `Unity.Entities`, `IComponentData`, `SystemBase`, or `ISystem`.
- This bundle is planning and architecture guidance, not proof of live scatter correctness or performance.

## Files

- `SCATTER_REFACTOR_EXECUTION_PLAN.md` - active scatter cleanup plan.
- `SCATTER_REFACTORING_MANIFESTO_V2.md` - stronger architectural manifesto for the director refactor.
- `SCATTER_PHASE1_BASELINE_CHECKLIST.md` - baseline validation checklist.
- `SCATTER_DOTS_NARROW_SCOPE_SPEC.md` - narrow DOTS scope.
- `ECS_DOTS_ADOPTION_PLAN.md` - broader DOTS adoption planning.

## Rule

Use this bundle as the active scatter planning zone instead of loose root-level `Docs/*.md` scatter files.
