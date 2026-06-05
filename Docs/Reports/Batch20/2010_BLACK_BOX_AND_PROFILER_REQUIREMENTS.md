# Batch20 / 2010 Black-Box And Profiler Requirements

Status: STATIC VERIFIED / NO UNITY / NO IN-GAME PROOF

## Scope

These are mandatory proof requirements for Batch20 visual proof and scene repair systems that consume or publish `GlobalQualityWeight`.

## Black-Box Ring Requirement

Every critical visual-adjacent runtime owner must expose a fixed 300-frame ring when it can affect route readability, hazard readability, visual truth, or player-facing proof.

Minimum entry fields:

- frame index;
- system id or owner hash;
- `GlobalQualityWeight`;
- state hash;
- active count or resolution;
- cadence/tick interval;
- CPU or GPU microsecond sample when available;
- memory/buffer generation where DataVault-backed;
- flags for NaN, overflow, dropped signal, over-budget, missing data, fallback active, dump failure;
- route-specific scalar: foam count, wake resolution, fog/turbidity, light count, flora density, terrain/LOD bucket, screenshot/proof capture id as applicable.

Minimum ring contract:

- capacity exactly 300 frames unless a larger capacity has compact memory proof;
- fixed-size blittable entries;
- no managed references;
- no hot-path allocation;
- cursor wraps deterministically;
- dump writes oldest-to-newest order;
- dump path deterministic.

With active batch ID `2010`, new proof dumps for this matrix should use:

- `Docs/AgentLogs/Dump_2010_<SystemName>.bin`
- `Docs/AgentLogs/Dump_2010_<SystemName>.json` for manifest

Existing inspected systems already declare static black-box routes:

- Ocean single-pass telemetry capacity: 300.
- Shoreline foam telemetry capacity: 300.
- Toxic outgassing telemetry capacity: 300 and NaN-triggered dump.

Static declaration is not proof that the ring ran in scene.

## Dump Triggers

Dump on:

- NaN or infinity in runtime state, shader input, density, flow, foam, wake, or quality-derived scalar;
- over-budget threshold breach sustained past configured hysteresis;
- buffer overflow, dropped signal storm, or telemetry write failure;
- missing required DataVault buffer after system marked ready;
- quality scalar outside `[0,1]` after sanitation;
- render pass cost spike if pass owns route readability;
- manual dev command for proof packet capture.

## Profiler Requirements

Every changed runtime route must provide:

- Unity Profiler capture with named marker;
- GC Alloc proof: 0 B/frame for at least 300 gameplay frames;
- CPU main-thread and render-thread timing where relevant;
- GPU timing for render passes where available;
- Frame Debugger or RenderGraph capture for new/changed render path;
- Memory Profiler or VRAM capture when textures, RTs, buffers, particles, HLOD residency, or material variants changed;
- compact hardware or compact-profile capture before any high/ultra claim.

Required marker naming pattern:

- `H8.Quality.<Domain>.Apply`
- `H8.Quality.<Domain>.Upload`
- `H8.Quality.<Domain>.Render`
- `H8.Quality.<Domain>.Telemetry`
- `H8.Quality.<Domain>.LoadShed`

Existing inspected marker/sample names include:

- `H8 Ocean Depth Dear Lie`
- `H8 Ocean Wake Dear Lie`

Those names are acceptable only if profiler artifacts show them and tie them to the relevant proof packet.

## Load-Shed Proof

Every system must prove what happens under pressure:

- frame time over 25 ms for configured hysteresis;
- VRAM used/total over 0.90;
- memory pressure;
- thermal trend;
- telemetry overflow.

Load-shed order:

1. optional diagnostics;
2. decorative particles;
3. secondary shadows;
4. expensive reflection/refraction;
5. volumetric layers;
6. far-field density and HLOD residency;
7. never the only survival warning, route cue, hazard silhouette, water color, sky readability, or instrument legibility.

## Rejection Rules

Reject any proof packet that:

- claims runtime readiness from static docs or Python output;
- reports exact frame time, GC, or VRAM without captured artifacts;
- omits compact capture;
- omits high/ultra comparison for visual-overkill claims;
- lacks 300-frame telemetry for critical systems;
- hides bad visuals behind darkness, fog, bloom, or post;
- changes gameplay truth through quality;
- uses binary switches instead of continuous scalar scaling;
- uses profiler data without scene, camera, hardware/tier, and repro context.

## Current Task State

Worker 2010 produced requirements only. No Unity, Play Mode, profiler, Frame Debugger, Memory Profiler, screenshot, or dump artifact was generated.
