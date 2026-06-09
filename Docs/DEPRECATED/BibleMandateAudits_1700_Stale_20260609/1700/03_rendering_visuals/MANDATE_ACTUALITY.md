# Rendering / VFX Mandate Actuality Report

Status: YELLOW_RENDER_MANDATES_NEED_QUALITY_REFRESH
Date: 2026-06-02
Evidence class: `STATIC_DOC` + `STATIC_SOURCE`

## What Exists

- Rendering routes exist: `rendering.md`, `shaders.md`, `lighting.md`, `vfx.md`, `presentation.md`, `water.md`, `atmosphere.md`, `platform.md`, `performance.md`, and `compute.md`.
- Rendering mandates cover URP, shader noir, instancing, fluid VFX, VRS, GPU sovereignty, occlusion, and texture streaming.
- `LINE_LEVEL_CLASSIFICATION.md` classified 109 runtime suspect lines.

## What Is Not Correct Enough Yet

- `RB-123` remains: fallback VFX mesh/material/RT assets must be authored or proven release-unreachable.
- Several rendering mandates contain older or incomplete quality scaling language, especially GPU/URP/VAT/VRS related files.
- Static RenderGraph shape is not Frame Debugger or GPU proof.

## Current Correct Mandate Interpretation

Rendering must buy Deep Sea Noir through authored assets, SRP-compatible material discipline, fixed GPU resource lifetimes, async/nonblocking readback policy, and continuous quality scaling. Runtime fallback geometry is not acceptable as production visuals.

## Required Proof

- RenderGraph Viewer and Frame Debugger captures.
- Shader variant and material batching proof.
- GPU/VRAM captures on compact and high lanes.
- Readback latency, upload-byte, and resource recreate counters.

