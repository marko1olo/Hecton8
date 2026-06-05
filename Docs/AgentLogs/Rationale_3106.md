# Rationale 3106 - Underwater Route Volume Owner

Date: 2026-06-05
Evidence class: STATIC_DOC / STATIC_SOURCE / STATIC_IMAGE_REVIEW

## Decision 1: Reject Current Underwater Proof Labels

Reason: `h8_1473_underwater_0_5m.png`, `h8_1473_underwater_20_50m_route.png`, `h8_1474_underwater_0_5m.png`, and `h8_1474_underwater_20_50m_route.png` are surface horizon shots. Sky, Aegir, coastline, and ocean skin dominate. They do not prove underwater volume, surface underside, depth band, seafloor route, particles, or return readability.

`h8_1473_mainrt_underwater_0_5m.png` is an underwater-ish flat green fill with a broad yellow/green slab and no usable route grammar. It also fails.

## Decision 2: Treat Underwater VFX As Visual Fake, Not Gameplay Truth

Mandate basis:

- Fluid VFX is presentation, not fluid truth.
- Default proof route is depth fade, authored flow masks, GPU-side drift, and event-driven pooled emitters.
- Physical simulation is allowed only when gameplay correctness breaks without it.

Therefore motes, marine snow, bubbles, beams, haze, and surface sheet must be visual consequences driven by owner state. They must not own pressure, route, damage, AI, oxygen, or save truth.

## Decision 3: Do Not Accept Null/Color-Only VFX Masks

Static material inspection found null texture slots in:

- `MAT_H8_PhoticMotes_1428`
- `MAT_H8_PhoticFishSilhouette_1430`
- `MAT_H8_SurfaceFoamRing_1432`
- `MAT_H8_VisibleFoamUnlit_1436`

Color-only transparent materials cannot prove premium underwater particles, fauna scale, foam contact, or route readability.

## Decision 4: Route Anchors Need Depth Predicates

Scene route labels exist, including `Route_Frontier`, `Lane_DarkRoute`, `Lane_BeaconRoute`, and `Route_Anchor`. Static labels do not prove first-route usability.

For the next proof pass, use `Route_Anchor` plus `Lane_BeaconRoute` as the return/forward proof pair. Do not use `Lane_DarkRoute` as the first 0-50 m proof identity; 0-100 m must remain bright, readable, and photic unless inside a cave or temporary event.

## Decision 5: Block Unity Mutation While Gate Is Red

Sampled active processes include Unity, Unity.ILPP.Runner, UnityAutoQuitter, and UnityShaderCompiler. Per task and root guardrails, no Unity/build/asset mutation was performed.

## Regression Model

- CPU: no runtime code changed. Future VFX route must prove no suspicious 0.1 ms+ feature without load-shed.
- GC: no runtime code changed. Future hot VFX paths require 0 B/frame profiler or GCMonitor proof.
- Memory/VRAM: no assets imported or rebound. Future particle/mask textures must report VRAM impact.
- Cadence: proposed VFX scales through continuous `GlobalQualityWeight`, not binary lanes.
- Correctness: route and pressure truth remain with world/water/player systems; VFX only presents.

## First-20-Minutes Impact

Removes a proof blocker for the bright photic exit, first swim, return-route readability, and oxygen/depth pressure route. It does not prove gameplay until player capture and route predicates exist.
