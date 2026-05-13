# INTERNAL_FLOOD_RENDERER Rationale

Status: PENDING VERIFICATION

## Initial Decision Frame

Problem: Habitat rooms flood in scalar gameplay state but presentation remains dry until full camera submersion.
Solution: Use deterministic screen-space waterline inside the existing Visor Uber Post path, fed by habitat room fill ratios. Physical water planes and per-room meshes are rejected.
Rejected Alternatives: `WaterPlaneManager.Instance`, `FloodVfxManager.Instance`, and `Instantiate(WaterMeshPrefab)` patterns would add scene objects, synchronization hazards, overdraw, and singleton dependency rot.
Scalability potential: Low = color tint only under split; Middle = mild refraction; High = refraction plus droplets; Ultra = stronger procedural detail and longer droplet persistence without new passes.
Hardware Impact: Expected low-end gain on i3/MX350 comes from avoiding mesh spawning and full-screen extra passes. Numeric proof is PENDING VERIFICATION until profiler/Unity capture exists.

## Mandate Binding

Problem: Task crosses habitat simulation, render presentation, AUP, and telemetry.
Solution: Bind implementation to selected mandates: fluid incursion fake-first, cinematic cheat protocol, URP RenderGraph/hotpath rules, noir shader doctrine, AUP shift safety, GlobalRegistry DI, zero-GC, and blackbox telemetry.
Rejected Alternatives: Direct concrete references between habitat, VFX, and gas systems are rejected; only contracts, `GlobalRegistry` owner queries, or existing event/signal lanes are allowed.
Scalability potential: Math LOD must keep MX350 path tint-only and spend saved cost on stronger high-tier post detail.
Hardware Impact: Avoiding per-frame allocation and scene search preserves hot-path GC target at 0 B/frame; measured proof is absent.
