# SHINOBU_237 Propwash GPU Director

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-21 R51 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, shader import, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.
Current DOC_GLOBAL boundary (2026-05-21 R51): `Docs/Reports/2026-05-21_DOCUMENTATION_R51_ROOT_ARCHITECTURE_ENCODING_BOUNDARY_READORDER_AND_ROUTE_GAPS_LOCAL.md` is the latest local static root/architecture encoding repair, boundary-gap, read-order, route-card/static-contract, and source/AtlasCheck orientation correction. R50 remains the prior generated-atlas regeneration, stale R48 interior-boundary, dump-target wording, and source-counter drift correction. R49 remains the prior AtlasCheck-red-state/boundary-gap/route-field/source-counter correction. R48 remains the prior date-rollover/AtlasCheck/source-counter correction. R47 remains the prior authority-spine/runtime-wording/counter-drift correction. R46 remains the prior interior-authority/route-field/proof-language correction. R45/R44/R43/R42/R41/R40/R39/R38/R37/R36/R35/R34 remain prior static correction layers. Current AtlasCheck remains red until `Tools/AtlasCheck.py` exits `0`; runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

Domain: presentation-only silt and propwash rendering.

Authority route:
- CPU owns only compact thrust DTO harvest into `GlobalDataVault` buffers `PropwashGpuEventRing`, `PropwashGpuRingCursor`, `PropwashGpuTelemetryRing`, `PropwashGpuTuning`, and `PropwashGpuWakeProfiles`.
- `PropwashEventDTO` is the GPU wire payload: 32 bytes, `float3 LocalPosition`, `float3 ThrustVector`, `float Intensity`, `float Radius`.
- `PropwashWakeProfileDTO` is a cold editor/source-data tuning payload: 64 bytes, FNV-1a engine hash plus emission, lifetime, turbulence, radius, intensity, lift, tint, curl, and jitter scalars from `Assets/_SourceData/VFX/Propwash/vehicle_wake_profiles.csv`.
- The CSV parser is byte-span based and fail-closed for numeric tokens: trailing bytes after a parsed number reject the field/row instead of hydrating partial values. Optional wake-profile columns may be absent or empty; malformed present optional values reject the row.
- Player builds do not read propwash CSV from `StreamingAssets`. CSV staging buffers, background reader, file IO, and parser refresh are compiled only under `UNITY_EDITOR`; non-editor/player lifecycle calls are no-ops. Until a domain `.h8bin`/Data Monolith route hydrates `PropwashGpuWakeProfiles`, runtime uses deterministic default wake rows.
- CPU upload to `_PropwashEvents` is double-buffered (`_propwashEventBufferA/B`) through `LockBufferForWrite`; the inactive buffer receives the next frame before becoming the compute read buffer.
- Upload consumes `PropwashRingCursorDTO.WriteCursor` and `EventCount`, computes the wrapped oldest slot, and writes a contiguous GPU snapshot. The Vault ring remains circular; the shader sees a dense linear buffer.
- `Hecton_MarineSnow.compute` owns SDF/depth proximity, particle injection, propwash advection, AUP rebase, and indirect-visible count mutation.
- `Hecton_MarineSnow.compute` tags propwash silt with particle flag bit 3; `Hecton_MarineSnow.shader` consumes the same bit and `_PropwashBiomeTint.rgb` so biome color reaches the visible material pass without widening the particle stride.
- `HectonMarineSnowRenderer` submits through non-indexed `Graphics.DrawProceduralIndirect`; the indirect args buffer is 16 bytes and the CPU never reads the GPU visible particle count.

Rollback/netcode exclusion:
- Propwash particles, event cursor, telemetry, biome tint, and tuner values are visual presentation state only.
- Gameplay authority remains vehicle kinematics, submarine physics, SDF collision truth, and save/network state owned by their existing domains.
- No `PropwashGpu*` buffer is a rollback/Merkle descriptor. GlobalQualityWeight may scale sample budget and particle budget, but never DTO layout, gameplay truth, save identity, or authority route.

Scalability:
- Low: 4 propwash event samples, low marine-snow particle budget, cheap radial/lift approximation.
- Middle: continuous increase of event sampling and curl response.
- High: denser SDF/depth-reactive silt injection and stronger biome tint variation.
- Ultra: consumes the full 500-event mock/harvest stress payload and spends GPU budget on visual overkill density.
- Wake proximity SDF/height dispatch uses the same continuous event sample budget as propwash flow sampling, so low quality reduces both advection event reads and near-floor silt injection work without changing the Vault ring.

Black box:
- `PropwashTelemetryEntry` stores 300 frames in `PropwashGpuTelemetryRing`.
- On black-box dump, raw telemetry bytes are written to `Docs/AgentLogs/Dump_SHINOBU_237.bin`.

Rejected:
- Unity `ParticleSystem.Emit()` for propwash/silt.
- `Physics.Raycast` or `RaycastNonAlloc` for cosmetic seabed proximity.
- CPU visible-particle counts or managed vehicle lists in the hot path.
