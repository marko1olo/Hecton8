# SHINOBU_62 Flora/Fauna Symbiosis

Runtime owner: `Hecton8.AI.Ecosystem.ShinobuFloraFaunaSymbiosisSolver`

Source anchors: `Assets/_Project/Scripts/AI/Ecosystem/ShinobuFloraFaunaSymbiosisSolver.cs`, `Assets/_Project/Scripts/Editor/EcologySymbiosisTunerWindow.cs`.

## Contract

- No Unity physics proximity queries. Flora/fauna exchange uses Vault-backed spatial hash arrays: bucket heads plus next indices.

- Persistent memory is requested from `GlobalDataVault` through `VaultBufferHandle`; runtime classes keep handles, not private `NativeArray` ownership.

- `SymbiosisExchangeDTO` is 16 bytes and explicitly laid out for ARM64-safe loads.

- SHINOBU-owned AUP DTO lanes use `SymbiosisAup48`, not legacy packed `AbsoluteUniversePosition`.
- Lane size: 48 bytes.
- Offsets 0/8/16: grid longs.
- Offsets 24/28/32: local floats.
- Manual padding reaches byte 48.

- Default flora capacity is 50,000 and the flora spatial hash uses 65,536 buckets. This targets the prompt-scale ecosystem without nearest-neighbor allocation.

- `GlobalQualityWeight` continuously shifts between micro exchange and macro biomass average. No binary low-end hardware switch is introduced.

- When `GlobalQualityWeight` falls below `MacroThreshold`, the scheduler does not build the flora spatial hash; the macro solver uses strided biomass sampling instead.

- Feeding attenuation and anomaly blight use guarded squared-distance scalar falloff. This is a deliberate Dear Lie to avoid hot sqrt cost in ecosystem chemistry.

- Missing `symbiosis_chemical_links.h8bin` is handled by deterministic emergency mock records so CI and editor tools can prove biomass transfer.

- Legacy `symbiosis_chemical_links.h8bin` accepts raw little-endian records or a 16-byte `S62L`/`S62B` header; `S62B` uses `math.reversebytes` before `math.asfloat`.

- Emergency mock RNG is `Unity.Mathematics.Random` seeded from `ResolveFrameSectorSeed(centerAup, simulationFrame)`.
- Seed mixes sector hash, solver frame, and SHINOBU salt. Runtime telemetry records solver frame, not `Time.frameCount`.

- `Ecology Symbiosis Tuner` is a UI Toolkit editor facade.
- It writes Vault tuning DTOs.
- It draws green SceneView lines by resolving each exchange to nearest flora AUP for the recorded flora hash.

## Output Buffers

- `ShinobuSymbiosisExchanges`: active flora/fauna biomass transfers.

- `ShinobuSymbiosisScannerVfx`: toxin scalar hits for GPU poison-spore presentation.

- `ShinobuSymbiosisOxygenEmitters`: sector oxygen oasis scalars.

- `ShinobuSymbiosisAdherence`: parasite/barnacle hull attachment requests.

- `ShinobuSymbiosisSeeds`: bioluminescent pollination seed drops.

- `ShinobuSymbiosisAcousticTaps`: dense cluster acoustic crackle source, bridged to `AcousticPingSignal`.

- `ShinobuSymbiosisTelemetryRing`: 300-frame black-box state.

## Dear Lie

- Chemistry is scalar radius math.
- Emitted DTO rows: nutrients, toxins, camouflage, oxygen, pollen, parasite growth.
- Consumers: renderer, audio, AI.
- Runtime forbids nutrient particles, trigger colliders, and GameObject debug graphs.
