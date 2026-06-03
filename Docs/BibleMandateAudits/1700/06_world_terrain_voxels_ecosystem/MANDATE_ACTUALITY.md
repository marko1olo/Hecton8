# World / Terrain / Voxels / Ecosystem Mandate Actuality Report

Status: YELLOW_WORLD_VOXEL_MANDATES_NEED_LEGACY_REFRESH
Date: 2026-06-02
Evidence class: `STATIC_DOC` + `STATIC_SOURCE`

## What Exists

- World routes exist: `world.md`, `voxels.md`, `terrain.md`, `streaming.md`, `ecosystem.md`, `water.md`, `rendering.md`, and `math.md`.
- Voxel/SDF mandates are mostly route-covered, and core generated asset routes exist.
- `LINE_LEVEL_CLASSIFICATION.md` classified 254 runtime suspect lines.

## What Is Not Correct Enough Yet

- `VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt` contains legacy wording and needs refresh.
- `RB-001` and `RB-015` remain cross-routed violations.
- Vegetation, radar, scatter, SDF, voxel fade, chunk residency, and material pools are static-proofed only, not runtime-proven.

## Current Correct Mandate Interpretation

World richness must come from authored packages, deterministic seeds, SDF/DataMonolith truth, fixed native storage, and bounded GPU upload/readback cadence. Prototype shells, mock SDF, runtime fallback packages, and material-pool churn are not release truth.

## Required Proof

- DataMonolith/SDF publication proof.
- Vegetation/radar/scatter/chunk/voxel/material-pool stress.
- Terrain seam and voxel transition captures.
- Authored world package proof and compact/high profiler/device captures.

