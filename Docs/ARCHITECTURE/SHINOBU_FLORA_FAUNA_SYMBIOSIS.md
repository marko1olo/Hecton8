# SHINOBU_62 Flora/Fauna Symbiosis

Runtime owner: `Hecton8.AI.Ecosystem.ShinobuFloraFaunaSymbiosisSolver`
Source anchors: `Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs`, `Assets/_Project/Scripts/Editor/EcologySymbiosisTunerWindow.cs`.

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

## Contract

- No Unity physics proximity queries. Flora/fauna exchange uses Vault-backed spatial hash arrays: bucket heads plus next indices.
- Persistent memory is requested from `GlobalDataVault` through `VaultBufferHandle`; runtime classes keep handles, not private `NativeArray` ownership.
- `SymbiosisExchangeDTO` is 16 bytes and explicitly laid out for ARM64-safe loads.
- SHINOBU-owned AUP DTO lanes use `SymbiosisAup48`, not the legacy packed `AbsoluteUniversePosition` field. The lane is 48 bytes, offsets 0/8/16 for grid longs, 24/28/32 for local floats, and manual padding to byte 48.
- Default flora capacity is 50,000 and the flora spatial hash uses 65,536 buckets. This targets the prompt-scale ecosystem without nearest-neighbor allocation.
- `GlobalQualityWeight` continuously shifts between micro exchange and macro biomass average. No binary low-end hardware switch is introduced.
- When `GlobalQualityWeight` falls below `MacroThreshold`, the scheduler does not build the flora spatial hash; the macro solver uses strided biomass sampling instead.
- Feeding attenuation and anomaly blight use guarded squared-distance scalar falloff. This is a deliberate Dear Lie to avoid hot sqrt cost in ecosystem chemistry.
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
