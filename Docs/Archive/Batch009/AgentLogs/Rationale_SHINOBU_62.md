# Rationale_SHINOBU_62

## Decision 001: Ocean Prompt Authority

Problem: duplicate `SHINOBU_62` prompts exist and a parallel stale writer repeatedly restores flora/fauna files.
Solution: use the second `SHINOBU_62` block, `OCEAN_SURFACE_AND_ATMOSPHERE_DIRECTOR`, because the user request explicitly names Gerstner waves, atmospheric scattering, no skyboxes, and buoyancy/visual desync.
Rejected Alternatives: merging flora/fauna work, using the first duplicate block, or touching unrelated ecosystem files.
Scalability potential: Low/Middle/High/Ultra lanes apply to wave count, radial grid density, foam, rain-normal detail, and atmosphere scalar richness.
Hardware Impact: zero runtime cost; prevents wrong-domain churn.

## Decision 002: Shared Gerstner Truth

Problem: buoyancy and visual waves desync when CPU and shader use different time, phase, quality, or AUP basis.
Solution: CPU/Burst `EvaluateWaves` and HLSL use the same wave budget curve, wavelength wrap, phase wrap, and AUP-projected phase input.
Rejected Alternatives: FFT ocean, flat displacement, Crest material runtime wrappers, or per-object visual fudge.
Scalability potential: Low 4 waves/no foam; Ultra up to 16 waves plus foam/rain detail.
Hardware Impact: CPU remains O(queryCount * activeWaves).

## Decision 003: Shader AUP Projection Parity

Problem: `_H8OceanCameraAupLocalProjection` was published but not included in HLSL phase input, risking visual/physics drift after large AUP shifts.
Solution: add `H8OceanResolveAupProjectedXZ(cameraLocalXZ)` and feed `projectedAupXZ` to `H8OceanWrappedPhase`.
Rejected Alternatives: absolute GPU coordinates, local-only phase, or large float world positions.
Scalability potential: invariant across tiers; quality changes wave count, not spatial truth.
Hardware Impact: stable visual/physics phase at 50km+.

## Decision 004: Data, Vault, And Compile Gate

Problem: ARM64 layout, H-PHI ownership, and compile-wall boundaries must remain intact.
Solution: keep explicit 32B/64B DTOs, Vault handles only, core provider route, exact Burst flags, `[NoAlias]`, and no build while CPU gate is 100%.
Rejected Alternatives: `Pack=1`, private NativeCollections, direct sibling registry, arbitrary `.Complete()`, or violating the build gate.
Scalability potential: fixed memory envelope; `GlobalQualityWeight` controls ALU and GPU density continuously.
Hardware Impact: no hot allocations; build hardware protected.
