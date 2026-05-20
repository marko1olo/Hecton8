# SHINOBU_151 Dynamic Point Light Culling Route Card

Date: 2026-05-19
Owner: SHINOBU_151
Domain: Echelon 7 Graphics & Lighting
Evidence: POLISH STATIC SOURCE / GUARDED COMPILE PENDING

<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_START -->
## 2026-05-20 R47 Root/Architecture Actuality Boundary

This document is active only where it agrees with:

- `Docs/README.md`
- `Docs/DOC_GOVERNANCE.md`
- `Docs/ARCHITECTURE/HECTON8_DOCUMENTATION_ACTUALITY_LEDGER.md`
- current source files
- fresh verification logs and artifacts

No Unity import, Unity Console, Play Mode, profiler, GCMonitor, Memory Profiler, Frame Debugger, player build, save/load route, or visual-route proof is implied unless this document links a fresh evidence artifact. Historical counters and older version claims inside this file are subordinate to the current authority spine above.

R47 root/architecture authority-spine/runtime-wording/counter-drift correction (`Docs/Reports/2026-05-20_DOCUMENTATION_R47_ROOT_ARCHITECTURE_AUTHORITY_SPINE_RUNTIME_WORDING_AND_COUNTER_DRIFT_LOCAL.md`) is the latest local static DOC_GLOBAL boundary for architecture/root documentation. R46 remains the prior interior-authority/route-field/proof-language correction. R45 remains the prior R43/R44 residue/proof-artifact/source-counter correction; R44 remains the prior internal-residue/exact-route-field/proof-wording correction; R43 remains the prior route-card/counter-residue/AtlasCheck red-state correction; R42 remains the prior counter/route-boundary/proof-label correction; R41 remains the prior global-authority/internal-residue correction; R40 remains the prior R38-residue/source-counter correction; R39 remains the prior authority-counter/proof-wording correction; R38/R37/R36/R35/R34 remain prior static correction layers. Runtime proof remains absent.
<!-- DOC_GLOBAL_DOCS_REFRESH:R4_INTERIOR_BOUNDARY_END -->

## Boundary

SHINOBU_151 owns presentation-only dynamic point/spot light suppression for abyss and habitat traversal. It does not own gameplay truth, rollback hashes, base-building lamp authority, Unity `Light` components, or global render pipeline feature registration.

The runtime route is:

1. `DynamicPointLightSourceDTO` records in Vault.
2. `DynamicPointLightSourceManifestDTO` commits the valid source window after source/state records are fully written.
3. Manual VP-matrix frustum extraction, Burst frustum, squared-distance fade, and SDF occlusion.
4. Radix-sort importance keys.
5. Top-N `DynamicPointLightGpuDTO` payload, written from Burst through ref/raw-pointer DTO access.
6. VISUAL_SYNC double-buffered `GraphicsBuffer.LockBufferForWrite` upload through prewarmed structured buffers.
7. Owner-local fake bounce stream in Vault buffer `71454` for the probe-grid owner.

No Unity `Light` object is instantiated or toggled by this route.

## Legacy Emitter Archaeology

Prior static project scan text found no `LightDistanceCull` script and no `Vector3.Distance` light-distance-cull pattern, and reported gameplay-owned Unity `Light` toggles in `PlayerFlashlight`, `RepairTool`, `DeployableFlare`, `GravTrap`, and a flashlight voxel-shadow provider, plus `13` authored Light YAML components. Treat that as STATIC_SOURCE orientation only until the exact scan command, timestamp, environment, output path, scanned root, and unresolved list are attached. SHINOBU_151 does not delete cross-domain emitters. Their migration route is to write `DynamicPointLightSourceDTO` records and commit SourceManifest buffer `71458`; this culler then owns mathematical survivor selection, GPU payload upload, and fake probe-bounce packets.

## Vault IDs

Owner-local BufferID lane:

- `71440` Sources: `DynamicPointLightSourceDTO[sourceCapacity]`
- `71441` States: `LightCullStateDTO[sourceCapacity]`
- `71442` Settings: `DynamicPointLightCullingSettingsDTO[1]`
- `71443` GpuPayloadFront: `DynamicPointLightGpuDTO[64]`
- `71444` GpuPayloadBack: `DynamicPointLightGpuDTO[64]`
- `71445` TelemetryRing: `DynamicPointLightCullingTelemetryEntry[300]`, cold-cleared for valid pre-roll blackbox dumps
- `71446` TelemetryCursor: `int[1]`
- `71447` ImportanceKeys: `uint[sourceCapacity]`
- `71448` ImportanceIndices: `int[sourceCapacity]`
- `71449` SortScratchKeys: `uint[sourceCapacity]`
- `71450` SortScratchIndices: `int[sourceCapacity]`
- `71451` CsvScratch: `byte[32768]`
- `71452` ProfileRules: `DynamicPointLightProfileRuleDTO[64]`
- `71453` MockSdfSamples: `float[resolution^3]`
- `71454` DynamicProbeLights: `CustomDynamicProbeLightDTO[64]`
- `71455` RuntimeCounters: `DynamicPointLightRuntimeCountersDTO[1]`
- `71456` FrustumPlanes: `float4[6]`
- `71457` SelfAudit: `DynamicPointLightSelfAuditDTO[1]`
- `71458` SourceManifest: `DynamicPointLightSourceManifestDTO[1]`, cold-cleared and committed only after source data is valid

## Layout

- `LightCullStateDTO`: explicit 32 bytes. Offsets: `LightHash=0`, `DistanceSq=4`, `BaseIntensity=8`, `ComputedIntensity=12`, `Flags=16`, `_pad0.._pad11=20..31`.
- `DynamicPointLightSourceDTO`: explicit 96 bytes. `double3 AUP` starts at offset 0; source record is 16-byte aligned by final 4-byte pad.
- `DynamicPointLightGpuDTO`: explicit 64 bytes, three float4 lanes plus hash/flags/distance/bounce scalar lane.
- `DynamicPointLightRuntimeCountersDTO`: explicit 64 bytes to keep the single producer/reader counter block cache-line bounded.
- `DynamicPointLightSourceManifestDTO`: explicit 64 bytes. Offsets: `ActiveSourceCount=0`, `SourceCapacity=4`, `WriterHash=8`, `SourceRevision=12`, `Flags=16`, `LastCommitFrame=20`, `RejectedSourceCount=24`, `VaultGeneration=28`, `_pad0.._pad3=32..63`.

`Pack=1` is not used.

## Scalability

`GlobalQualityWeight` is continuous. `ResolveMaxActiveLights(weight, thermal)` uses `math.step` only as a zero-quality numeric gate, a cubic smooth polynomial, and `math.lerp` to map low pressure toward 8 survivors and full quality to 64 survivors. Thermal damping is polynomial and branchless. The culling cadence also lerps from roughly 5 Hz under pressure to 60 Hz at high quality. Intensity fades by squared distance and thermal pressure before radix sorting, so lights disappear by weight, not by binary tier. Uncommitted source manifests or unseeded mock-SDF buffers publish count `0` and do not read uninitialized memory.

## Cross-Domain Route

`Hecton8.Lighting.asmdef` references Core/Core.Contracts/Core.Memory and Unity packages only. It does not reference sibling runtime assemblies. Probe-bounce publishing uses `CustomDynamicProbeLightDTO[64]` in Vault buffer `71454`. The culler does not hold a serialized `InteriorGIProbeVolumeRuntime` reference, does not mutate probe memory, and does not complete probe-grid jobs. Gameplay rollback does not consume any SHINOBU_151 DTO.

Hot DTO access in the Burst job file uses `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr`, `NativeArrayUnsafeUtility.GetUnsafePtr`, `UnsafeUtility.AsRef<T>`, and `ref readonly`/`ref` records for source/state/payload/probe/counter lanes. The public ownership route remains Vault handles; raw pointers do not escape the SHINOBU_151 job file.

## Current Route Review Disposition

| Field | Value |
|---|---|
| Route ID | `SHINOBU_151_DYNAMIC_POINT_LIGHT_CULLING` |
| Review disposition | YELLOW / STATIC_SOURCE_ONLY |
| Owner | SHINOBU_151 / Dynamic Point Light Culling |
| Instrument | GlobalDataVault light source/state/sort/GPU/probe buffers, VISUAL_SYNC GPU payload route, SourceManifest `71458`, and black-box dump route |
| Producer phase | VISUAL_SYNC source manifest commit and culling/upload preparation |
| Consumer phase | VISUAL_SYNC GPU upload/readback and probe-grid consumer handoff |
| Consumers | GPU upload path, probe-grid owner through `CustomDynamicProbeLightDTO[64]`, diagnostics |
| Cadence | Continuous quality-weighted visual cadence, roughly 5 Hz under pressure through 60 Hz at high quality |
| Capacity | `DynamicPointLightGpuDTO[64]`, `CustomDynamicProbeLightDTO[64]`, source/state/sort buffers bounded by source capacity, telemetry ring fixed at 300 entries |
| Overflow/failure | Uncommitted manifests publish zero count; unseeded mock-SDF buffers publish zero; top-N survivor selection bounds GPU payload |
| Shutdown/disposal | Vault handles and graphics buffers remain owner-local; no Unity `Light` mutation or probe-grid job completion is performed by this route |
| Fault dump target | `Docs/AgentLogs/Dump_LIGHT_DIRECTOR.bin` is planned/generated on fault; no existing artifact is implied unless a timestamped runtime trigger and output are linked |
| Proof required before GREEN | Fresh compile/import artifact, Burst Inspector proof, Frame Debugger/profiler proof, GC proof, GPU upload proof, and linked output path with command, timestamp, environment, and result |

## Forensics

Blackbox path: `Docs/AgentLogs/Dump_LIGHT_DIRECTOR.bin` planned/generated on fault; no existing artifact is implied unless linked with command, timestamp, environment, trigger, and output.

Telemetry ring is 300 x 64-byte entries and records frame, total lights, culled lights, submitted lights, Burst elapsed estimate, quality, thermal pressure, flags, state hash, max active count, max distance, average intensity, last GPU upload bytes, and Vault generation.

## Verification

Prior static scan text says the owned source avoided forbidden light toggles, managed filtering, DTO properties, `Pack=`, direct sibling imports, `GeometryUtility`, managed `Plane[]`, direct probe injection, sqrt/length distance, and binary quality tier switches. Treat that as STATIC_SOURCE orientation only until a fresh artifact tuple is attached: artifact path, command/tool, timestamp, environment, scanned root, and output. The Vault manifest and `UnsafeUtility.AsRef` raw-access notes are source-orientation only; they are not Burst Inspector, Frame Debugger, GPU upload, runtime, or profiler proof.

Unity import, Burst Inspector, Play Mode profiler, Frame Debugger, and player build proof remain pending until the guarded compile/runtime pass completes.
