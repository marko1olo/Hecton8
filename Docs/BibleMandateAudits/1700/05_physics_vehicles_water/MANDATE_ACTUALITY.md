# Physics / Vehicles / Water Mandate Actuality Report

Status: YELLOW_PHYSICS_MANDATES_VALID_BUT_PROOF_INCOMPLETE
Date: 2026-06-02
Evidence class: `STATIC_DOC` + `STATIC_SOURCE`

## What Exists

- Physics routes exist: `physics.md`, `vehicles.md`, `water.md`, `survival.md`, `combat.md`, `animation.md`, and `camera.md`.
- Mandates cover deterministic force routing, multithreaded body solving, tethers, fluid incursion, destructible organic entropy, and vehicle AUP.
- `LINE_LEVEL_CLASSIFICATION.md` classified 281 runtime suspect lines.

## What Is Not Correct Enough Yet

- Physics mandates are mostly valid but some lack explicit current `GlobalQualityWeight` wording.
- `RB-001`, `RB-015`, `RB-122`, and `RB-130` remain binding.
- `RuntimePhysicsBaker1609` is not current-source runtime cooking, but still needs serialized prebound `COL_*` proxy proof.

## Current Correct Mandate Interpretation

Physics truth is fixed-step, bounded, deterministic, and cheaper than visual detail. Visual LOD0 is never collision truth. Water, buoyancy, storm, pressure, and thermal systems may use cinematic fakes, but only with measured owner buffers, no blocking readbacks, and black-box proof.

## Required Proof

- Collider hierarchy audit and no LOD0 `MeshCollider`.
- Prebound `COL_*` proof for all collider proxy routes.
- 300-frame vehicle/tether/buoyancy/storm/reactor/thermal stress.
- Async readback latency, job completion, native growth, and PhysX transition telemetry.

