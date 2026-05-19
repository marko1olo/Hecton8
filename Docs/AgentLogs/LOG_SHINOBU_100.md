# SHINOBU_100 Log

Date: 2026-05-19
Agent: SHINOBU_100
Domain: Echelon 1 Core & Memory Infrastructure / GlobalDataVault sovereignty
Evidence class: STATIC_SOURCE. Unity compile/import/profiler was not run because CPU gate reported 80.3-96.9% and AGENTS forbids build at >50%.

## Batch Pass - Vault Sovereignty Hardening

What was wrong:
- Vehicle, acoustic, fauna, and submarine paths still had authority or staging state outside the GlobalDataVault route.
- AUP rows lacked a 64-bit sector / 32-bit local split job for 5000m sector wrapping.
- Vault telemetry had a 300-frame ABI but did not receive dispatcher generation-miss deltas or measured memory-maintenance cost.
- Vault compaction had no SHINOBU-owned Frost sweep that could swap-pop dead rows and broadcast index mutation.
- The memory CSV override path still needed Vault-owned scratch, live polling, and `stride_aggressiveness` consumption.
- Editor memory visibility existed but had weak runtime proof paths and managed list staging.

What was done:
- Migrated VehicleMotor submarine state and sweep staging to `VaultBufferHandle<T>` (`VehicleMotorSubmarineStates`, `VehicleMotorSweepCommands`, `VehicleMotorSweepResults`).
- Moved SubmarineDynamics local signal queues to typed SignalBus lanes and AcousticEcho pending taps to `AcousticEchoPendingTaps`.
- Added explicit 64B `VaultAupSectorLocal32`, `VaultSovereigntyTelemetryEntry`, `VaultSovereigntyMaintenanceStats`, and expanded 64B `MemoryAddressShiftSignal`.
- Added deterministic Burst `VaultAupPrecisionDeltaCompactionJob` and `VaultOrphanedPointerSweepJob`, both with `[NoAlias]`.
- Routed Frost memory maintenance through `SystemDispatcher.RunPreSimulationMemoryDefrag` and routed POST_SIMULATION telemetry heartbeat through `RunMasterPostSimulationPhase`.
- Added `GenerationHandleMissCount` to `IDataVault`/`GlobalDataVault` and increments on generation-checked handle refresh.
- Added Vault scratch CSV parsing and Frost polling for `memory_overrides.csv`; `stride_aggressiveness` now mathematically shrinks sweep budgets.
- Added editor-only `VaultMemoryGizmoVisualizer` and rebuilt `VaultXRayWindow` on UI Toolkit with fixed-array snapshots.

Cinematic cheats used:
- Distant/throttled submarine updates use deterministic dead reckoning and cubic Hermite presentation interpolation instead of full 6D solve every visual frame.
- Low-quality memory maintenance collapses toward a bounded Frost minimum instead of full-array scans.
- Gizmo visualization reads existing Vault rows and SignalBus snapshots; no debug GameObjects or scene probes.

Exact static microseconds saved:
- Vehicle boot allocator avoidance: 80-150us per active vehicle static estimate.
- Submarine low-quality solve decimation: 10-40us per 16-vehicle batch static estimate.
- Fauna NoAlias/layout vectorization opportunity: 5-25us per 5k fauna batch static estimate.
- AUP sector-local compaction: 6-18us per 1k active rows static cost, paid to avoid per-consumer broad-world double math.
- Frost swap-pop sweep: 8-35us per Frost window static estimate for common active counts.
- Telemetry heartbeat: <2us per record static estimate.

<SELF_AUDIT agent_id="SHINOBU_100" evidence="STATIC_SOURCE">
  <TASK_RECONCILIATION>
    <TASK id="01" status="PASS">Static allocation audit recorded resolved VehicleMotor/Acoustic/Submarine allocations and remaining SHINOBU_37 culling containers in `AllocationAudit_SHINOBU_100.md`.</TASK>
    <TASK id="02" status="PASS">VehicleMotor no longer disposes migrated local native state; handles tombstone and sweep buffers unlock through Vault route.</TASK>
    <TASK id="03" status="PASS">VehicleMotor/Fauna hot DTO access uses raw fields and ref helpers; no hot get/set DTO wrappers were added.</TASK>
    <TASK id="04" status="PASS">Touched authoritative DTOs are explicit layout with sizes divisible by 8/16/32/64; no `Pack=1` in touched memory files.</TASK>
    <TASK id="05" status="PASS">VehicleMotor fallback state uses Vault buffers and deterministic slot seeding, not new owner-local arrays.</TASK>
    <TASK id="06" status="PASS">BufferID range 556-563 added for migrated SHINOBU buffers and scratch.</TASK>
    <TASK id="07" status="PASS">Migrated loops resolve `VaultBufferHandle<T>` views at phase entry and do not cache job pointers across frames.</TASK>
    <TASK id="08" status="PASS">Touched migrated Burst jobs use `[NoAlias]` on NativeArray fields.</TASK>
    <TASK id="09" status="PASS">Submarine integration stride and Vault sweep budget consume continuous `GlobalQualityWeight`.</TASK>
    <TASK id="10" status="PASS">State DTOs use deterministic Burst float mode and explicit byte offsets for rollback memcpy.</TASK>
    <TASK id="11" status="PASS">Vehicle sweep and submarine jobs register active handles; Vault buffer locks guard sweep pointer lifetime.</TASK>
    <TASK id="12" status="PASS">Skipped simulation frames use deterministic dead reckoning/Hermite presentation rather than frozen visuals.</TASK>
    <TASK id="13" status="PASS">`VaultAupPrecisionDeltaCompactionJob` wraps 5000m sector boundaries into 64-bit sectors and 32-bit local offsets.</TASK>
    <TASK id="14" status="PASS">300-entry `VaultSovereigntyTelemetryEntry` ring receives POST_SIMULATION heartbeat and fault dump route.</TASK>
    <TASK id="15" status="PASS">`VaultOrphanedPointerSweepJob` performs O(1) swap-pop on SHINOBU-owned Vault hot/AUP rows. Remaining SHINOBU_37 culling containers are documented cross-domain debt.</TASK>
    <TASK id="16" status="PASS">Target migrated files now have no `GlobalRegistry.DataVault/Get/TryGet`; cached `IDataVault` plus `GlobalDataVault.TryGetLatestCreated` is used.</TASK>
    <TASK id="17" status="PASS">64B `MemoryAddressShiftSignal` carries old/new index and moved entity and is emitted by the sweep job.</TASK>
    <TASK id="18" status="PASS">Vault X-Ray uses UI Toolkit, fixed 256-row snapshot array, heatmap, generation, fragmentation, and quality stride display.</TASK>
    <TASK id="19" status="PASS">CSV override parser streams into Vault byte scratch and hashes ASCII keys; no `ReadAllBytes`, `string.Split`, regex, foreach, `Dictionary<>`, or `new List<>` in the SHINOBU memory parser/facades.</TASK>
    <TASK id="20" status="PASS">`VaultMemoryGizmoVisualizer` reconstructs sector-local editor positions and flashes swap-pop moved rows from SignalBus snapshots.</TASK>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <STRUCT name="VaultAupSectorLocal32" size="64">
      <FIELD offset="0" size="8">long SectorX</FIELD>
      <FIELD offset="8" size="8">long SectorY</FIELD>
      <FIELD offset="16" size="8">long SectorZ</FIELD>
      <FIELD offset="24" size="12">float3 LocalOffset</FIELD>
      <FIELD offset="36" size="4">uint EntityId</FIELD>
      <FIELD offset="40" size="4">uint Flags</FIELD>
      <FIELD offset="44" size="4">uint ShiftFrameId</FIELD>
      <FIELD offset="48" size="8">ulong _pad0</FIELD>
      <FIELD offset="56" size="8">ulong _pad1</FIELD>
      <MATH>8+8+8+12+4+4+4+8+8 = 64 bytes, one L1 cache line.</MATH>
    </STRUCT>
    <STRUCT name="VaultSovereigntyTelemetryEntry" size="64">
      <FIELD offset="0" size="8">long TotalVaultBytes</FIELD>
      <FIELD offset="8" size="8">long ArenaBytes</FIELD>
      <FIELD offset="16" size="4">int ActiveBufferCount</FIELD>
      <FIELD offset="20" size="4">int GenerationMisses</FIELD>
      <FIELD offset="24" size="4">int StrideMultiplier</FIELD>
      <FIELD offset="28" size="4">float MaxMemoryJobUs</FIELD>
      <FIELD offset="32" size="4">uint Frame</FIELD>
      <FIELD offset="36" size="4">uint VaultGenerationId</FIELD>
      <FIELD offset="40" size="4">uint BufferId</FIELD>
      <FIELD offset="44" size="4">uint StateHash</FIELD>
      <FIELD offset="48" size="4">float GlobalQualityWeight</FIELD>
      <FIELD offset="52" size="4">uint Flags</FIELD>
      <FIELD offset="56" size="8">ulong _pad0</FIELD>
      <MATH>16 bytes of 64-bit counters + 40 bytes scalar telemetry + 8 bytes pad = 64 bytes.</MATH>
    </STRUCT>
    <STRUCT name="MemoryAddressShiftSignal" size="64">
      <FIELD offset="0" size="8">long OldPointer</FIELD>
      <FIELD offset="8" size="8">long NewPointer</FIELD>
      <FIELD offset="16" size="4">int BufferId</FIELD>
      <FIELD offset="20" size="4">int ByteLength</FIELD>
      <FIELD offset="24" size="4">uint Version</FIELD>
      <FIELD offset="28" size="1">byte Flags</FIELD>
      <FIELD offset="29" size="1">byte SystemId</FIELD>
      <FIELD offset="30" size="2">ushort _pad0</FIELD>
      <FIELD offset="32" size="4">int OldIndex</FIELD>
      <FIELD offset="36" size="4">int NewIndex</FIELD>
      <FIELD offset="40" size="4">uint MovedEntityId</FIELD>
      <FIELD offset="44" size="4">uint SourceFrame</FIELD>
      <FIELD offset="48" size="4">uint SourceHash</FIELD>
      <FIELD offset="52" size="4">uint CompactedCount</FIELD>
      <FIELD offset="56" size="8">ulong _pad1</FIELD>
      <MATH>Pointer relocation ABI retained; index-mutation tail fills offsets 32-55; pad to 64 bytes.</MATH>
    </STRUCT>
    <STRUCT name="VehicleMotor.SubmarineState" size="112">
      <FIELD offset="0" size="48">AUP sector/local double authority</FIELD>
      <FIELD offset="48" size="16">quaternion RuntimeRotation</FIELD>
      <FIELD offset="64" size="12">float3 RuntimePosition</FIELD>
      <FIELD offset="76" size="12">float3 LinearVelocity</FIELD>
      <FIELD offset="88" size="12">float3 AngularVelocityRadians</FIELD>
      <FIELD offset="100" size="4">uint _pad0</FIELD>
      <FIELD offset="104" size="8">ulong _pad1</FIELD>
      <MATH>48+16+12+12+12+4+8 = 112 bytes, divisible by 16.</MATH>
    </STRUCT>
  </STRUCT_LAYOUT_VERIFICATION>
  <SCALABILITY_CURVE>
    When `GlobalQualityWeight` drops below 0.3, submarine heavy integration trends toward stride 4 and skipped rows use dead reckoning/Hermite presentation. Vault Frost sweep computes `dampedQuality = quality * lerp(1, quality, stride_aggressiveness)` and smoothstep-like `q*q*(3-2q)`, so high `stride_aggressiveness` collapses scanning toward the 64-row minimum while still progressing every Frost window. CSV can tune `stride_aggressiveness` without C# recompilation. No binary low-end branch was added.
  </SCALABILITY_CURVE>
  <H_PHI_VAULT_STATUS>
    <PRIVATE_ARRAY_ALLOCATIONS>SHINOBU-owned migrated VehicleMotor, AcousticEcho, SubmarineDynamics queues, and Vault maintenance paths declare zero private persistent NativeArray/NativeQueue ownership after this pass.</PRIVATE_ARRAY_ALLOCATIONS>
    <KNOWN_EXTERNAL_DEBT>Static scan still finds SHINOBU_37 culling `_physicsSpatialHash`, `_physicsStateChangedIndices`, and `_physicsTargetWakeRequests`; not rewritten in this pass because that is a separate spatial-query contract.</KNOWN_EXTERNAL_DEBT>
    <VAULT_BUFFER_HANDLES>550 VaultMemoryLayoutConfig; 551 VaultHotEntityData; 552 VaultColdEntityData; 553 VaultAup64; 556 VehicleMotorSubmarineStates; 557 VehicleMotorSweepCommands; 558 VehicleMotorSweepResults; 559 VaultSovereigntyTelemetryRing; 560 AcousticEchoPendingTaps; 561 VaultAupSectorLocal32; 562 VaultSovereigntyActiveEntityCount; 563 VaultMemoryProfileCsvScratch.</VAULT_BUFFER_HANDLES>
  </H_PHI_VAULT_STATUS>
  <POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
    <NO_ALIAS>Fauna parasite jobs, Acoustic Echo tracking, Submarine integrator, AUP compaction, and orphan sweep use `[NoAlias]` on migrated NativeArray fields.</NO_ALIAS>
    <JOB_GRAPH>PRE_SIMULATION: `GlobalDataVault.FrostTickDefrag` then `VaultAupPrecisionDeltaCompactionJob` then `VaultOrphanedPointerSweepJob`; final handle is registered as `SystemID.CoreDataVault` and completed inside bounded Frost maintenance before indices are published. Vehicle sweep and Submarine integration register their scheduled handles with `H8Memory.RegisterActiveJob`.</JOB_GRAPH>
    <SIGNAL_GRAPH>Swap-pop index moves enqueue `SignalBus<MemoryAddressShiftSignal>.ParallelWriter`; downstream presentation reads the lane snapshot, not the memory manager.</SIGNAL_GRAPH>
  </POINTER_ALIASING_AND_DEPENDENCY_GRAPH>
  <COMPILE_GUARD>
    No asmdef references were edited in this pass. SHINOBU runtime changes rely on `Hecton8.Core`, `Hecton8.Core.Memory`, `Hecton8.Core.Contracts`, GlobalDataVault, and SignalBus. No new sibling runtime assembly reference was introduced.
  </COMPILE_GUARD>
  <THE_DEAR_LIE_CONFIRMATION>
    Heavy per-frame kinematic solving is avoided on skipped quality-stride frames by deterministic dead reckoning and Hermite visual interpolation over local float3 space. Before: full heavy update O(N) every simulation frame plus visible freeze risk under decimation. After: O(N/stride) heavy updates plus O(N) cheap presentation prediction, with low-quality stride approaching 4. For memory maintenance, before was full O(N) sweep pressure; after is O(min(N, quality_budget)) on Frost cadence plus O(1) swap-pop per tombstone.
  </THE_DEAR_LIE_CONFIRMATION>
  <VERIFICATION>
    <STATIC_CHECK>Target files: no `Pack=1` or sequential layouts in touched memory/vehicle/fauna/submarine/acoustic DTO files.</STATIC_CHECK>
    <STATIC_CHECK>Target Burst jobs: no missing `CompileSynchronously = true` in touched Burst job files.</STATIC_CHECK>
    <STATIC_CHECK>SHINOBU memory parser/facades/gizmo: no `ReadAllBytes`, `ReadAllText`, `string.Split`, regex, foreach, `Dictionary<>`, or `new List<>`.</STATIC_CHECK>
    <STATIC_CHECK>Target migrated files: no `GlobalRegistry.DataVault`, `GlobalRegistry.Get`, or `GlobalRegistry.TryGet` remains.</STATIC_CHECK>
    <BUILD>Not run. CPU gate measured 80.3-96.9%; no `dotnet`/`csc` process was active, but AGENTS forbids launching build above 50% CPU.</BUILD>
  </VERIFICATION>
</SELF_AUDIT>

<POLISH_AUDIT agent_id="SHINOBU_100" pass="compile_wall_and_buffer_abi" date="2026-05-19">
  <WHAT_WAS_WRONG>
    The Frost sweep had an illegal reverse dependency: `Hecton8.Core.Memory` attempted to write `SignalBus<MemoryAddressShiftSignal>`, but `SignalBus` is compiled in `Hecton8.Core` and `Hecton8.Core` already references `Hecton8.Core.Memory`. That route creates a compile-wall/circular-reference failure. The late SHINOBU BufferIDs also collided with existing WristHud values 560-565.
  </WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>
    Added 64-byte `VaultMemoryAddressShiftRecord` and `VaultMemoryAddressShiftCount` as Vault-owned buffers. `VaultOrphanedPointerSweepJob` now writes records only; `SystemDispatcher` publishes the Core `MemoryAddressShiftSignal` after Frost maintenance from the legal Core boundary. `VaultXRayWindow` moved to the Editor asmdef and `VaultMemoryGizmoVisualizer` moved to Core diagnostics so editor/debug code can reference Core-only symbols without contaminating Core.Memory.
  </WHAT_WAS_DONE>
  <BUFFER_ABI>
    SHINOBU active Vault IDs are now: 550 VaultMemoryLayoutConfig; 551 VaultHotEntityData; 552 VaultColdEntityData; 553 VaultAup64; 554 VaultEntityBucketMap; 555 VaultSharedTransformMatrices; 556 VehicleMotorSubmarineStates; 557 VehicleMotorSweepCommands; 558 VehicleMotorSweepResults; 559 VaultSovereigntyTelemetryRing; 636 AcousticEchoPendingTaps; 637 VaultAupSectorLocal32; 638 VaultSovereigntyActiveEntityCount; 639 VaultMemoryProfileCsvScratch; 640 VaultMemoryAddressShiftRecords; 641 VaultMemoryAddressShiftCount.
  </BUFFER_ABI>
  <STRUCT_LAYOUT_VERIFICATION>
    `VaultMemoryAddressShiftRecord` is 64 bytes: offsets 0/8 two long pointers, 16/20 int BufferId/ByteLength, 24 uint Version, 28 byte Flags, 29 byte SystemId, 30 ushort pad, 32/36 int OldIndex/NewIndex, 40 uint MovedEntityId, 44 uint SourceFrame, 48 uint SourceHash, 52 uint CompactedCount, 56 ulong pad. Total = 64 bytes, one cache line.
  </STRUCT_LAYOUT_VERIFICATION>
  <CINEMATIC_CHEAT>
    No new physical simulation was introduced. The existing Dear Lie remains deterministic dead-reckoning/Hermite presentation on skipped quality-stride frames while authoritative compaction is O(min(N, quality_budget)) on Frost cadence plus O(1) swap-pop.
  </CINEMATIC_CHEAT>
  <EXACT_MICROSECONDS_SAVED>
    Direct measured savings are not claimed without Unity Profiler/Burst proof. Static estimate unchanged: avoiding heavy submarine solve on stride-4 low quality saves 10-40us per 16-vehicle batch; Frost sweep remains 8-35us per bounded window. The compile-wall fix saves developer iteration failure time, not frame time.
  </EXACT_MICROSECONDS_SAVED>
  <VERIFICATION>
    Core.Memory static scan now finds no `SignalBus<MemoryAddressShiftSignal>`, no Core `MemoryAddressShiftSignal`, no `HectonFloatingOrigin`, and no `HomeostasisBrain`. The remaining `MockSignalBus<T>` is editor/test gated and does not reference Core. Touched-file `git diff --check` reports only LF/CRLF warnings. Full `git diff --check` is blocked by unrelated trailing whitespace in `Docs/AgentLogs/Rationale_SHINOBU_113.md`.
    BufferID duplicate scan no longer reports SHINOBU_100/WristHud collisions. Remaining duplicate values are external: 70150-70157 VRSomatic/QuestDag and 70200 SaveWorldPager/ConstructionBuilder.
  </VERIFICATION>
  <BUILD>
    Not run. CPU gate measured 100% and no `dotnet`/`csc` process; AGENTS forbids launching build above 50% CPU.
  </BUILD>
</POLISH_AUDIT>

<POLISH_AUDIT agent_id="SHINOBU_100" pass="core_memory_explicit_abi_closure" date="2026-05-19">
  <WHAT_WAS_WRONG>
    A targeted ABI scan still found non-generic Core.Memory and dispatcher DTOs using `LayoutKind.Sequential`. Sequential layout was size-asserted, but it still permits future offset drift when fields are inserted. The dispatcher also retained two `GlobalRegistry.DataVault` fallbacks in the memory cache path, making the SHINOBU route harder to prove as cached-only.
  </WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>
    Converted non-generic Core.Memory records to explicit layout without changing byte sizes: `BlockDescriptor` 40B, `H8AllocationRecord` 48B, `H8MemoryTelemetryEntry` 64B, `VaultRelocationRecord` 32B, `VaultBufferMeta` 48B, `VaultMemoryBlockSnapshot` 48B, `VaultArenaBlock` 32B, and `MemoryDefragTelemetryEntry` 128B. Converted touched dispatcher DTOs `CriticalMemoryPressureEvent` 32B and `DispatcherBlackBoxEntry` 64B. Replaced `SystemDispatcher` `GlobalRegistry.DataVault` fallback with cached `_dataVault` plus `GlobalDataVault.TryGetLatestCreated`. Added the missing `.meta` for the moved diagnostics `VaultMemoryGizmoVisualizer.cs`.
  </WHAT_WAS_DONE>
  <STRUCT_LAYOUT_VERIFICATION>
    `BlockDescriptor`: IntPtr 0, long Offset 8, long Bytes 16, int OwnerKey 24, int Generation 28, ushort SystemID 32, ushort Flags 34, byte State 36, byte Reserved 37, ushort Reserved2 38 = 40B. `H8AllocationRecord`: pointer/long at 0/8, ints at 16/20/24/28/32/36, ushort fields at 40/42/44/46 = 48B. `H8MemoryTelemetryEntry`: longs 0/8/16, uint 24, ints 28/32/36/40/44/48/52, ushorts 56/58, uint 60 = 64B.
  </STRUCT_LAYOUT_VERIFICATION>
  <GENERIC_HANDLE_EXCEPTION>
    `VaultBufferHandle<T>` and `VaultBufferSlice<T>` remain sequential by design. Explicit layout on generic value types is a CLR/IL2CPP compile-wall risk; both types are handle/view metadata, not authoritative hot DTO rows, and `VaultSurgeryEditTests` plus runtime guards assert exact 24B/32B sizes.
  </GENERIC_HANDLE_EXCEPTION>
  <EXTERNAL_DEBT_NOT_HIJACKED>
    Broad scan still finds `Pack=1` / non-synchronous Burst debt in separate AI ownership lanes: `AI/Pathfinding/PathFunnelContracts.cs` and `AI/Ambient/AmbientBiotaDirector.cs`. SHINOBU_100 did not rewrite those binary contracts in this pass.
  </EXTERNAL_DEBT_NOT_HIJACKED>
  <CINEMATIC_CHEAT>
    No physical simulation was added. The Dear Lie remains dead-reckoning/Hermite presentation under quality striding plus Vault Frost O(1) swap-pop compaction.
  </CINEMATIC_CHEAT>
  <EXACT_MICROSECONDS_SAVED>
    Measured frame savings are not claimed. Static value: removes service-locator fallback from the dispatcher memory route and freezes ARM64 offsets; expected runtime difference is below profiler significance until Unity/Burst proof exists.
  </EXACT_MICROSECONDS_SAVED>
  <VERIFICATION>
    Static scans after this pass: no direct `GlobalRegistry.DataVault/Get/TryGet` in SHINOBU-touched memory/physics route; no `Allocator.Persistent` matches in `Assets/_Project/Scripts/AI` or `Assets/_Project/Scripts/Physics`; touched Burst scan clean; touched-file `git diff --check` reports only LF/CRLF warnings. SHINOBU IDs 636-641 and 70630-70635 have no source collisions outside their enum declarations. Broad enum duplicate scan reports external collisions at 70200, 70550, and 70800-70807.
  </VERIFICATION>
  <BUILD>
    Not run. CPU gate measured 97.9%; AGENTS/user rules forbid `dotnet build` above 50% CPU.
  </BUILD>
</POLISH_AUDIT>

<POLISH_AUDIT agent_id="SHINOBU_100" pass="physics_culling_vault_closure" date="2026-05-19">
  <WHAT_WAS_WRONG>
    A fresh static scan still showed three persistent owner-local physics culling containers: `_physicsSpatialHash` as `NativeParallelMultiHashMap<int,int>`, `_physicsStateChangedIndices` as `NativeQueue<int>`, and `_physicsTargetWakeRequests` as `NativeQueue<PhysicsCullingTargetWakeRequestSignal>`. The prior report marked this as SHINOBU_37 debt; after reading the actual partial, the migration was feasible without inventing a cross-assembly dependency.
  </WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>
    Replaced the spatial hash with Vault SoA buffers: `ShinobuPhysicsCullingSpatialBucketHeads` (70630), `ShinobuPhysicsCullingSpatialNext` (70631), and `ShinobuPhysicsCullingSpatialCellHashes` (70632). Replaced changed-index queue with `ShinobuPhysicsCullingChangedIndices` (70633) plus 64B `PhysicsCullingCounter64` at `ShinobuPhysicsCullingChangedCount` (70634). Replaced targeted wake queue with Vault wake mirror plus 64B `ShinobuPhysicsCullingWakeRequestCount` (70635). The Burst culling jobs now write changed indices through atomic counter increments and `[NoAlias]` arrays.
  </WHAT_WAS_DONE>
  <BUFFER_ABI>
    New physics culling IDs intentionally use 70630-70635. The first candidate range 70609-70614 was rejected after source/doc scan found `ShinobuApexBrainVault` owns 70609-70619 and 70626-70629 through casted BufferID constants.
  </BUFFER_ABI>
  <STRUCT_LAYOUT_VERIFICATION>
    `PhysicsCullingCounter64` is exactly 64 bytes: offset 0 int Value, offset 4 uint Flags, offsets 8/16/24/32/40/48/56 seven ulong pads. Total = 4 + 4 + 56 = 64 bytes, one L1 cache line for atomic producer counters. `PhysicsCullingDTO` remains 40 bytes: double3 at 0, int at 24, float at 28, byte at 32, three byte pads 33-35, uint flags at 36.
  </STRUCT_LAYOUT_VERIFICATION>
  <CINEMATIC_CHEAT>
    No rigidbody micro-simulation was added. The culling path remains an optical/physics budget cheat: far bodies are slept, made kinematic, or stripped of heavy mesh colliders by distance/frustum math instead of simulating every body at full fidelity. Quality now expands/shrinks radii continuously through `HomeostasisBrain.GlobalQualityWeight`, `math.lerp`, polynomial smoothing, and `math.step`.
  </CINEMATIC_CHEAT>
  <EXACT_MICROSECONDS_SAVED>
    Measured savings are not claimed without Unity Profiler/Burst proof. Static impact: removes three persistent native container allocations from the physics culling partial; spatial rebuild remains O(N), nine-cell query remains O(C) with exact cell-hash checks; changed-index production is O(1) atomic write. Expected low-end gain is lower allocator fragmentation and no native queue/multihash container maintenance overhead.
  </EXACT_MICROSECONDS_SAVED>
  <VERIFICATION>
    Static target scan: no `NativeQueue`, `NativeParallelMultiHashMap`, or `Allocator.Persistent` remains in `GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs`. Target Burst scan: all BurstCompile attributes in the touched culling files include `CompileSynchronously = true`; culling jobs use deterministic float mode. BufferID duplicate enum scan reports only existing external `70200 SaveWorldPagerWriteArena/ConstructionBuilderOccupancy`.
  </VERIFICATION>
  <BUILD>
    Not run. Build remains gated by user/AGENTS CPU and active-dotnet rules until the machine is below 50% CPU and no `dotnet`/`csc` process is active.
  </BUILD>
</POLISH_AUDIT>

<POLISH_AUDIT agent_id="SHINOBU_100" pass="physics_culling_scratch_vault_closure" date="2026-05-19">
  <WHAT_WAS_WRONG>
    After the native queue and multihash migration, the physics culling partial still retained local managed scratch fields: two byte arrays for CSV/binary hydration and one nine-cell `int3[]` neighbor scratch. These were not persistent native allocations, but they weakened the source-level DataVault proof for the physics culling configuration path.
  </WHAT_WAS_WRONG>
  <WHAT_WAS_DONE>
    Added `ShinobuPhysicsCullingCsvScratch` (70636) and `ShinobuPhysicsCullingLegacyRadiiScratch` (70637) BufferIDs. Replaced managed file-read scratch with Vault-backed `NativeArray<byte>` spans and `ReadOnlySpan<byte>` parsing. Removed the 9-cell managed neighbor array by computing the 3x3 candidate cell hashes directly during spatial query. Kept the existing `Plane[6]` frustum scratch because Unity's frustum API requires that managed array and it is not authoritative memory.
  </WHAT_WAS_DONE>
  <BUFFER_ABI>
    `ShinobuPhysicsCullingCsvScratch` 70636: byte, capacity 4096. `ShinobuPhysicsCullingLegacyRadiiScratch` 70637: byte, capacity 64. Both are byte-addressable scratch buffers and do not define DTO padding beyond the one-byte element stride.
  </BUFFER_ABI>
  <CINEMATIC_CHEAT>
    No simulation was added. The physics culling Dear Lie remains distance/frustum sleeping and collider stripping, with direct 3x3 cell hash queries replacing a staged neighbor-cell array.
  </CINEMATIC_CHEAT>
  <EXACT_MICROSECONDS_SAVED>
    Measured microseconds are not claimed. Static impact: removes two managed scratch arrays and one nine-element managed neighbor array from the culling partial; file hydration now writes directly into Vault scratch. Expected hot-path gain is below profiler resolution, but the architecture proof is stronger.
  </EXACT_MICROSECONDS_SAVED>
  <VERIFICATION>
    Static scans: no `NativeQueue`, `NativeParallelMultiHashMap`, `Allocator.Persistent`, private byte-array scratch, or private int3-array scratch remains in `GlobalPhysicsStateManager.Shinobu37PhysicsCulling.cs`; no `GlobalRegistry.DataVault/Get/TryGet` remains in the SHINOBU-touched route; touched Burst scan is clean; 70636 and 70637 have no source collisions outside their enum declarations. `git diff --check` reports only LF/CRLF warnings on touched files.
  </VERIFICATION>
  <BUILD>
    Not run. CPU measured 18.0%, but seven `dotnet` processes were active; AGENTS/user rules forbid launching `dotnet build` while `dotnet` or `csc` is already running.
  </BUILD>
</POLISH_AUDIT>
