# LOG: DOC_HONEST_ANALYSIS

Date: 2026-05-15
Evidence class: FILESYSTEM / STATIC_DOC / STATIC_TEXT_SCAN

## Documentation Honest Analysis

What was wrong:

- Some current navigation docs still described former root mirrors and root evidence files as if they were still in repository root after the May 15 cleanup.
- `COMPUTE_DOMINANCE_REPORT.md` still framed compute slices as near-root files.
- `Docs/Reports/2026-05-15_COMPUTE_AUDIT/README.md` lagged the live bundle contents.
- `Docs/README.md` and `Docs/Reports/README.md` still had May 13 counters worded too close to current truth.

What was done:

- Patched current navigation docs and report indexes to point to the May 15 bundle/deprecated paths.
- Added `Docs/Reports/2026-05-15_DOCUMENTATION_HONEST_ANALYSIS.md`.
- Updated the compute bundle README to include `COMPUTE_ENERGY_EQUIVALENTS.md` and `COMPUTE_LIVE_BURN_PERSISTENCE_CHECK.md`.
- Preserved historical dated reports instead of rewriting snapshots.

Cinematic Cheats used:

- Not applicable. No runtime simulation, rendering, physics, VFX, water, fog, light, particles, or gameplay path changed.

Exact Microseconds saved:

- Runtime: `0` claimed. No profiler evidence was collected and no runtime path changed.
- Documentation/process only: root/current-index ambiguity reduced; no runtime timing claim.

Verification:

- Root filtered scan: only `AGENTS.md`, `BUILD_PLAYTEST_ISSUES.md`, and `MASTER_RELEASE_WORK_PLAN.md`.
- Compute bundle file count: `21`.
- Direct `Docs/Reports/*.md` count observed: `91`.
- Direct `Docs/*.md` count observed: `15`.
- Boundary: static/filesystem only. No Unity Console, Play Mode, profiler, GCMonitor, player-build, scene-wiring, or visual proof.

## Continuation R2 - Archivarius Pointer Cleanup

What was wrong:

- Active Archivarius navigation still promoted May 11 continuation/manifest and May 4 actuality sweep as current/latest evidence in several places.
- Domain-map trust notes could send agents into historical May 4 counters before the May 13/May 15 correction boundary.

What was done:

- Patched Archivarius index, authority classification, concept map, coverage matrix, master index, project atlas, Reports README, and domain-map trust notes.
- May 13 DOC_AUDIT X-Ray and May 15 documentation honest analysis now precede older counter/root/build-artifact claims in current navigation.
- May 11 and May 4 artifacts remain preserved as historical evidence.

Cinematic Cheats used:

- Not applicable. Markdown-only documentation routing.

Exact Microseconds saved:

- Runtime: `0` claimed. No runtime path changed and no profiler data collected.

Verification:

- Focused stale-phrase scan no longer finds the targeted `current/latest` May 11 phrases in active navigation surfaces.
- Markdown diff whitespace check and root filtered scan completed separately in the continuation pass.

## Continuation R3 - H-Phi Core Graph Prune

What was wrong:

- Fresh H-Phi summary found current Core asmdef debt at `26`, while the accepted R49 ceiling was `25`.
- `Hecton8.World.GPR` was present as a high-confidence unused Core asmdef reference candidate during transient workspace/index drift.

What was done:

- Aligned the current file/index so `Assets/_Project/Scripts/Hecton8.Core.asmdef` contains no `Hecton8.World.GPR` reference.
- Kept World GPR runtime code untouched.
- Updated H-Phi/stable documentation with the new static and CLI evidence boundary.

Cinematic Cheats used:

- Not applicable. No runtime simulation, visual, physics, VFX, water, fog, light, or gameplay path changed.

Exact Microseconds saved:

- Runtime: `0` claimed. No profiler evidence was collected and no runtime path changed.
- Tooling elapsed: H-Phi summary `142717567` us, Core graph post-prune `26030675` us, Core CLI compile `85479750` us.

Verification:

- `HPhi_DOC_HONEST_ANALYSIS_R3_20260515_CoreGraphAfterGprPrune.json`: `EXIT=0`, Core graph debt `25/10/14/8/6`, unused Core candidates cleared.
- `Build_DOC_HONEST_ANALYSIS_R3_20260515_AfterGprAsmdefPrune_Hecton8Core.log`: `Build succeeded`, `0 Warning(s)`, `0 Error(s)`.
- Runtime proof remains `PENDING VERIFICATION`.
