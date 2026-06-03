# Manual Review Pass 21 - UI / HUD / Localization Line Closure

Status: STATIC METHOD REVIEW - NO RUNTIME PROFILER PROOF
Date: 2026-06-02

## Scope

Reviewed the 241 runtime suspect lines in:

- `02_ui_frontend_hud/RUNTIME_TRIAGE.md`
- `02_ui_frontend_hud/RUNTIME_PRECLASSIFICATION.md`
- `_scans/02_ui_frontend_hud_runtime_risks.txt`

This pass only classifies static source lines. It does not prove UI frame cost, Canvas rebuild cost, GC, player-build symbol stripping, localization expansion, input-remap IO, or compact/high readability.

## Classification Result

| Class | Count |
|---|---:|
| `LEGAL_EDITOR_OR_DEV_GUARDED` | 128 |
| `LEGAL_COLD_PATH` | 86 |
| `FALSE_POSITIVE` | 24 |
| `RUNTIME_VIOLATION` | 3 registered |

The full line table is in `02_ui_frontend_hud/LINE_LEVEL_CLASSIFICATION.md`.

## Decisions

- `AcousticRadarSphereRenderer.cs:531` remains a registered runtime asset fallback under `RB-131`: missing authored `voxelMesh` creates a runtime cube mesh and runtime material.
- `DiegeticVisorHudMesh.cs:438` remains a registered runtime asset fallback under `RB-131`: `OnEnable()` builds a runtime projection mesh and material, and the quality-policy dirty flag path still needs a fix or explicit bootstrap-only contract.
- `VehicleSubOsCockpitRuntime.cs:2601` is added to `RB-131`: missing authored damage proxy mesh creates a runtime fallback cube mesh.
- Settings/menu/PDA component lookups are cold setup or rebind-shaped, not proof that the UI is 0 B/frame.
- Font recovery is split: editor material repair is legal editor-only, while runtime font recovery/mesh refresh is cold/recovery-shaped and must not become normal text update behavior.
- RenderFeature material references are false positives at the flagged lines; shader/material lifecycle remains under `RB-125`.
- `H8Debug` UI/settings/input/terminal/visor logs are release-stripped, but the underlying systems still need player-build and profiler evidence.

## What Still Blocks Release Claims

- No 300-frame HUD/menu/PDA/cockpit/wrist profiler proof with 0 B/frame after bootstrap.
- No compact/high UI readability captures after localization expansion.
- No authored mesh/material/font proof for radar, visor projection, cockpit damage hologram, PDA, relay marker, and suit HUD.
- No `UIStateStore`, glitch, localization, and staged dictionary prewarm counters.
- No input-remap/user-options blocking IO proof.
- No proof that runtime fallback meshes/materials are unreachable in release scenes.

## Verdict

`YELLOW_LINE_LEVEL_STATIC_CLASSIFIED_RUNTIME_PROOF_PENDING`

The UI group is no longer an unclassified grep queue. It is a static line-classified system with explicit runtime asset fallback blockers and proof gates.
