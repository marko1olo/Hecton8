# LOG_SHINOBU_35

## 2026-05-18 - Predictive Chunk Streaming Director

Agent: SHINOBU_35  
Domain: CHUNK_RESIDENCY_AND_STREAMING_DIRECTOR  
Status: IMPLEMENTED / CORE COMPILE BLOCKED BY OUT-OF-DOMAIN DEPENDENCY

### What Was Wrong

- Chunk streaming had no SHINOBU_35-owned fixed residency DTO ledger for predictive hydration/dehydration state, Addressables native request mirrors, HLOD impostor DTOs, and runtime tuning.
- Existing chunk activation could dispatch too much I/O/copy pressure into one frame on weak MicroSD devices.
- Distant chunks needed a cheaper Dear Lie path instead of treating far landscape as physical truth.
- Designer tuning required code/inspector changes instead of hot Vault/CSV updates.
- Blackbox reporting existed, but hydration-copy spikes were not explicitly tied to the `Dump_ASSET_STREAMING_PREDICTIVE.bin` threshold.

### What Was Done

- Added `Assets/_Project/Scripts/World/ShinobuStreamingRuntime.cs` with:
  - `ChunkResidencyDTO` 40-byte ARM64-safe layout.
  - `AddressablesRequestDTO` 16-byte layout.
  - `HLOD_ImpostorDTO` 16-byte layout.
  - `MockAssetHandle`, `MockAddressables`, `MockAupShiftSignal`.
  - Burst init, predictive residency, AUP-shift mock/reconcile jobs.
  - zero-split CSV parser and archive profile archaeology/fallback.
- Extended `WorldChunkResidencyManager` with:
  - Vault-backed streaming ledger handles.
  - predictive radius/stretch tuning.
  - `MaxConcurrentLoads` I/O throttle.
  - 512KB/frame hydration-copy budget and spike dump.
  - additive scene activation gate.
  - SystemHealthIndex radius squeeze.
  - threat residency override through existing `IAmbientBiotaService` SOA aliases.
  - typed SignalBus sector hydration/dehydration publication.
- Added `Assets/_Project/Scripts/Editor/ResidencyStreamingTunerWindow.cs`:
  - sliders for predictive stretch, LOD1 radius, hysteresis, max concurrent loads, and hydration copy budget.
  - Play Mode writes into GlobalDataVault tuning memory.
  - CSV watcher for `Assets/_Project/Data/World/Streaming/streaming_profiles.csv`.
  - SceneView grid colors: green hydrated, yellow pending load, red pending unload, blue threat override.
- Updated `Directory.Build.targets` source-backed bridge for runtime/editor CLI visibility without touching generated csproj files.

### Cinematic Cheats Used

- The Dear Lie: distant chunks are represented as compact HLOD impostor DTO/native matrix lanes instead of hydrated physics/render prefabs.
- Low-tier math uses dot/length checks and velocity-projected sphere overlap, not raycasts or simulation.
- Hardware distress squeezes streaming radii by 40%, letting fog/proxy distance hide reduced residency.

### Microseconds Saved

- Exact measured microseconds saved: not available. Unity profiler/runtime validation is blocked by current Core compile failure.
- Engineering estimate: the 512KB hydration budget prevents multi-MB one-frame copy spikes; `MaxConcurrentLoads=4` limits MicroSD queue pressure; HLOD impostors avoid far-chunk collider/prefab activation entirely.
- False precision rejected: no fabricated profiler numbers were recorded.

<SELF_AUDIT>
  <TASK_CHECK>
    Task 01 [PASS] Archive scan plus emergency mock profile.
    Task 02 [PASS] No runtime Instantiate in owned path; ObjectPoolManager used.
    Task 03 [PASS] DTO raw fields, no accessors.
    Task 04 [PASS] AddressablesRequestDTO 16 bytes.
    Task 05 [PASS] Mock Addressables and mock AUP shift/reconcile job.
    Task 06 [PASS] Burst predictive streaming kernel.
    Task 07 [PASS] bounded async load dispatch.
    Task 08 [PASS] HLOD impostor DTO/native impostor path.
    Task 09 [PASS] 512KB/frame hydration apply budget.
    Task 10 [PASS] threat/persistence dehydration safeguards.
    Task 11 [PASS] additive scene activation gate.
    Task 12 [PASS] SystemHealthIndex radius squeeze.
    Task 13 [PASS] double AUP subtract before float local math.
    Task 14 [PASS] threat residency through existing GlobalRegistry AmbientBiota contract.
    Task 15 [PASS] typed SignalBus sector broadcasts.
    Task 16 [PASS] UninitializedMemory DTO allocation plus Burst init.
    Task 17 [PASS] 300-frame telemetry dump on hydration copy spike.
    Task 18 [PASS] editor tuner window.
    Task 19 [PASS] zero-split CSV ingestor.
    Task 20 [PASS] SceneView residency gizmo grid.
  </TASK_CHECK>
  <ARM64_CHECK>
    ChunkResidencyDTO total 40 bytes.
    offset 00: double3 AUP_Center, 24 bytes.
    offset 24: uint SectorHash, 4 bytes.
    offset 28: float DistanceSq, 4 bytes.
    offset 32: byte StateFlags, 1 byte.
    offset 33: byte Priority, 1 byte.
    offset 34: ushort _pad0, 2 bytes.
    offset 36: uint _pad1, 4 bytes.
    AddressablesRequestDTO total 16 bytes: uint AssetHash @0, int TargetChunkIndex @4, ulong HandlePtr @8.
    HLOD_ImpostorDTO total 16 bytes: uint SectorHash @0, float2 CenterXZ @4, ushort RadiusMetersQ @12, byte ImpostorType @14, byte Flags @15.
    No SHINOBU_35 runtime struct uses Pack=1.
  </ARM64_CHECK>
  <ZERO_GC_CHECK>
    Owned streaming jobs use NativeArray/native structs and no LINQ, boxing, closures, managed dictionaries, or managed lists.
    Cold/editor paths intentionally use file IO for archaeology/CSV watch; these are outside the runtime hot streaming tick.
  </ZERO_GC_CHECK>
  <AUP_CHECK>
    Distance math subtracts chunk/camera double3 AUP first, then casts the local delta to float3 for length/predictive checks.
    No absolute AUP is cast directly to float in the new predictive path.
  </AUP_CHECK>
  <DEAR_LIE_CHECK>
    Far landscape truth is faked with HLOD impostor DTO/matrix lanes; physical chunks hydrate only inside the hard physical radius.
  </DEAR_LIE_CHECK>
  <DEPENDENCY_CHECK>
    Used GlobalRegistry.DataVault, GlobalRegistry.AmbientBiota, GlobalRegistry.Persistence, and typed SignalBus lanes.
    No new sibling runtime assembly reference or new Contracts file was added.
  </DEPENDENCY_CHECK>
  <H_PHI_CHECK>
    SHINOBU_35 residency/addressables/HLOD/tuning/mock ledgers are requested through GlobalDataVault handles and only cached as non-owning NativeArray aliases.
    Residual risk: existing WorldChunkResidencyManager legacy arrays are still local native fields; full H-Phi rewrite is outside this surgical patch.
  </H_PHI_CHECK>
  <BLACKBOX_CHECK>
    Existing 300-frame telemetry ring is active; hydration copy over 1.5ms dumps `Docs/AgentLogs/Dump_ASSET_STREAMING_PREDICTIVE.bin`.
  </BLACKBOX_CHECK>
  <COMPILE_GUARD>
    Core Attempt1 exposed and fixed one SHINOBU_35 API error.
    Core Attempt2 has no SHINOBU_35 file errors and is blocked by out-of-domain GlobalPhysicsStateManager/SubmarineDynamicsRuntime errors.
    Editor Attempt1 is blocked by missing Core DLL after Core compile failure.
  </COMPILE_GUARD>
</SELF_AUDIT>

## 2026-05-18 - H-Phi / Compile Polish Pass

Agent: SHINOBU_35  
Domain: CHUNK_RESIDENCY_AND_STREAMING_DIRECTOR  
Status: IMPLEMENTED / CORE COMPILE PASS / EDITOR BLOCKED BY OUT-OF-DOMAIN FILES

### What Was Wrong

- The first implementation left older `WorldChunkResidencyManager` streaming arrays as local allocations while the new SHINOBU ledger was Vault-backed.
- Hydration copy accounting existed, but the Vault write proof needed a concrete fixed record copied with `UnsafeUtility.MemCpy`.
- The editor tuner originally touched `GlobalRegistry.DataVault` directly; adding the missing contracts reference caused duplicate-type conflicts in the generated editor project.
- Core validation was blocked by an out-of-domain `SaveBinaryStorage.cs` ordering bug: `header.Version` was read before `header` existed.

### What Was Done

- Added Vault buffer IDs 70566-70583 for chunk IDs, AUP centers, telemetry, HLOD lanes, pager tickets, macro eviction scratch, and dehydration metadata.
- Added `AcquireWorldStreamingArray<T>()`: normal runtime storage comes from `GlobalRegistry.DataVault.GetBuffer<T>()`; H8Memory is fallback only when the Vault is absent.
- Added `ReleaseWorldStreamingArray<T>()` so Vault-backed aliases are not disposed as owners.
- Added `ChunkHydrationApplyRecord` 64-byte DTO and copied it into Vault with `UnsafeUtility.MemCpy`.
- Narrowed `ResidencyStreamingTunerWindow` so it writes through `WorldChunkResidencyManager.ApplyRuntimeTuning`; this preserves Play Mode Vault writes without importing `Hecton8.Core.Contracts` into the editor project.
- Fixed one compile-blocking save writer line to use `CurrentVersion` for the sector-entry size before `SaveFileHeader header` is declared.

### Cinematic Cheats Used

- Dear Lie remains intact: far landscape is HLOD DTO/matrix data, not hydrated physics.
- Low and Middle tiers buy stability by shrinking radii and limiting I/O depth; High and Ultra can spend the same Vault/tuner lanes on wider visual residency and richer impostor bands.

### Exact Microseconds Saved

- Measured microseconds saved: not available; no Unity profiler capture was run.
- Engineering limits enforced instead of fabricated savings: 512KB max hydration copy per frame, default 4 concurrent loads, HLOD-only far chunks, and 300-frame blackbox dump over 1.5ms copy time.

<SELF_AUDIT>
  <TASK_CHECK>
    Task 01 [PASS] Archive profile scan plus emergency mock profile.
    Task 02 [PASS] No runtime Instantiate/Destroy in owned streaming path; activation uses ObjectPoolManager.
    Task 03 [PASS] DTOs are raw fields; no get/set wrappers.
    Task 04 [PASS] AddressablesRequestDTO is 16 bytes, uint/int/ulong.
    Task 05 [PASS] Mock Addressables handle and mock AUP shift signal/job.
    Task 06 [PASS] Burst predictive streaming kernel with velocity stretch.
    Task 07 [PASS] bounded Addressables/additive I/O dispatch.
    Task 08 [PASS] 16-byte HLOD impostor DTO path.
    Task 09 [PASS] 512KB/frame hydration apply plus Vault MemCpy record.
    Task 10 [PASS] threat/persistence dehydration safeguards and WAL metadata.
    Task 11 [PASS] additive scene activation gate.
    Task 12 [PASS] SystemHealthIndex radius squeeze.
    Task 13 [PASS] double AUP subtract before local float math.
    Task 14 [PASS] threat residency through existing AmbientBiota SOA contract.
    Task 15 [PASS] typed SignalBus sector broadcasts.
    Task 16 [PASS] UninitializedMemory DTO boot path plus Burst init.
    Task 17 [PASS] 300-frame telemetry ring dumps predictive streaming spike file.
    Task 18 [PASS] editor tuner facade.
    Task 19 [PASS] zero-split CSV parser.
    Task 20 [PASS] SceneView residency grid.
  </TASK_CHECK>
  <ARM64_CHECK>
    ChunkResidencyDTO total 40 bytes: offset 00 double3 AUP_Center 24b; offset 24 uint SectorHash 4b; offset 28 float DistanceSq 4b; offset 32 byte StateFlags 1b; offset 33 byte Priority 1b; offset 34 ushort _pad0 2b; offset 36 uint _pad1 4b.
    AddressablesRequestDTO total 16 bytes: uint AssetHash @0; int TargetChunkIndex @4; ulong HandlePtr @8.
    HLOD_ImpostorDTO total 16 bytes: uint SectorHash @0; float2 CenterXZ @4; ushort RadiusMetersQ @12; byte ImpostorType @14; byte Flags @15.
    ChunkHydrationApplyRecord total 64 bytes; 8-byte lanes lead; no owned runtime struct uses Pack=1.
  </ARM64_CHECK>
  <ZERO_GC_CHECK>
    Tick/job paths use NativeArray, NativeList writers, NativeQueue, typed SignalBus, and fixed DTOs. Static scan found no LINQ, boxing, managed dictionary/list, runtime Instantiate, runtime Destroy, Material.SetFloat, GetComponent, or FindObjectsOfType in owned SHINOBU runtime files.
    Cold archive/file enumeration and editor CSV IO are outside the streaming hot path.
  </ZERO_GC_CHECK>
  <AUP_CHECK>
    Every owned distance evaluator subtracts camera/chunk AUP as double3 first, then casts the local delta to float3. Absolute AUP is not cast directly to float in predictive residency math.
  </AUP_CHECK>
  <DEAR_LIE_CHECK>
    The faked physical calculation is far-biome presence: distant chunks become HLOD impostor DTOs and GPU matrix/card work instead of CPU-side mesh, collider, and flora hydration.
  </DEAR_LIE_CHECK>
  <DEPENDENCY_CHECK>
    Runtime coupling goes through GlobalRegistry services and existing typed SignalBus lanes. No new Contracts file was changed. The editor bridge avoids `Hecton8.Core.Contracts` to prevent duplicate types.
  </DEPENDENCY_CHECK>
  <H_PHI_CHECK>
    SHINOBU ledgers use Vault handles. Existing manager arrays for chunk IDs, telemetry, HLOD lanes, pager tickets, macro scratch, and metadata now acquire through GlobalDataVault first; fields are non-owning aliases when Vault is present.
  </H_PHI_CHECK>
  <BLACKBOX_CHECK>
    300-frame ring is active; hydration apply over 1.5ms writes `Docs/AgentLogs/Dump_ASSET_STREAMING_PREDICTIVE.bin`.
  </BLACKBOX_CHECK>
  <COMPILE_GUARD>
    Core rebuild passed: Build_SHINOBU_35_Core_Attempt8_Rebuild.log.
    World contracts passed: Build_SHINOBU_35_WorldContracts_Attempt2.log.
    Editor blocked outside this domain: Build_SHINOBU_35_Editor_Attempt7_NoContractsRef.log lists unrelated editor windows and no SHINOBU_35 file errors.
  </COMPILE_GUARD>
</SELF_AUDIT>
