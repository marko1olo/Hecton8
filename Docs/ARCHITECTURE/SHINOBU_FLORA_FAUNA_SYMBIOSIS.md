# SHINOBU_62 Flora/Fauna Symbiosis

Runtime owner: `Hecton8.AI.Ecosystem.ShinobuFloraFaunaSymbiosisSolver`

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-18 R4 Interior Actuality Boundary

This document is active only where it agrees with current source, fresh verification artifacts, R24 root/architecture correction, R23 proof-language/navigation boundary, and R22 prior source-count boundary. `Tools/AtlasCheck.py` still has the known RealtimeCSG missing-reference blocker.

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, ecosystem runtime wiring, or visual proof is implied unless this document links a fresh evidence artifact. Static design claims are not fauna/flora runtime proof.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Contract

- No Unity physics proximity queries. Flora/fauna exchange uses Vault-backed spatial hash arrays: bucket heads plus next indices.
- Persistent memory is requested from `GlobalDataVault` through `VaultBufferHandle`; runtime classes keep handles, not private `NativeArray` ownership.
- `SymbiosisExchangeDTO` is 16 bytes and explicitly laid out for ARM64-safe loads.
- SHINOBU-owned AUP DTO lanes use `SymbiosisAup48`, not the legacy packed `AbsoluteUniversePosition` field. The lane is 48 bytes, offsets 0/8/16 for grid longs, 24/28/32 for local floats, and manual padding to byte 48.
- Default flora capacity is 50,000 and the flora spatial hash uses 65,536 buckets. This targets the prompt-scale ecosystem without nearest-neighbor allocation.
- `GlobalQualityWeight` continuously shifts between micro exchange and macro biomass average. No binary low-end hardware switch is introduced.
- Missing `symbiosis_chemical_links.h8bin` is handled by deterministic emergency mock records so CI and editor tools can prove biomass transfer.
- Legacy `symbiosis_chemical_links.h8bin` accepts raw little-endian records or a 16-byte `S62L`/`S62B` header; `S62B` uses `math.reversebytes` before `math.asfloat`.
- Emergency mock RNG is `Unity.Mathematics.Random` seeded from `ResolveFrameSectorSeed(centerAup, simulationFrame)`, mixing sector hash, solver frame, and a SHINOBU domain salt. Runtime telemetry also records the solver frame, not `Time.frameCount`.
- `Ecology Symbiosis Tuner` is a UI Toolkit editor facade. It writes Vault tuning DTOs and draws green SceneView lines by resolving each exchange to the nearest flora AUP for the recorded flora hash.

## Output Buffers

- `ShinobuSymbiosisExchanges`: active flora/fauna biomass transfers.
- `ShinobuSymbiosisScannerVfx`: toxin scalar hits for GPU poison-spore presentation.
- `ShinobuSymbiosisOxygenEmitters`: sector oxygen oasis scalars.
- `ShinobuSymbiosisAdherence`: parasite/barnacle hull attachment requests.
- `ShinobuSymbiosisSeeds`: bioluminescent pollination seed drops.
- `ShinobuSymbiosisAcousticTaps`: dense cluster acoustic crackle source, bridged to `AcousticPingSignal`.
- `ShinobuSymbiosisTelemetryRing`: 300-frame black-box state.

## Dear Lie

Chemistry is scalar radius math. Nutrients, toxins, camouflage, oxygen, pollen, and parasite growth are emitted as small DTO rows for renderer/audio/AI consumers. There is no nutrient particle simulation, no trigger collider, and no GameObject debug graph in runtime.
