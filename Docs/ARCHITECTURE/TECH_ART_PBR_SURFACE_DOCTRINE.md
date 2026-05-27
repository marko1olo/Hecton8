# Tech Art PBR Surface Doctrine

Date: 2026-05-26
Status: ACTIVE ARCHITECTURE CONTRACT
Owner: DOCS_ACTUALIZATION
Evidence class: STATIC_DOC / STATIC_SOURCE

Purpose: stable contract for PBR surface authoring, shader/material ownership, and visual-cheat scaling. This is not a material audit log.

Full pre-distillation snapshot: `Docs/DEPRECATED/Root_Generated_Snapshots_2026-05-26/TECH_ART_PBR_SURFACE_DOCTRINE.md`.

## Authority

- Runtime source and material assets own implementation details.
- `Docs/PROJECT_BASELINE.md` owns global documentation boundaries.
- `Docs/ARCHITECTURE/CINEMATIC_CHEATS_LEDGER.md` owns approved fake-first rendering patterns.
- Proof requires screenshots, render/debugger evidence, shader-variant evidence, GPU timing, or runtime logs. Static doctrine alone proves nothing.

## Surface Rules

- Prefer authored masks, lookup textures, packed channels, impostors, and baked variation over simulated micro-physics.
- Material identity must be data-routed. No runtime `Shader.Find`, uncached string property churn, or uncontrolled material instancing in hot paths.
- Use stable IDs for biome/surface families so save data, streaming, and visual systems do not depend on scene object names.
- Surface detail scales continuously with `GlobalQualityWeight`: cadence, sample count, normal/detail layers, decal density, and optional telemetry may change; gameplay truth and data identity may not.
- Low hardware path preserves silhouette, albedo class, roughness intent, and damage/wetness state.
- High hardware path spends saved time on richer normals, blend layers, caustics/decal density, and overkill presentation.

## Rejection Rules

- Reject material workflows that require per-frame managed allocation.
- Reject shader/material lookup from read accessors.
- Reject binary quality switches; use continuous weights and capped bands only as authoring labels.
- Reject visual claims without an artifact path.
