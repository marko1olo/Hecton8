# LOG_SHINOBU_204

## 2026-05-20 Session Start

What was wrong: Alignment ownership was not yet established for this batch. No SHINOBU_204 status or rationale file existed.

What was done: Created status, rationale, and log files. Extracted the XML assignment from Docs/Tasks/CURRENT_BATCH.md and identified 20 tasks.

Cinematic Cheats used: None. This is memory ABI work, not physical simulation.

Exact Microseconds saved: 0 us verified. Static planning only.

Verification: PENDING. No compile, Unity import, Burst Inspector, or runtime profiler proof yet.

## 2026-05-20 Alignment Pass - Partial Runtime ABI Cleanup

What was wrong:
Pack=1 was present on runtime DTOs and Burst-adjacent payloads. The highest-risk hits were fluid NativeArray/GPU structs, physics force/event payloads, fauna LOD proxy data, loot magnet signal data, proxy light upload data, and VFX compute budget records. Physics event structs also hid ABI fields behind readonly auto-properties. Several explicit layouts still carried a meaningless Pack=1 parameter. Global scan still reports 91 runtime StructLayout Pack=1 hits outside this pass.

What was done:
Converted targeted DTOs to explicit layouts with fixed 16/32/64/128-byte sizes and FieldOffset on every field. Padded ForcePacket to 64 bytes, pressure/removed physics payloads to 128, acoustic/large acoustic payloads to 64, BuoyancyParams to 128, fluid GPU/telemetry payloads to 32/64, LootMagnetSignalEvent to 128, ProxyLightData to 128, FaunaTier1LodProxyEntry to 64, InteractionPacket to 64, InteractionSignal to 128, and VfxComputeParticleBudget to 32.

What was done:
Packed FaunaTier1LodProxyEntry's Flags, HeadingOctant, Health01, Hunger01, and QualityTier into one uint StatusFlags with static bit helpers. Rejected properties on physics DTOs and exposed raw fields. Removed Pack=1 from already-explicit runtime structs in vehicle docking, tentacle IK, player state, loot contracts, repair black box, and biolum manager.

What was done:
Added NoAlias to Physics ValidateForcePacketsJob and HectonFluid WaveQueryJob, BuoyancyJob, and InteriorFloodBfsJob NativeArray fields. Unity job structs containing NativeArray handles were left Sequential after Pack=1 removal because the handle ABI is Unity-owned and not a DTO element layout.

What was done:
Added AlignmentTelemetryEntry, Arm64AlignmentTelemetry 300-entry Vault ring recorder, BufferID.Arm64AlignmentTelemetryRing = 642, BufferID.Arm64AlignmentTelemetryCursor = 643, and Dump_SHINOBU_204.bin writer. Added Arm64AlignmentFaultGizmo for scene-view fault visualization.

What was done:
Added Memory Alignment X-Ray editor window and strict CLI report method. It scans ISignal, BinaryBlittableSafe, DTO, payload, and telemetry structs in editor only, uses UnsafeUtility.GetFieldOffset, writes Docs/Reports/ARM64_ALIGNMENT_XRAY_REPORT.txt, and provides GenerateMockLayoutStressTest with a deliberately misaligned double mock plus corrected Burst job proof.

Cinematic Cheats used:
No physical simulation was replaced. The only cheat applied was memory-side: byte flags collapsed into a uint StatusFlags mask, which buys cache density without changing gameplay truth.

Exact Microseconds saved:
Static estimate only, no profiler proof because compile/build was blocked by CPU gate. Force/physics packet quantization: 4-60 us per 10k queued packets under contention. Fluid DTO quantization and NoAlias: 2-25 us per scheduled batch. Fauna LOD bit packing: 1-4 us per 10k proxy reads. AUP/64-bit alignment crash-prevention class: 2-20 us per 10k 64-bit loads when avoiding split reads. Runtime validator cost: 0.00 us because reflection is editor-only.

Verification:
git diff --check passed for touched files. Targeted rg found no Pack=1 in HectonFluidEngine, PhysicsApplySystem, FaunaTier1LodProxyRegistry, EquipmentInteractionContracts, ProxyLightRegistry, VfxComputeParticleBudgetCatalog, AlignmentTelemetryContracts, or LootMagnetContracts. dotnet/Unity compile was not run: no dotnet/csc process was active, but CPU was 56.7% then 92.4%, above the explicit 50% build-launch ceiling.

Remaining debt:
91 runtime StructLayout Pack=1 hits remain outside touched scope, concentrated in animation IK, procedural crab IK, gameplay/mining DTOs, Sargassum/world registries, DataArchaeology, ItemTemplateRegistry, HazardZoneManager, GroundRadarJobs, CameraJuiceSystem, MaterialDecayRuntime, and persistent world records. Task 18 auto-AST rewrite is not complete; strict scanner exists, but regex auto-fixing was rejected because it would corrupt ABI on non-trivial Sequential structs.

<SELF_AUDIT>
AgentID: SHINOBU_204
RuntimeReflection: false
RuntimeGCFromValidation: 0 bytes/frame
EditorReflectionOnly: Arm64MemoryAlignmentXRayWindow
VaultBufferIDs: Arm64AlignmentTelemetryRing=642, Arm64AlignmentTelemetryCursor=643
CriticalLayout_ForcePacket: Size=64; Force@0; Torque@12; PointOffset@24; Mode@36; RigidbodyIndex@40; Flags@44; Priority@45; padding@46/48/56.
CriticalLayout_BuoyancyParams: Size=128; boundsCenter@0; boundsExtents@12; scalar block@24-60; flag bytes@72-75; alignmentPadding@76; padding@80/88/96/104/112/120.
CriticalLayout_LootMagnetSignalEvent: Size=128; PositionAup@0; Velocity@48; ItemHash@60; Quantity@64; DistanceSq@68; Frame@72; Flags@76; padding@80-120.
CriticalLayout_FaunaTier1LodProxyEntry: Size=64; PositionAup@0; InstanceUid@48; SpeciesId@52; StatusFlags@56; Reserved1@60.
BuildVerification: NOT_RUN_CPU_GATE
</SELF_AUDIT>

## 2026-05-20 Alignment Pass - Core Intrinsic/Arena DTO Sweep

What was wrong:
- Core still had owner-safe Sequential DTOs in culling primitives, bitmask lanes, logistics pipe spline metadata, native allocation snapshots, and arena allocation return records.
- `ArenaAllocation` was 24 bytes, so repeated allocator metadata walks phase-shifted across 64-byte cache lines.
- `NativeBitmask256.IsEmpty` and `NativeArenaSlice<T>.IsCreated` were DTO properties, not raw-field/method-style access.

What was done:
- Converted `HectonAabb` to Explicit Size=32: `Min@0`, `Max@12`, `Reserved@24`.
- Converted `HectonSphere` to Explicit Size=16: `Center@0`, `Radius@12`.
- Converted `NativeBitmask256` to Explicit Size=32: four aligned `ulong` words at 0/8/16/24.
- Converted `SplineDescriptor` to Explicit Size=64 with four `float3` lanes at 0/12/24/36, scalar lanes at 48/52/56, `Flags@60`, and byte pads 61..63.
- Converted `NativeAllocationSnapshotSource` to Explicit Size=32 with pointer/byte count at 0/8 and compact metadata at 16..31.
- Converted `ArenaAllocation` from Sequential Size=24 to Explicit Size=32 with `_pad0@24`.
- Kept `NativeArenaSlice<T>` Sequential Size=32 as a generic-layout TypeLoad exception; it remains pointer-first and named-padded.

Cinematic Cheats used:
- No simulation was added. This is the data-side cheap path: fixed culling/allocator payloads keep CPU truth compact so higher tiers can spend cycles on richer culling diagnostics and render presentation lanes.

Exact Microseconds saved:
- Static estimate only: 1-8 us per 10k spatial/bitmask/arena metadata reads, plus reduced split-line/cache-phase risk from removing the 24-byte arena allocation stride.

Verification:
- `StructLayout(...Pack=...)` scan under `Assets/_Project/Scripts` returned 0 hits.
- Targeted touched-file `StructLayout(LayoutKind.Sequential` scan now reports only `NativeArenaSlice<T>` and two managed sentinel registry records; `HectonSpatialIntrinsics`, `NativeBitmask256`, and `LogisticsPipeBuilder` report 0 Sequential hits.
- `git diff --check` passed for the five touched Core files with CRLF warnings only.
- `dotnet/csc` process scan returned no active compiler process. Build was not launched because the known dependency wall remains and the user explicitly prohibited rebuild until needed.

Remaining debt:
- Broad non-Pack Sequential inquisition remains owner-gated for Unity job wrappers, GPU/HLSL interop, replay/file ABI, generic wrappers, and domain-owned records.
- Task 20 remains pending Unity/Burst/import proof.

<SELF_AUDIT>
  <TASK_02_SEQUENTIAL_LAYOUT_INQUISITION status="PARTIAL">
    <NewCoverage>HectonAabb=32; HectonSphere=16; NativeBitmask256=32; SplineDescriptor=64; NativeAllocationSnapshotSource=32; ArenaAllocation=32.</NewCoverage>
    <Exception>NativeArenaSlice&lt;T&gt; stayed Sequential Size=32 because generic explicit layout is a TypeLoad/IL2CPP risk; field order remains pointer-first and 8-byte padded.</Exception>
  </TASK_02_SEQUENTIAL_LAYOUT_INQUISITION>
  <STRUCT_LAYOUT_VERIFICATION>
    <HectonAabb size="32">Min@0:float3 bytes 0..11; Max@12:float3 bytes 12..23; Reserved@24:float2 bytes 24..31.</HectonAabb>
    <HectonSphere size="16">Center@0:float3 bytes 0..11; Radius@12:float bytes 12..15.</HectonSphere>
    <NativeBitmask256 size="32">Word0@0:ulong; Word1@8:ulong; Word2@16:ulong; Word3@24:ulong.</NativeBitmask256>
    <SplineDescriptor size="64">Start@0; End@12; StartForward@24; EndForward@36; Radius@48; RuptureStartTimeSeconds@52; FlowScalar@56; Flags@60; pads@61..63.</SplineDescriptor>
    <NativeAllocationSnapshotSource size="32">Pointer@0:ulong; Bytes@8:long; OwnerHash@16:uint; LabelHash@20:uint; AllocationFrame@24:int; Lifetime@28:byte; Allocator@29:byte; Reserved@30:ushort.</NativeAllocationSnapshotSource>
    <ArenaAllocation size="32">Ptr@0:pointer; ByteCount@8:int; ArenaIndex@12:int; SlabIndex@16:int; FrameSequence@20:int; _pad0@24:long.</ArenaAllocation>
  </STRUCT_LAYOUT_VERIFICATION>
  <COMPILE_GUARD>No sibling assembly dependency added. No rebuild launched.</COMPILE_GUARD>
</SELF_AUDIT>

## 2026-05-20 Alignment Pass - Acoustic AUP And Object Batch Explicit Layout

What was wrong:
- `AcousticAup`, `ObjectBatchInstance`, and `ObjectBatchChunk` still used Sequential layout.
- These are shared contracts: audio parent DTOs embed `AcousticAup`, and object batch validators assert the current authoring payload sizes.

What was done:
- Converted `AcousticAup` to Explicit Size=40: `GridX@0`, `GridY@8`, `GridZ@16`, `Local@24`, `_pad0@36`.
- Converted `ObjectBatchInstance` to Explicit Size=80: `LocalToWorld@0`, `AssetHash@64`, `Flags@68`, `MeshIndex@72`, `MaterialIndex@76`.
- Converted `ObjectBatchChunk` to Explicit Size=40: `Bounds@0`, `StartIndex@24`, `Count@28`, `ChunkHash@32`, `LodLevel@36`, tail pads 37..39.

Cinematic Cheats used:
- No new render simulation. Object batches remain BRG-compatible baked payloads instead of GameObject scans; this pass only pins their byte map.

Exact Microseconds saved:
- Static estimate: 0-8 us per 10k object-batch/audio AUP metadata reads. Main gain is ABI determinism and preventing future compiler layout drift.

Verification:
- Touched-file scan for `StructLayout(LayoutKind.Sequential` across AcousticAup and ObjectBatchBase returned 0 hits.
- Global `StructLayout(...Pack=...)` scan under `Assets/_Project/Scripts` returned 0 hits.
- `git diff --check` for the touched audio/render contract files passed with CRLF warnings only.
- Build was not relaunched; previous scoped Core build is still blocked by unrelated dependency-wall errors and the active instruction forbids rebuild until needed.

Remaining debt:
- `AcousticAup` remains 40 bytes and object-batch rows remain 80/40 bytes. They are explicit now, but cache-line resizing requires coordinated audio/render-owner parent DTO and serialized-payload migration.

<SELF_AUDIT>
  <TASK_02_SEQUENTIAL_LAYOUT_INQUISITION status="PARTIAL_PROGRESS">
    <NewCoverage>AcousticAup=40; ObjectBatchInstance=80; ObjectBatchChunk=40.</NewCoverage>
    <RejectedScope>Audio parent DTO and serialized object-batch ABI resizing.</RejectedScope>
  </TASK_02_SEQUENTIAL_LAYOUT_INQUISITION>
  <STRUCT_LAYOUT_VERIFICATION>
    <AcousticAup size="40">GridX@0:long; GridY@8:long; GridZ@16:long; Local@24:float3 bytes 24..35; _pad0@36:uint.</AcousticAup>
    <ObjectBatchInstance size="80">LocalToWorld@0:Matrix4x4 bytes 0..63; AssetHash@64; Flags@68; MeshIndex@72; MaterialIndex@76.</ObjectBatchInstance>
    <ObjectBatchChunk size="40">Bounds@0:24 bytes; StartIndex@24; Count@28; ChunkHash@32; LodLevel@36; pads@37..39.</ObjectBatchChunk>
  </STRUCT_LAYOUT_VERIFICATION>
  <COMPILE_GUARD>No new sibling runtime assembly dependency added. Build not relaunched; previous dependency wall remains the controlling compile fact.</COMPILE_GUARD>
</SELF_AUDIT>

## 2026-05-20 Alignment Pass - Core Runtime Snapshot DTO Sweep

What was wrong:
- Brine, power, UI, and player runtime snapshot DTOs still used Sequential layout.
- `UIValueSlot` was a 24-byte NativeArray row and `PlayerSurvivalRuntimeState` was an 88-byte player truth snapshot.

What was done:
- Converted `BrineLayerSample=32`, `BatteryRuntimeSnapshot=16`, `UIStateData=32`, `UIValueSlot=32`, `PlayerMovementRuntimeState=128`, `PlayerLookState=32`, `PlayerSurvivalRuntimeState=128`, and `PlayerInteractionRuntimeState=32` to explicit layouts.
- Kept `PlayerMovementRuntimeState.PredictedAup` aligned at offset 24; the embedded 48-byte AUP occupies 24..71 and the following float3 starts at 72.
- Added named tail padding to `UIValueSlot` and `PlayerSurvivalRuntimeState`.

Cinematic Cheats used:
- No new physical simulation. The player and UI truth snapshots remain scalar feeds that let visual systems fake HUD stress, brine density, oxygen, power, and radiation presentation without additional per-frame object graphs.

Exact Microseconds saved:
- Static estimate: 1-12 us per 10k UI value/player snapshot reads. The larger benefit is deterministic ABI and fewer split-line copies in native UI/player snapshot fan-out. No profiler proof.

Verification:
- Touched-file scan for `StructLayout(LayoutKind.Sequential` across BrineLayerSample, PowerGridRuntimeService, UIStateStore, and PlayerRuntimeContext returned 0 hits.
- Global `StructLayout(...Pack=...)` scan under `Assets/_Project/Scripts` returned 0 hits.
- `git diff --check` for the touched snapshot files passed with CRLF warnings only.
- Build was not relaunched; previous scoped Core build is still blocked by unrelated dependency-wall errors and the active instruction forbids rebuild until needed.

Remaining debt:
- `AcousticAup` remains a 40-byte audio contract because audio-side parent structs depend on that size. That needs coordinated audio-owner migration, not a blind Core edit.
- Replay/prologue/file-format records remain owner-gated.

<SELF_AUDIT>
  <TASK_02_SEQUENTIAL_LAYOUT_INQUISITION status="PARTIAL_PROGRESS">
    <NewCoverage>BrineLayerSample=32; BatteryRuntimeSnapshot=16; UIStateData=32; UIValueSlot=32; PlayerMovementRuntimeState=128; PlayerLookState=32; PlayerSurvivalRuntimeState=128; PlayerInteractionRuntimeState=32.</NewCoverage>
    <RejectedScope>AcousticAup 40-byte audio contract; replay/prologue file-format records.</RejectedScope>
  </TASK_02_SEQUENTIAL_LAYOUT_INQUISITION>
  <STRUCT_LAYOUT_VERIFICATION>
    <UIValueSlot size="32">Version@0:uint; Flags@4:uint; Value@8:float; PreviousValue@12:float; LastWriteUnscaledTime@16:float; pads@20/24/28.</UIValueSlot>
    <PlayerMovementRuntimeState size="128">WorldPosition@0; PredictedWorldPosition@12; PredictedAup@24:48 bytes; Velocity@72; Forward@84; CameraForward@96; scalar lanes@108/112/116; Flags@120; pad@124.</PlayerMovementRuntimeState>
    <PlayerSurvivalRuntimeState size="128">19 float lanes@0..72; StatusMask@76; Flags@80; pad uint@84; tail ulong pads@88/96/104/112/120.</PlayerSurvivalRuntimeState>
  </STRUCT_LAYOUT_VERIFICATION>
  <COMPILE_GUARD>No new sibling runtime assembly dependency added. Build not relaunched; previous dependency wall remains the controlling compile fact.</COMPILE_GUARD>
</SELF_AUDIT>

## 2026-05-20 Alignment Pass - Core Content And Dispatcher DTO Sweep

What was wrong:
- Core-owned ContentAuthority and dispatcher records still used non-Pack Sequential layout while living in Vault/NativeArray/telemetry routes.
- `ContentBundleRefState` had a 24-byte stride. That stride is 8-byte aligned per field, but not cache-quantized; repeated Vault scans phase-shift across 64-byte lines.

What was done:
- Converted `ContentBundleRefState` to Explicit Size=32 with `Bytes@0`, `Hash@8`, `RefCount@12`, `LastAccessFrame@16`, byte flags at 20..23, and `_pad0@24` covering 24..31.
- Converted `ContentAuthorityTelemetryEntry=64`, `ContentPendingLoadState=16`, `ContentVisualFeatureBudget=16`, `ContentLoreBlockIndex=16`, `TelemetryEvent=64`, `DispatcherStateDTO=32`, `DispatcherPipelineTelemetryEntry=32`, and `MockTimeDilationSignal=16` to explicit offsets.
- Updated ContentAuthority editor layout validator to expect `ContentBundleRefState=32`.

Cinematic Cheats used:
- No simulation was added. The pass keeps content residency and dispatcher telemetry as compact scalar rows; expensive content visuals remain budgeted by existing feature masks and shader-side presentation instead of CPU-side per-asset physics.

Exact Microseconds saved:
- Static estimate: 2-15 us per 10k ContentAuthority bundle/telemetry rows in residency-heavy frames by removing 24-byte phase churn and keeping telemetry rows on 16/32/64-byte explicit strides. No profiler proof.

Verification:
- Touched-file scan for `StructLayout(LayoutKind.Sequential` across ContentRuntimeServices, ContentLoreBinaryProvider, GlobalTelemetryBus, and SystemDispatcherContracts returned 0 hits.
- Global `StructLayout(...Pack=...)` scan under `Assets/_Project/Scripts` returned 0 hits.
- `git diff --check` for the touched content/telemetry/dispatcher files passed with CRLF warnings only.
- Build was not relaunched because the previous scoped Core build already exposed an unrelated dependency wall and the active instruction forbids rebuild until needed.

Remaining debt:
- Lockstep replay/file ABI structs and Unity job-wrapper structs still need owner-aware treatment, not blind conversion.
- Task 20 remains partial until the dependency wall is resolved and a meaningful Unity/Burst compile route can run.

<SELF_AUDIT>
  <TASK_02_SEQUENTIAL_LAYOUT_INQUISITION status="PARTIAL_PROGRESS">
    <NewCoverage>ContentBundleRefState=32; ContentAuthorityTelemetryEntry=64; ContentPendingLoadState=16; ContentVisualFeatureBudget=16; ContentLoreBlockIndex=16; TelemetryEvent=64; DispatcherStateDTO=32; DispatcherPipelineTelemetryEntry=32; MockTimeDilationSignal=16.</NewCoverage>
    <RejectedScope>Lockstep replay/file ABI records and Unity collection job wrappers.</RejectedScope>
  </TASK_02_SEQUENTIAL_LAYOUT_INQUISITION>
  <STRUCT_LAYOUT_VERIFICATION>
    <ContentBundleRefState size="32">Bytes@0:long bytes 0..7; Hash@8:uint; RefCount@12:int; LastAccessFrame@16:int; BiomeId@20:byte; Tier@21:byte; IsBiomeCache@22:byte; Reserved0@23:byte; _pad0@24:ulong bytes 24..31.</ContentBundleRefState>
    <TelemetryEvent size="64">FrameIndex@0; EventType@4; SubjectHash@8; ContextHash@12; ScalarValue@16; WorldPosition@20 float3 bytes 20..31; Reserved0..7 @32..60.</TelemetryEvent>
    <DispatcherStateDTO size="32">Eight uint lanes at offsets 0,4,8,12,16,20,24,28.</DispatcherStateDTO>
  </STRUCT_LAYOUT_VERIFICATION>
  <COMPILE_GUARD>No new sibling runtime assembly dependency added. Build not relaunched; previous dependency wall remains the controlling compile fact.</COMPILE_GUARD>
</SELF_AUDIT>

## 2026-05-20 Alignment Pass - NativeQueue Payload Quantization

What was wrong:
- Several NativeQueue payloads were still Sequential or explicit 24/40/48-byte records. That creates cross-cache-line queue strides and owner-blind padding.
- `ModuleStatusEventPayload` used DTO bool properties and a `ushort StatusBits` lane.
- Bridge verifier still expected 48/40-byte entries after 64-byte cache-line quantization became required.

What was done:
- Converted AudioLog, AtlasSignal, Bootstrap, GameBootstrapper, BiomeMatrix, Crafting, Weather, Localization, Narrative, Scan, ModuleStatus, Inventory, Core Registry, and Core command queue payloads to Explicit 16/32/64-byte layouts.
- Re-quantized `H8PrefabMappingEntry`, `H8FacadeTelemetryEntry`, `InputProfileDTO`, `InputTelemetryEntryDTO`, duplicate deterministic input DTOs, `MockPlayerKinematicsSignal`, and `BlackboxRingBufferDTO` to 32/64 where owner-safe.
- Replaced `ModuleStatusEventPayload` bool properties with static `ModuleStatusEvents` bit helpers over `uint StatusFlags`.
- Updated `SignalCryptographySmokeTester` expectations for AtlasSignal and BiomeMatrix payloads from Sequential to Explicit.

Cinematic Cheats used:
- Queue payloads spend dead padding bytes to avoid runtime branch/lock repairs. Module status now sends one packed uint mask instead of exposing multiple bool accessors.

Exact Microseconds saved:
- Queue payload quantization: estimated 3-25 us per heavy late-frame NativeQueue drain on low-end silicon.
- `ModuleStatusEventPayload` uint mask helpers: estimated 1-6 us per 10k payload reads.
- Bridge/input telemetry 64-byte strides: estimated 2-20 us per 10k entries when telemetry drains under editor/development pressure.

Verification:
- Targeted `git diff --check` passed with CRLF warnings only.
- Targeted Sequential scan for the converted queue/core files reports no remaining `[StructLayout(LayoutKind.Sequential)]`.
- `StructLayout(...Pack=...)` scan under `Assets/_Project/Scripts` reports zero hits.
- Build not run: latest CPU gate measured `dotnet_csc=0 CPU_avg=100.0`.

Remaining debt:
- Core `InputStateDTO` remains explicit Size=24 as a rollback ABI exception until netcode offsets are migrated.
- Broad non-Pack Sequential debt remains in Unity-owned job wrappers, editor rows, GPU/HLSL interop records, and domain-owned DTOs.

<SELF_AUDIT>
  <TASK_RECONCILIATION latest="queue_payload_quantization">
    <Task02 status="PARTIAL">More NativeQueue/Core payloads converted to Explicit; full owner-wide Sequential conversion still open.</Task02>
    <Task03 status="PASS_EXTENDED">ModuleStatus DTO bool properties removed; static mask helpers used instead.</Task03>
    <Task07 status="PASS_EXTENDED">Converted target payload strides to 16/32/64 bytes.</Task07>
    <Task08 status="PASS_EXTENDED">ModuleStatus status bits now stored in `uint StatusFlags`.</Task08>
    <Task20 status="BLOCKED">Compile proof blocked by CPU gate.</Task20>
  </TASK_RECONCILIATION>
  <STRUCT_LAYOUT_VERIFICATION>
    <ModuleStatusEventPayload size="64">ModuleEntityId@0:ulong; ModuleHashId@8:uint; ReferenceSlot@12:int; Integrity01@16:float; AirReserve01@20:float; PowerSupply01@24:float; StatusFlags@28:uint; EventType@32:ushort; Reserved@34:ushort; padding@36..63.</ModuleStatusEventPayload>
    <CraftingEventPayload size="64">SpawnPosition@0:Vector3; VelocityChange@12:Vector3; hashes@24/28/32:uint; Progress01@36:float; Quantity@40:int; ReferenceSlot@44:int; EventType@48:ushort; Reserved@50:ushort; padding@52..63.</CraftingEventPayload>
    <H8FacadeTelemetryEntry size="64">Frame@0:uint; FacadeHash@4:uint; FieldHash@8:uint; OffsetBytes@12:int; Old/New/Safe@16/20/24:float; LutSwapHash@28:uint; Flags@32:ushort; Reserved@34:ushort; padding@36..63.</H8FacadeTelemetryEntry>
  </STRUCT_LAYOUT_VERIFICATION>
  <H_PHI_VAULT_STATUS>No new private persistent NativeArray ownership introduced.</H_PHI_VAULT_STATUS>
  <COMPILE_GUARD>No sibling runtime asmdef dependency added. Build blocked by CPU gate.</COMPILE_GUARD>
</SELF_AUDIT>

## 2026-05-20 Alignment Pass - Core DTO Explicit Patch And Continuous Striding

What was wrong:
- After Pack purge, Core still contained high-confidence Sequential runtime DTOs in Bridge, input determinism, black-box telemetry, foveated telemetry, and simulation bucketing.
- `ModuloSimulationBucketer` still had prior low/high style debt in the traversal policy. That violated continuous `GlobalQualityWeight` load shedding even though DTO layouts themselves must stay platform-invariant.
- Compile proof remains unavailable because the CPU gate measured 100.0 percent with dotnet/csc count 0.

What was done:
- Converted `H8PrefabMappingEntry`, `H8PrefabLoreLinkEntry`, `H8DesignValueEntry`, `H8FacadeTelemetryEntry`, `H8FacadeTelemetryDumpHeader`, `H8InputFacadeBindingEntry`, and `H8FacadeMacroHeader` to Explicit layout with unchanged sizes and offsets.
- Converted `InputStateDTO`, `HapticCommandDTO`, `InputProfileDTO`, `InputTelemetryEntryDTO`, `MockCollisionSignal`, `MockToolEquipSignal`, and `MockPlayerKinematicsSignal` to Explicit layout.
- Converted `TelemetryHeaderDTO`, `TelemetryEventDTO`, `MockPhysicsState`, `MockOriginShiftSignal`, `BlackboxRingBufferDTO`, `TelemetryLoggingMaskDTO`, and `BlackboxSourceSlot` to Explicit layout.
- Converted `FoveatedSimulationTelemetryEntry`, `SimulationBucketFrameState`, `SimulationBucketRebalanceResult`, and `SimulationBucketBlackBoxEntry` to Explicit layout.
- Added NoAlias to `ModuloSimulationBucketer.LoadBalancingJob` NativeArray fields.
- Replaced stepped slow-bucket active count with deterministic frame-phase dither between 1/2/4 active slow bucket groups from SmoothStep(GlobalQualityWeight). Layout stays fixed; traversal cost breathes continuously.

Cinematic Cheats used:
- The traversal fake is temporal stochastic decimation over deterministic bucket groups. Low quality does not mutate data layout or create a smaller DTO; it reads fewer cache-line-stable groups and lets presentation interpolation hide the cadence.

Exact Microseconds saved:
- Core DTO explicit pinning: estimated 2-20 us per 10k hot reads where ABI now avoids compiler padding drift and possible split 8-byte loads.
- Continuous bucket dither: estimated 10-80 us per 100k bucketed slow-tick candidates under thermal pressure.

Verification:
- Targeted changed-file `git diff --check` passed with CRLF warnings only.
- Targeted Sequential counts after patch: H8BridgeContracts=0, InputDeterminismDtos=0, SimulationBucketingContracts=0, ModuloSimulationBucketer=0. Remaining Sequential in GlobalTelemetry/Foveated are Unity job wrappers or editor-only rows.
- `StructLayout(...Pack=...)` under `Assets/_Project/Scripts` returned 0 hits.
- Build not run: `dotnet_csc=0 CPU_avg=100.0`; build launch remains forbidden over 50 percent CPU.

Remaining debt:
- Task 02 remains partial because broad non-Pack Sequential structs still exist across domain-owned GPU/HLSL interop, authoring, Unity wrapper, and external-route records.
- Task 20 remains partial until Unity/Burst compile and runtime proof can run under the CPU gate.

<SELF_AUDIT>
  <TASK_02_SEQUENTIAL_LAYOUT_INQUISITION status="PARTIAL">
    <CoreDtosConverted>H8BridgeContracts, InputDeterminismDtos, GlobalTelemetryBus black-box DTOs, FoveatedSimulationTelemetryEntry, SimulationBucketFrameState, SimulationBucketRebalanceResult, SimulationBucketBlackBoxEntry.</CoreDtosConverted>
    <RemainingDebt>Non-Pack Sequential debt exists outside the owner-safe Core DTO set. Unity job wrapper structs were not explicit-laid out.</RemainingDebt>
  </TASK_02_SEQUENTIAL_LAYOUT_INQUISITION>
  <TASK_10_CONTINUOUS_SCALABILITY_LOD_STRIDING status="PASS_STATIC_SOURCE">
    <Policy>activeSlowBucketCount = dither(pow2 lerp 1..4, SmoothStep(GlobalQualityWeight), frameHash16)</Policy>
    <BinarySwitches>None in new bucketer path.</BinarySwitches>
  </TASK_10_CONTINUOUS_SCALABILITY_LOD_STRIDING>
  <STRUCT_LAYOUT_VERIFICATION>
    <InputTelemetryEntryDTO size="40">InputSystemTimeSeconds@0:double; Frame@8:uint; Sequence@12:uint; ButtonMask@16:uint; CurrentInputSchemeHash@20:uint; PollingTimeMicroseconds@24:uint; BufferedInputsConsumed@28:uint; HapticCommandsActive@32:ushort; Flags@34:ushort; _pad0@36:uint.</InputTelemetryEntryDTO>
    <MockOriginShiftSignal size="64">SectorX@0:long; SectorY@8:long; SectorZ@16:long; DeltaLocalMeters@24:float3; FrameNumber@36:uint; ShiftId@40:uint; Flags@44:uint; SourceHash@48:uint; ImpactPosition@52:float3.</MockOriginShiftSignal>
    <SimulationBucketBlackBoxEntry size="64">int/uint/float telemetry fields occupy 0..55; ActiveSlowBucketCount@56:byte; AupBarrierActive@57:byte; ReservedPadding@58:ushort; StateHash@60:uint.</SimulationBucketBlackBoxEntry>
  </STRUCT_LAYOUT_VERIFICATION>
  <COMPILE_GUARD>No new sibling runtime dependency added. Build not launched because CPU_avg=100.0.</COMPILE_GUARD>
</SELF_AUDIT>

## 2026-05-20 Alignment Pass - Total Pack Parameter Purge

What was wrong:
- 79 non-Pack=1 `StructLayout(...Pack=...)` parameters remained after the signal-fence pass. Those were still a policy breach and could hide future unaligned 8-byte fields.
- Two remaining packed records were not safe for blind Pack removal: `VoxelChunkModifiedEvent` contained `ulong`; `ResourceNodeTombstoneRecord` contained AUP data.

What was done:
- Converted `VoxelChunkModifiedEvent` to Explicit Size=64 with `VolumeInstanceId@0`, `MinAbsoluteCell@8`, `MaxAbsoluteCell@20`, scalar block@32..44, and tail uint pads@48..60.
- Converted `ResourceNodeTombstoneRecord` to Explicit Size=80 with `TombstoneId@0`, `Position@16`, `ChunkId@64`, and explicit tail pad@76.
- Removed every remaining Pack parameter under `Assets/_Project/Scripts`.
- Updated `BoidKillSignalSizeBytes` and `CableSnappedSignalStrideBytes` to their aligned 64-byte reality.

Cinematic Cheats used:
- Redundant Pack=4/16 declarations were deleted instead of preserving a fake "compact" memory story. The cheaper truth is default layout for cold shader/authoring records and Explicit for the two 8-byte hot records.

Exact Microseconds saved:
- Pack purge: 0-20 us per affected hot queue in frames touching `VoxelChunkModifiedEvent` or cable snap records; main value is removing ARM64 ABI ambiguity.

Verification:
- `Pack1=0 ExplicitPack=0 CorePhysicsPack=0 AnyPack=0`.
- `BadSignalCount=0`.
- `git diff --check -- Assets/_Project/Scripts` passed with CRLF warnings only.
- Build not run: CPU gate still measured 100.0 percent with dotnet/csc count 0.

Remaining debt:
- Non-Pack Sequential layout debt still exists and needs owner-aware conversion. The Pack parameter itself is gone.

## 2026-05-20 Alignment Pass - Signal Fence And Core/Physics Pack Gate

What was wrong:
- `ISignal` payloads still had 48/72/80/96/160-byte queue strides. Those strides make MPSC NativeQueue writes share cache lines under contention.
- `TerminalClickSignal` and `TerminalCommandSignal` were Sequential despite being SignalBus payloads.
- Explicit layouts still carried `Pack=8` in global contracts/editor guard records. This was not Pack=1, but it preserved a forbidden Pack parameter in byte-critical contracts.
- Generated global contract offsets still claimed `PhysicsFacade_Size = 40` and bootstrap context/snapshot size 80 after cache-line padding was required.

What was done:
- Re-quantized source-visible `ISignal` payloads to Explicit 16/32/64/128-byte layouts, except `TetherTensionSignal` at 192 bytes because two 48-byte AUPs plus metadata cannot fit into 128 without deleting authoritative data.
- Added explicit tail padding to Construction, VisualScavenge, SeismicTide, Modding haptic, ToolKinematics, TerminalOS, and central Core signal payloads.
- Added `SignalBus<T>` cold-path ABI fence using `UnsafeUtility.SizeOf<T>()`; invalid strides reject queue initialization without runtime reflection.
- Removed all Explicit-layout Pack parameters under `Assets/_Project/Scripts`.
- Converted `GlobalContracts.PhysicsFacade` to Explicit Size=64 and padded bootstrap context/snapshot to Size=128. Updated `VaultOffsets.g.cs` and CompileWall X-Ray generated constants.
- Added `Arm64LayoutSourceFixer` editor CLI/report for Core/Physics safe Pack removal and hard-blocking Sequential DTO candidates.

Cinematic Cheats used:
- Signal queues now spend memory on deterministic padding instead of runtime lock/branch repair. This is the Dear Lie at the ABI layer: pay dead bytes, buy lower contention and predictable flushes.

Exact Microseconds saved:
- Signal queue stride fence: estimated 2-40 us saved per heavy flush under multi-producer pressure by preventing 48/72/80/96-byte stride sharing.
- Explicit Pack removal: 0 direct frame us; prevents future unaligned offset recurrence in Core/Physics.
- Bootstrap/facade cache quantization: 0 direct frame us; compile-wall contract ABI becomes deterministic for byte-copy tooling.

Verification:
- `BadSignalCount=0` by source-visible `ISignal` parser with allowed sizes 16/32/64/128/192.
- `Pack1=0 ExplicitPack=0 AnyPack=79`.
- `Core/Physics Pack` scan returned 0 hits.
- `git diff --check` passed with CRLF warnings only.
- Build not run: `dotnet_csc=0 CPU=100`; CPU gate forbids compile above 50%.

Remaining debt:
- 79 Sequential Pack parameters remain outside Core/Physics, mostly GPU/HLSL interop, authoring records, and domain-owned DTOs. Pack=1 remains zero.
- Unity/Burst compile proof is blocked by CPU-gate, not by choice.

<SELF_AUDIT>
  <TASK_11_SIGNAL_PAYLOAD_ALIGNMENT_FENCE status="PASS">
    <Evidence>BadSignalCount=0; SignalBus cold stride fence added; TerminalOS Sequential signal payloads converted to Explicit.</Evidence>
  </TASK_11_SIGNAL_PAYLOAD_ALIGNMENT_FENCE>
  <STRUCT_LAYOUT_VERIFICATION>
    <SystemHealthSignal size="64">Frame@0:uint; SystemHealthIndex01@4:float; FpsEwma@8:float; JitterSigmaMs@12:float; CpuTempC@16:float; GpuUtil01@20:float; BatteryLife01@24:float; KillSwitchMask@32:ulong aligned; tail padding@48,56.</SystemHealthSignal>
    <PlayerLookTargetSignal size="128">TargetAup@0:AbsoluteUniversePosition 48 bytes; RuntimeAnchor@48:float3; SurfaceNormal@76:float3; prompt args@96..108; tail padding@112,120.</PlayerLookTargetSignal>
    <TetherTensionSignal size="192">AnchorAup@0 and PayloadAup@48 are 8-byte aligned; metadata ends@144; tail padding@144..184 makes three exact 64-byte cache lines.</TetherTensionSignal>
  </STRUCT_LAYOUT_VERIFICATION>
  <H_PHI_VAULT_STATUS>No new private NativeArray ownership introduced. Existing AlignmentTelemetry ring uses BufferID.Arm64AlignmentTelemetryRing=642 and cursor=643.</H_PHI_VAULT_STATUS>
  <COMPILE_GUARD>Core/Physics Pack scan is zero. No new sibling runtime assembly dependency added.</COMPILE_GUARD>
  <BUILD_STATUS>Not run. CPU gate measured 100.0 percent; dotnet/csc count 0.</BUILD_STATUS>
</SELF_AUDIT>

## 2026-05-20 Alignment Pass - Pack1 Runtime Zero Sweep

What was wrong:
The previous pass left 91 runtime Pack=1 sites. Those were not theoretical: IK/vault records, Sargassum density and boid payloads, GPR/VFX telemetry, mining extraction records, hazard volumes, persistent-world save/delta records, and wreck-generation DTOs were still using packed sequential layout. Several touched Burst jobs also lacked the mandated `CompileSynchronously = true` flag.

What was done:
Runtime Pack=1 was reduced to zero by strict scan `StructLayout(... Pack = 1\b)` under `Assets/_Project/Scripts`. Converted DTO/native element records to explicit layouts with named padding in:
Animation IK, ladder IK, VR hand IK, procedural crab IK, contextual IK, Hecton player state, hazard exposure, deployable SDF drill contracts, data archaeology, item templates, suit telemetry/stats, RTG telemetry, camera juice, material decay, GPR telemetry, biolum diffusion GPU point data, vegetation nav portals, procedural wreckage/wreck records, Sargassum micro-fauna/global drag records, and persistent world save/delta records.

What was done:
Added `InitializeAlignedBufferJob` in Core Memory. It clears 64-byte cache-line chunks using eight `ulong` stores per job index and a bounded tail path. This is the Task 15 zero-init bypass for uninitialized Vault buffers.

What was done:
Patched touched jobs to synchronous Burst flags and added NoAlias fences where fields are NativeArray/NativeSlice/NativeList and are architecturally separate. Unity-owned job wrapper structs remain Sequential when they carry NativeArray/RaycastCommand handles; element DTOs carry the explicit ABI.

Cinematic Cheats used:
No physical simulation was added. The Dear Lie remained data-side: flag payloads were compressed where already in scope, small packed records were padded into cache-line DTOs, and Sargassum/ladder/IK records retain cheap foveated or low-tier mathematical fallbacks instead of extra physics.

Exact Microseconds saved:
Static estimate only. Runtime Pack=1 zeroing removes 6-120 us per 10k hot reads/writes in the formerly packed clusters, depending on contention and stride. Persistent save/delta quantization prevents 0-15 us per large delta scan plus false rollback hash misses. InitializeAlignedBufferJob should save 10-150 us on large boot-time clears versus scalar/default clear paths. No profiler proof yet.

Verification:
`git diff --check` passed for touched files with CRLF warnings only. Pack1 runtime scan reports `Pack1RuntimeCount=0`. Any Pack-parameter scan still reports `AnyPackRuntimeCount=89` for Pack=2/4/8/16 or explicit Pack=8 sites, many of which are GPU/HLSL interop or older global contracts. No dotnet/csc process was active, but CPU measured 100.0%, so compile was not launched under the build gate.

Remaining debt:
Signal payload alignment fence is still red: many ISignal payloads retain non-16/32/64/128 sizes and need a separate strict migration. Broad Pack-parameter purge is not finished because Pack=4 HLSL/GPU interop records require shader/stride owner checks. Full Unity import/Burst compile/profiler proof is blocked by CPU gate.

<SELF_AUDIT>
AgentID: SHINOBU_204
TaskCount: 20
Pack1RuntimeCount: 0
AnyPackRuntimeCount: 89
RuntimeReflection: false
RuntimeGCFromValidation: 0 bytes/frame
EditorReflectionOnly: Arm64MemoryAlignmentXRayWindow
VaultBufferIDs: Arm64AlignmentTelemetryRing=642, Arm64AlignmentTelemetryCursor=643
ZeroInitKernel: InitializeAlignedBufferJob; CacheLineBytes=64; stores=8xulong; tail=bounded byte loop
CriticalLayout_LadderClimbIkInput: Size=128; float3 block@0..83; scalar block@84..116; Flags:uint@116; pad@120..127
CriticalLayout_VRHandPresenceInput: Size=320; InteractableAUP@0; quaternions@64/80/96/112; float3 block@128..235; scalar/flags@236..275; pad@276..319
CriticalLayout_ProceduralCrabLegEntityState: Size=192; int/scalar block@0..99; RootPosition@100; Velocity@112; Avoidance@124; RootRotation@136; pad@152..191
CriticalLayout_HazardVolumeData: Size=64; double3 AUP@0; radius/scalars@24..44; HazardType@48; broadphase bytes@52..54; pad@55..63
CriticalLayout_DeployableSdfDrillMacroRecord: Size=128; Grid longs@0/8/16; LastUnscaledTimeSeconds@24; local/scalar/hash block@32..60; slot quantities/caps@62..76; item/ore hashes@80..108; pad@112..127
CriticalLayout_PersistentWorldItemRecord: Size=256; Position@0; ItemPersistentIdHash@48; FixedString128Bytes@56; ChunkId@184; packed quantity/flags@196; InstanceUid@200; pad@204..255
BuildVerification: NOT_RUN_CPU_GATE_100_PERCENT
SignalFenceStatus: FAIL_REMAINING_IRREGULAR_SIGNAL_SIZES
CompileGuard: no dotnet/csc active; build launch blocked by CPU > 50%
</SELF_AUDIT>

## 2026-05-20 Alignment Pass - CurrentMeta Weather Re-quantization

What was wrong:
- `CurrentMeta` was still Explicit Size=24. It is a Core weather DTO embedded in both snapshot fan-out and NativeQueue weather events.
- Expanding `CurrentMeta` without adjusting containing layouts would overlap `WeatherRuntimeSnapshot.Wave0` and `WeatherEventPayload.StateMask/EventType`.

What was done:
- Expanded `CurrentMeta` to Explicit Size=32 with `GlobalBaseVector@0`, `GlobalScale@12`, `ThermalIntensity@16`, `TimeAccumulator@20`, and named tail padding at 24..31.
- Expanded `WeatherRuntimeSnapshot` to Explicit Size=192. Wave components now start at `Wave0@64`, `Wave1@96`, `Wave2@128`; tail padding covers 160..191.
- Expanded `WeatherEventPayload` to Explicit Size=128 with `CurrentMeta@32`, `EventType@64`, and named tail padding through byte 127.
- Updated `BinaryLayoutManifest` cold-boot assertions for `CurrentMeta` and `WeatherRuntimeSnapshot`.

Cinematic Cheats used:
- The three Gerstner components remain the cheap analytic weather/wave fake. No extra CPU fluid simulation was introduced; the saved path is still direct scalar metadata plus analytic wave terms.

Exact Microseconds saved:
- Estimated 1-10 us per heavy weather event drain or snapshot fan-out by removing a 24-byte Core DTO stride and aligning queue payloads to 128 bytes. Static estimate only; no profiler proof.

Verification:
- Targeted `git diff --check` passed for `GlobalRegistryContracts.cs`, `BinaryLayoutManifest.cs`, and `WeatherEvents.cs` with CRLF warnings only.
- `StructLayout(...Pack=...)` under `Assets/_Project/Scripts` returned 0 hits.
- Stale weather layout scan found no old CurrentMeta size assertion, no old WeatherRuntimeSnapshot size assertion, and no stale wave offsets 56/88/120 in source. One stale 64-byte weather-payload status note existed and was corrected.
- Build not run: `dotnet_csc=0 CPU_avg=50.3`; build launch remains forbidden over 50 percent CPU.

Remaining debt:
- Core `InputStateDTO` remains an explicit 24-byte rollback ABI exception until netcode offset owners migrate 24/48/60 hard-coded state offsets.
- Broad non-Pack Sequential layout debt remains outside owner-safe Core/NativeQueue DTOs.

<SELF_AUDIT>
  <TASK_02_SEQUENTIAL_LAYOUT_INQUISITION status="PARTIAL">
    <NewCoverage>CurrentMeta=32; WeatherRuntimeSnapshot=192; WeatherEventPayload=128.</NewCoverage>
    <RemainingDebt>Owner-blind conversion is still blocked for broad Sequential structs and the Core InputStateDTO rollback ABI exception.</RemainingDebt>
  </TASK_02_SEQUENTIAL_LAYOUT_INQUISITION>
  <STRUCT_LAYOUT_VERIFICATION>
    <CurrentMeta size="32">GlobalBaseVector@0:float3 bytes 0..11; GlobalScale@12:float; ThermalIntensity@16:float; TimeAccumulator@20:float; _pad0@24:ulong bytes 24..31.</CurrentMeta>
    <WeatherRuntimeSnapshot size="192">StateMask@0:uint; WeatherIntensity@4:float; GlobalCurrentVector@8:float3; GlobalWindVector@20:float3; CurrentMeta@32:32 bytes; Wave0@64:32 bytes; Wave1@96:32 bytes; Wave2@128:32 bytes; tail pads@160/168/176/184.</WeatherRuntimeSnapshot>
    <WeatherEventPayload size="128">GlobalCurrentVector@0:float3; GlobalWindVector@12:float3; StateMask@24:uint; WeatherIntensity@28:float; CurrentMeta@32:32 bytes; EventType@64:ushort; Reserved@66:ushort; _pad0@68:uint; tail pads@72..127.</WeatherEventPayload>
  </STRUCT_LAYOUT_VERIFICATION>
  <COMPILE_GUARD>No new sibling runtime assembly dependency added. Build not launched because CPU_avg=50.3.</COMPILE_GUARD>
</SELF_AUDIT>

## 2026-05-20 Alignment Pass - Compile Wall Triage

What was wrong:
- The first permitted scoped build command failed: `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:BuildProjectReferences=false -p:UseSharedCompilation=false -clp:ErrorsOnly`.
- The build emitted 117 errors. Most were existing dependency-wall failures: `Hecton8.Logistics.Grid`, `H8BinaryWorldPager`, `VaultGenerationHandle<>`, construction socket DTOs, docking/autopilot interfaces, and related sibling-domain symbols missing from the scoped Core compile route.
- One compile error was in the SHINOBU-owned signal layer: `AnomalySignal` duplicated `_padTail0/_padTail1/_padTail2`.

What was done:
- Removed the duplicate `AnomalySignal` padding triplet, leaving one explicit pad sequence at offsets 18, 20, and 24.
- Stopped orphan MSBuild dotnet nodes left by the failed scoped build attempt.
- Did not edit unrelated Logistics, Save, Construction, Docking, Fauna, or Vault-generation dependencies.

Cinematic Cheats used:
- None. This was compile-wall triage, not simulation work.

Exact Microseconds saved:
- 0 direct runtime us. Compile blocker removed in one signal DTO; remaining failure is dependency wall outside this agent's ABI ownership.

Verification:
- `git diff --check -- Assets/_Project/Scripts/Core/GlobalSignals.cs` passed with CRLF warnings only after the duplicate padding removal.
- A build rerun was not launched because the post-cleanup CPU gate rose above 50%.

Remaining debt:
- Scoped Core build still requires owners of Logistics Grid, binary pager, VaultGenerationHandle, construction socket DTOs, docking/autopilot contracts, and related dependency routes.
- Task 20 remains partial until a clean Unity/Burst compile route runs.

<SELF_AUDIT>
  <COMPILE_ATTEMPT status="FAILED_DEPENDENCY_WALL">
    <Command>dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 -nr:false -p:BuildProjectReferences=false -p:UseSharedCompilation=false -clp:ErrorsOnly</Command>
    <ObservedErrors>117</ObservedErrors>
    <ShinobuOwnedFix>AnomalySignal duplicate padding removed.</ShinobuOwnedFix>
    <DependencyWall>Hecton8.Logistics.Grid, H8BinaryWorldPager, VaultGenerationHandle, construction socket DTOs, docking/autopilot contracts.</DependencyWall>
  </COMPILE_ATTEMPT>
  <COMPILE_GUARD>Rerun blocked by CPU gate above 50 percent after orphan MSBuild nodes were stopped.</COMPILE_GUARD>
</SELF_AUDIT>

## 2026-05-20 Alignment Pass - Core Registry Sequential Sweep

What was wrong:
- `GlobalRegistryContracts.cs` still contained source-owned `LayoutKind.Sequential` DTOs on the contract boundary. Several carried AUP/double fields and public copy/fan-out paths, so CLR-inferred offsets were not acceptable evidence.
- Core input/scheduling helpers still had small Sequential records where hidden padding was compiler-owned.
- `XRInputState.HasActiveInput` and `EcosystemBiomassAuditSample.IsFinite` were DTO properties instead of explicit methods.

What was done:
- Converted `BufferedActionEntry=16`, `JobAdmissionBlackboxEntry=32`, `CombatDamageSignalAupShiftTransformer=16`, `InputState=24`, `PlayerInputState=64`, and `XRInputState=64` to explicit layouts.
- Converted every `StructLayout` record in `GlobalRegistryContracts.cs` to explicit layout while preserving existing size contracts: damage, celestial/GI/seismic, audio, VR somatic, narrative trigger, player pose, habitat waterline, gas snapshots/audits, hardware profile, and ecosystem records.
- Converted `XRInputState.HasActiveInput` and `EcosystemBiomassAuditSample.IsFinite` to methods and updated the two source-visible call sites.

Cinematic Cheats used:
- No physical simulation was added. The patch preserves registry scalar/vector contracts so downstream visual fakes can keep consuming compact facts rather than requesting heavyweight owner systems.

Exact Microseconds saved:
- Estimated 2-18 us per 10k registry/input/black-box payload reads by removing compiler-owned padding and property getter calls from copied DTO paths. Static estimate only; no profiler proof.

Verification:
- `rg -n "StructLayout\(LayoutKind\.Sequential" Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs` returned 0 hits.
- `rg -n "StructLayout\([^\)]*Pack\s*=" Assets/_Project/Scripts -g "*.cs"` returned 0 hits.
- `git diff --check -- Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs Assets/_Project/Scripts/World/EcosystemDirector.cs` passed with CRLF warnings only.
- No `dotnet build` or rebuild was launched. `dotnet.exe/csc.exe` process scan returned no active compiler process, but the known dependency wall remains and the user prohibited rebuild until needed.

Remaining debt:
- Broad Sequential debt remains outside owner-safe Core DTOs: Unity collection/job wrappers, generic wrappers, editor rows, GPU/HLSL interop records, persisted external ABI records, and domain-owned structs that require owner review.
- Compile proof remains blocked by the previously recorded dependency wall.

<SELF_AUDIT>
  <TASK_02_SEQUENTIAL_LAYOUT_INQUISITION status="PARTIAL">
    <NewCoverage>GlobalRegistryContracts.cs has 0 LayoutKind.Sequential hits; Core input/scheduler/global-signal DTOs converted to Explicit.</NewCoverage>
    <RemainingDebt>Owner-blind project-wide Sequential rewrite remains blocked by Unity wrapper, generic, shader, persisted ABI, and domain-owner boundaries.</RemainingDebt>
  </TASK_02_SEQUENTIAL_LAYOUT_INQUISITION>
  <STRUCT_LAYOUT_VERIFICATION>
    <DamagePacket size="48">Channel@0:byte; PreviousValue@4; NextValue@8; Magnitude@12; LocalPoint@16:float3; DamageType@28; IntegrityDelta@32; Depth@36; SourceId@40; TraumaLevel@42; pads@1..3/33..35/43..47.</DamagePacket>
    <PlayerRuntimePoseSnapshot size="80">RuntimePosition@0:float3; Forward@12:float3; Aup@24:AbsoluteUniversePosition bytes 24..71; Flags@72; _pad0@76.</PlayerRuntimePoseSnapshot>
    <GasBaseHibernationSnapshot size="88">CenterAup@0:AbsoluteUniversePosition bytes 0..47; HibernatedUnscaledTime@48:double; floats@56/60/64; ints@68/72/76; flags@80/81; pads@82/84.</GasBaseHibernationSnapshot>
  </STRUCT_LAYOUT_VERIFICATION>
  <COMPILE_GUARD>No new sibling runtime assembly dependency added. Build not launched after this sweep.</COMPILE_GUARD>
</SELF_AUDIT>

## 2026-05-20 Alignment Pass - GlobalTelemetry Blackbox Job Sweep

What was wrong:
- `GlobalTelemetryBus.Blackbox.cs` still had Sequential job/editor frame records after the registry sweep.
- The records are pointer/scalar Burst job payloads or editor-only frame snapshots, not Unity collection wrappers, so their 32-byte ABI can be source-owned.

What was done:
- Converted `NanSweeperJob` to Explicit Size=32 with pointers at offsets 0, 16, and 24.
- Converted `MockOriginShiftFireJob` to Explicit Size=32 with `Output@0`, scalar lanes at 8/12/16/20/24, and tail pad at 28.
- Converted editor-only `BlackboxEditorFrame` to Explicit Size=32 with `ImpactPosition@12` and tail pad at 28.

Cinematic Cheats used:
- None. This is black-box crash forensics and fault injection plumbing, not simulation.

Exact Microseconds saved:
- Estimated 0-4 us per black-box sweep dispatch. Main value is deterministic forensic ABI, not frame-time gain.

Verification:
- `rg -n "StructLayout\(LayoutKind\.Sequential" Assets/_Project/Scripts/Core/GlobalTelemetryBus.Blackbox.cs` returned 0 hits.
- `git diff --check -- Assets/_Project/Scripts/Core/GlobalTelemetryBus.Blackbox.cs` passed with CRLF warnings only.
- Build not launched. Later compiler gate observed an external `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /m:1` process, so SHINOBU did not start another compile.

Remaining debt:
- Core scan still reports expected Sequential debt in Unity/native wrappers, generic wrappers, static-data contracts, replay/file ABI records, and domain-owned contracts requiring owner review.

<SELF_AUDIT>
  <STRUCT_LAYOUT_VERIFICATION>
    <NanSweeperJob size="32">Payload@0:pointer; PayloadBytes@8:int; FatalHash@12:uint; IsCatastrophicFailure@16:pointer; FatalHashOutput@24:pointer.</NanSweeperJob>
    <MockOriginShiftFireJob size="32">Output@0:pointer; OutputLength@8:int; Seed@12:uint; FrameNumber@16:uint; _pad0@20:uint; _pad1@24:uint; _pad2@28:uint.</MockOriginShiftFireJob>
    <BlackboxEditorFrame size="32">FrameNumber@0:uint; FatalHash@4:uint; LastEventHash@8:uint; ImpactPosition@12:Vector3; Slot@24:int; _pad0@28:uint.</BlackboxEditorFrame>
  </STRUCT_LAYOUT_VERIFICATION>
  <COMPILE_GUARD>No build launched after this sweep.</COMPILE_GUARD>
</SELF_AUDIT>

## 2026-05-20 Alignment Pass - Lockstep MacroDatabase StaticData Sweep

What was wrong:
- Lockstep replay/hash rows, MacroDatabase cache rows, and H8StaticData file records still depended on `LayoutKind.Sequential`.
- These records are rollback, MMF, file, telemetry, or native-cache contracts. Natural alignment is not enough evidence for ARM64 and binary payload ABI.

What was done:
- Converted lockstep DTO rows to explicit layouts: `LockstepPlayerKinematicState=96`, `LockstepReplayInputFrame=48`, `LockstepReplayBlockHeader=128`, `LockstepArrayHash=32`, `LockstepTelemetryEntry=64`, and `LockstepMasterHashHistoryEntry=32`.
- Converted MacroDatabase contracts and private cache rows to explicit layouts: `MacroDatabaseConfig=64`, `MacroDatabasePayloadHandle=40`, `MacroDatabaseStats=80`, `MacroDatabaseTelemetryEntry=72`, `HydrationCandidate=48`, `MacroDatabaseDirtyPayloadSlot=64`, and `MacroDatabaseSectorCoordSlot=64`.
- Converted H8StaticData file/lookup/static rows to explicit layouts while preserving the on-disk schema sizes: headers 64/32, lookup/Babel rows 16, static balance rows 48, telemetry 64, dump header 32.

Cinematic Cheats used:
- No simulation added. The patch preserves compact scalar/static-data contracts so downstream systems can continue using MMF/BTree lookup and authored fakes instead of managed object graphs.

Exact Microseconds saved:
- Estimated 1-12 us per 10k lockstep/cache/static-data row reads. Main gain is ABI determinism, 64-byte dirty-slot isolation, and removal of source-unowned file offsets.

Verification:
- `rg -n "StructLayout\(LayoutKind\.Sequential" Assets/_Project/Scripts/Core/Contracts/MacroDatabaseContracts.cs Assets/_Project/Scripts/Core/Database/H8MacroDatabaseService.cs Assets/_Project/Scripts/Core/Data/H8StaticDataContracts.cs` returned 0 hits.
- `rg -n "StructLayout\([^\)]*Pack\s*=" Assets/_Project/Scripts -g "*.cs"` returned 0 hits.
- `LockstepStateValidator.cs` remaining Sequential hits are Unity `NativeArray` job wrappers at the validator job layer, not element DTOs.
- `git diff --check` for the touched Core/Data/Lockstep/MacroDatabase files passed with CRLF warnings only.
- No `dotnet build` or rebuild was launched.

Remaining debt:
- Full project Sequential inquisition remains open for owner-specific domains and Unity/generic wrapper exceptions.
- Compile proof remains blocked by the previously recorded dependency wall and the active no-rebuild gate.

<SELF_AUDIT>
  <STRUCT_LAYOUT_VERIFICATION>
    <LockstepReplayBlockHeader size="128">Magic@0:ulong; Version@8:uint; HeaderSizeBytes@12:uint; StartFrame@16:uint; HashFrame@20:uint; InputCount@24:uint; Flags@28:uint; MasterHash@32:LockstepArrayHash; PlayerCount@64:uint; SnapshotStrideBytes@68:uint; SnapshotOffsetBytes@72:uint; InputFrameStrideBytes@76:uint; BlockSequence@80:uint; HashCadence@84:uint; Reserved1@88:ulong; Reserved2@96:ulong; Reserved3@104:ulong; Reserved4@112:ulong; Reserved5@120:ulong.</LockstepReplayBlockHeader>
    <MacroDatabaseDirtyPayloadSlot size="64">SectorHash@0:ulong; Handle@8:MacroDatabasePayloadHandle bytes 8..47; Version@48:uint; State@52:byte; Reserved0@53:byte; Reserved1@54:ushort; Pad0@56:ulong.</MacroDatabaseDirtyPayloadSlot>
    <H8StaticDataHeader size="64">Magic@0:uint; FormatVersion@4:ushort; HeaderSizeBytes@6:ushort; SchemaMajor@8:ushort; SchemaMinor@10:ushort; uint lanes FileByteLength..Reserved2 at 12,16,20,24,28,32,36,40,44,48,52,56,60.</H8StaticDataHeader>
    <H8StaticDataLookupEntry size="16">Hash@0:uint; RecordType@4:ushort; ByteSize@6:ushort; Offset@8:long.</H8StaticDataLookupEntry>
    <H8StaticDataTelemetryEntry size="64">Eight uint lanes @0..28; FileByteLength@32:long; LastOffset@40:long; four uint lanes @48..60.</H8StaticDataTelemetryEntry>
  </STRUCT_LAYOUT_VERIFICATION>
  <H_PHI_VAULT_STATUS>No new private NativeArray/NativeList/NativeHashMap fields were introduced. This pass edited DTO layouts only.</H_PHI_VAULT_STATUS>
  <COMPILE_GUARD>No sibling Runtime dependency added. Build not launched by mandate.</COMPILE_GUARD>
</SELF_AUDIT>

## 2026-05-20 Alignment Pass - SaveSystem Persisted DTO Sweep

What was wrong:
- `SaveStateMerkleTree.cs`, `SaveMasterHashV10.cs`, and `SaveDeltaCompression.cs` still used Sequential layout for persisted save, WAL, Merkle, and delta-compression rows.
- These records are blind binary/hash inputs; file-schema rows must not depend on compiler-inferred padding.

What was done:
- Converted Merkle/tree/WAL/telemetry records to explicit 32/64/80/128-byte layouts without changing schema sizes.
- Converted `SaveFileHeaderV10` to explicit Size=72 with master hashes at offsets 56 and 64.
- Converted delta records to explicit layouts: 8-byte packed runs, 24-byte quantized AUP sector row, 32-byte local offset/chunk rows, 64-byte strict header, and 264-byte sector payload.

Cinematic Cheats used:
- None. This pass protects save/delta ABI. Existing compressed/quantized delta rows are preserved as the data-density path.

Exact Microseconds saved:
- Estimated 1-10 us per 10k save/delta rows during hash/compression batches. Main value is ARM64-safe persisted ABI proof.

Verification:
- `rg -n "StructLayout\(LayoutKind\.Sequential" Assets/_Project/Scripts/SaveSystem/SaveStateMerkleTree.cs Assets/_Project/Scripts/SaveSystem/SaveMasterHashV10.cs Assets/_Project/Scripts/SaveSystem/SaveDeltaCompression.cs` returned 0 hits.
- `git diff --check` for the three SaveSystem files passed with CRLF warnings only.
- No build or rebuild was launched.

Remaining debt:
- Other SaveSystem/SaveBinaryStorage records still need owner-aware pass because several headers are schema constants and larger files must be migrated in smaller verified slices.

<SELF_AUDIT>
  <STRUCT_LAYOUT_VERIFICATION>
    <SaveFileHeaderV10 size="72">MagicValue@0:uint; Version@4:ushort; CompatMask@6:byte; Flags@7:byte; TimestampUnixMs@8:ulong; uint lanes Checksum..EntityOffset at 16,20,24,28,32,36; HashPayload64@40:ulong; HashHeader64@48:ulong; MasterStateHashLo@56:ulong; MasterStateHashHi@64:ulong.</SaveFileHeaderV10>
    <StrictSaveFileHeader64 size="64">Magic@0:ulong; PlayTimeSeconds@8:double; AupX@16:long; AupY@24:long; AupZ@32:long; Checksum@40:ulong; uint lanes Version..Reserved2 at 48,52,56,60.</StrictSaveFileHeader64>
    <SectorPayloadDTO size="264">SectorHash@0:uint; DataLength@4:uint; fixed Data[256]@8.</SectorPayloadDTO>
    <QuantizedAupSectorHalf3 size="24">SectorX@0:int; SectorY@4:int; SectorZ@8:int; LocalOffset@12:QuantizedLocalHalf3 bytes 12..19; Reserved0@20:uint.</QuantizedAupSectorHalf3>
  </STRUCT_LAYOUT_VERIFICATION>
  <COMPILE_GUARD>No build launched after this persisted-DTO sweep.</COMPILE_GUARD>
</SELF_AUDIT>

## 2026-05-20 Alignment Pass - SaveBinaryStorage Header Sweep

What was wrong:
- `SaveBinaryStorage.cs` still contained Sequential records for tokenized payloads, indexed sector metadata, override headers, current save headers, cloud metadata, delta cells, thermal RLE, and compact persistent-world rows.
- Legacy `IndexedSaveFileHeaderV8` was explicit but had `ulong` hash fields at offsets 36 and 44, a real unaligned 64-bit lane hazard on ARM64.

What was done:
- Converted all Sequential records in `SaveBinaryStorage.cs` to explicit layouts while preserving wire sizes.
- Split legacy V8 64-bit hashes into aligned uint halves at offsets 36/40/44/48 and added compose/split helpers for conversion.
- Kept `CurrentHeaderSize=56`, `IndexedHeaderV8Size=52`, `SaveFileHeaderPrefixSize=8`, and all indexed sector block/header constants unchanged.

Cinematic Cheats used:
- None. This is binary storage safety. Existing compact/tokenized payloads remain the data-density route.

Exact Microseconds saved:
- Estimated 1-15 us per 10k header/compact-row reads in load/save bursts. Primary gain is eliminating unaligned legacy hash loads.

Verification:
- `rg -n "StructLayout\(LayoutKind\.Sequential" Assets/_Project/Scripts/SaveBinaryStorage.cs` returned 0 hits.
- `git diff --check -- Assets/_Project/Scripts/SaveBinaryStorage.cs` passed with CRLF warnings only.
- No build or rebuild was launched.

Remaining debt:
- Legacy save formats still require versioned bridges; no schema widening was performed in this pass.

<SELF_AUDIT>
  <STRUCT_LAYOUT_VERIFICATION>
    <SaveFileHeader size="56">MagicValue@0:uint; Version@4:ushort; CompatMask@6:byte; Flags@7:byte; TimestampUnixMs@8:ulong; Checksum@16:uint; DeltaCount@20:uint; EntityCount@24:uint; PlayerOffset@28:uint; DeltaOffset@32:uint; EntityOffset@36:uint; HashPayload64@40:ulong; HashHeader64@48:ulong.</SaveFileHeader>
    <IndexedSaveFileHeaderV8 size="52">MagicValue@0:uint; Version@4:ushort; CompatMask@6:byte; Flags@7:byte; TimestampUnixMs@8:ulong; uint lanes DeltaCount..EntityOffset at 16,20,24,28,32; HashPayload64Lo@36:uint; HashPayload64Hi@40:uint; HashHeader64Lo@44:uint; HashHeader64Hi@48:uint.</IndexedSaveFileHeaderV8>
    <SaveCloudMetadataHeader size="128">TimestampUnixMs@0:ulong; HashPayload64@8:ulong; HashHeader64@16:ulong; uint/ushort/byte lanes @24..51; _pad0@52:uint; Reserved1..Reserved9 @56..120.</SaveCloudMetadataHeader>
  </STRUCT_LAYOUT_VERIFICATION>
  <COMPILE_GUARD>No build launched after this save-binary sweep.</COMPILE_GUARD>
</SELF_AUDIT>

## 2026-05-20 Alignment Pass - H8BinaryWorldPager Queue Sweep

What was wrong:
- `H8BinaryWorldPager.cs` still used Sequential layout for page write/read commands, read results, and pager telemetry rows.
- These rows are queue and forensic DTOs; hidden offsets weaken paging ABI proof.

What was done:
- Converted `PageWriteCommand` to explicit Size=32.
- Converted `PageReadCommand` to explicit Size=24.
- Converted `PageReadResult` to explicit Size=32.
- Converted `PagerTelemetryEntry` to explicit Size=64 with all 8-byte fields on 8-byte offsets.

Cinematic Cheats used:
- None. This is page IO queue layout hygiene.

Exact Microseconds saved:
- Estimated 0-8 us per 10k pager command/telemetry rows. Main gain is deterministic paging ABI and forensic row proof.

Verification:
- `rg -n "StructLayout\(LayoutKind\.Sequential" Assets/_Project/Scripts/SaveSystem/H8BinaryWorldPager.cs` returned 0 hits.
- Unaligned 8-byte `FieldOffset` scan over `H8BinaryWorldPager.cs` and `SaveBinaryStorage.cs` returned 0 hits.
- `git diff --check` passed with CRLF warnings only.
- No build or rebuild was launched.

<SELF_AUDIT>
  <STRUCT_LAYOUT_VERIFICATION>
    <PageWriteCommand size="32">SectorHash@0:long; PayloadType@8:uint; ByteOffset@12:int; ByteCount@16:int; SourceHash@20:uint; Frame@24:uint; Reserved@28:uint.</PageWriteCommand>
    <PageReadCommand size="24">SectorHash@0:long; PayloadType@8:uint; RequestId@12:uint; Frame@16:uint; Reserved@20:uint.</PageReadCommand>
    <PageReadResult size="32">SectorHash@0:long; PayloadType@8:uint; RequestId@12:uint; SlotIndex@16:int; ByteCount@20:int; Status@24:byte; Reserved0@25:byte; Reserved1@26:ushort; Reserved2@28:uint.</PageReadResult>
    <PagerTelemetryEntry size="64">SectorHash@0:long; Offset@8:long; Frame@16:uint; RequestId@20:uint; PayloadType@24:uint; PendingWrites@28:int; PendingReads@32:int; PageFaults@36:int; PayloadBytes@40:int; Operation@44:byte; Status@45:byte; Flags@46:ushort; TicksUtc@48:long; Reserved@56:long.</PagerTelemetryEntry>
  </STRUCT_LAYOUT_VERIFICATION>
  <COMPILE_GUARD>No build launched after this pager sweep.</COMPILE_GUARD>
</SELF_AUDIT>

## 2026-05-20 Alignment Pass - SaveData Blittable DTO Sweep

What was wrong:
- `SaveData.cs` mixed managed compatibility DTOs with fixed `[BinaryBlittableSafe]` records.
- The fixed records still used Sequential layout, leaving persisted mirror offsets compiler-owned.

What was done:
- Converted all `[BinaryBlittableSafe]` records in `SaveData.cs` to explicit layouts.
- Preserved existing sizes for player kinematics, inventory shadow, fauna hibernation, geology seam/cave rows, habitat flood state, module blit, PDA advisory, environmental strain, and module graph edge rows.
- Left managed DTOs with strings, arrays, or bool fields as compatibility structs; they are not unmanaged memcpy payloads.

Cinematic Cheats used:
- None. This pass protects fixed save mirror ABI. Existing compact mirror rows remain the data-density route.

Exact Microseconds saved:
- Estimated 1-12 us per 10k fixed save DTO rows in restore/mirror passes. Static estimate only.

Verification:
- `rg -U "\[BinaryBlittableSafe\]\s*\r?\n\s*\[StructLayout\(LayoutKind\.Sequential" Assets/_Project/Scripts/SaveData.cs` returned 0 hits.
- Unaligned 8-byte `FieldOffset` scan over `SaveData.cs` returned 0 hits.
- `git diff --check -- Assets/_Project/Scripts/SaveData.cs` passed with CRLF warnings only.
- No build or rebuild was launched.

<SELF_AUDIT>
  <STRUCT_LAYOUT_VERIFICATION>
    <HibernatedFaunaStateDTO size="112">species/biome/type/health @0/4/8/12; position@16:AbsoluteUniversePositionBlit128 bytes 16..63; rotation @64..76; linear velocity @80..88; angular velocity @92..100; uniqueInstanceUid@104:uint; flags@108:byte; pads@109/110.</HibernatedFaunaStateDTO>
    <ModuleBlitDTO size="64">prefabHashId@0:int; moduleHashId@4:int; aupGridX/Y/Z @8/16/24; aupLocal @32/36/40; rotation @44/48/52/56; health/flags/failure/reserved @60..63.</ModuleBlitDTO>
    <ExternalScavengerSiteDTO size="32">chunkX/Y/Z @0/4/8; offsets/radius @12..15; remainingTime@16:float; seed@20:uint; pad@24:long.</ExternalScavengerSiteDTO>
  </STRUCT_LAYOUT_VERIFICATION>
  <COMPILE_GUARD>No build launched after this SaveData fixed-record sweep.</COMPILE_GUARD>
</SELF_AUDIT>

## 2026-05-20 Alignment Pass - Core Prologue Inertial DOD Replay Sweep

What was wrong:
- `PrologueSequenceContracts.cs`, `InertialNavigationContracts.cs`, and `DodReplayRecorder.cs` still had Sequential DTO rows.
- These rows carry contract, navigation, replay, or telemetry state; hidden offsets weaken AUP/hash replay evidence.

What was done:
- Converted prologue snapshots to explicit layout: `PrologueOrbitalSnapshot=48`, `PrologueAtmosphericReentrySnapshot=16`, `PrologueCompleteSnapshot=16`.
- Converted navigation contracts to explicit layout: `CompassStateDTO=176`, `InertialNavigationSnapshot=120`.
- Converted DOD replay sidecars to explicit layout: `DodReplayJobProfileRecord=32`, `DodReplayAupDriftRecord=48`, `DodReplayEntityGhostRecord=32`, `DodReplayLogisticFlowRecord=48`, `DodReplayAtmosphereCellRecord=40`, `DodReplayVramAllocationRecord=32`, `DodReplayPhysicsSmokeRecord=56`, `ReplaySourceHash=24`, `AupDriftState=48`.

Cinematic Cheats used:
- None. This pass is ABI ownership and replay evidence hygiene. No physics or visual simulation was introduced.

Exact Microseconds saved:
- Estimated 1-10 us per 10k navigation/replay rows. Main gain is deterministic offset proof and ARM64 aligned 64-bit loads.

Verification:
- `rg -n "StructLayout\(LayoutKind\.Sequential" Assets/_Project/Scripts/Core/DodReplayRecorder.cs Assets/_Project/Scripts/Core/Contracts/PrologueSequenceContracts.cs Assets/_Project/Scripts/Core/Contracts/InertialNavigationContracts.cs` returned 0 hits.
- Unaligned 8-byte `FieldOffset` scan over the same three files returned 0 hits.
- `git diff --check` passed with CRLF warnings only.
- No build or rebuild was launched.

<SELF_AUDIT>
  <STRUCT_LAYOUT_VERIFICATION>
    <CompassStateDTO size="176">CurrentPosition@0:double3; CurrentVelocity@24:double3; AccumulatedDrift@48:double3; LastAnchorAup@72:double3; HeadingRadians@96:float; PitchRadians@100:float; RollRadians@104:float; DepthMeters@108:float; TemperatureCelsius@112:float; PressureKpa@116:float; LocalMagneticNorth@120:float3; DriftConfidence@132:float; NavigationFlags@136:uint; LastAnchorFrame@140:uint; AnchorQuality@144:float; ReservedFloat0@148:float; Reserved0@152:ulong; Reserved1@160:ulong; Reserved2@168:ulong.</CompassStateDTO>
    <InertialNavigationSnapshot size="120">Aup@0:double3; VelocityAupPerSecond@24:double3; DriftAup@48:double3; LocalAngularVelocity@72:float3; LocalAcceleration@84:float3; QuaternionYawPitchRoll@96:float4; Flags@112:uint; FrameIndex@116:uint.</InertialNavigationSnapshot>
    <DodReplayAupDriftRecord size="48">Frame@0:uint; EntityId@4:uint; SectorHash@8:ulong; ErrorMeters@16:double; DriftX@24:double; DriftY@32:double; Flags@40:uint; _pad0@44:uint.</DodReplayAupDriftRecord>
  </STRUCT_LAYOUT_VERIFICATION>
  <COMPILE_GUARD>No build launched after this Core replay/navigation sweep.</COMPILE_GUARD>
</SELF_AUDIT>

## 2026-05-20 Alignment Pass - ArchitectEye Diagnostics Row Sweep

What was wrong:
- `ArchitectEyeVisualizer.cs` still used Sequential layout for GPU instance rows, black-box entries, and runtime state.
- Empty Core/Persistence assembly marker structs still appeared as Sequential layout debt in the Core scan.

What was done:
- Converted `ArchitectEyeQuadInstance` to explicit Size=80 with five aligned `float4` lanes.
- Converted `ArchitectEyeBlackBoxEntry` to explicit Size=64.
- Converted `ArchitectEyeRuntimeState` to explicit Size=64.
- Converted `CoreContractsAssemblyMarker` and `PersistenceAssemblyMarker` to explicit Size=1.

Cinematic Cheats used:
- ArchitectEye remains an indirect-quad diagnostic overlay. No physics simulation was added; it keeps presentation as GPU instance data.

Exact Microseconds saved:
- Estimated 0-6 us per 10k diagnostic rows/uploads when ArchitectEye is active. Main gain is GPU/black-box ABI proof.

Verification:
- Touched-file Sequential scan returned 0 hits.
- Unaligned 8-byte `FieldOffset` scan over the touched files returned 0 hits.
- `git diff --check` passed with CRLF warnings only.
- No build or rebuild was launched.

<SELF_AUDIT>
  <STRUCT_LAYOUT_VERIFICATION>
    <ArchitectEyeQuadInstance size="80">CenterHalfX@0:float4; AxisYHalfY@16:float4; Color@32:float4; UvMode@48:float4; Aux@64:float4.</ArchitectEyeQuadInstance>
    <ArchitectEyeBlackBoxEntry size="64">Frame@0:uint; QuadCount@4:ushort; SignalLaneCount@6:ushort; pressure/health/frame floats @8/12/16/20/24; NonFiniteCount@28:int; KillSwitchMask@32:uint; Flags@36:uint; LastFaultPosition@40:float3; GasCo201@52:float; GasO201@56:float; StpScale01@60:float.</ArchitectEyeBlackBoxEntry>
    <ArchitectEyeRuntimeState size="64">int/uint cursor and flag lanes @0..20; runtime floats @24..44; signal/nonfinite counters @48/52; reserved ints @56/60.</ArchitectEyeRuntimeState>
  </STRUCT_LAYOUT_VERIFICATION>
  <COMPILE_GUARD>No build launched after this ArchitectEye diagnostics sweep.</COMPILE_GUARD>
</SELF_AUDIT>

## 2026-05-20 Alignment Pass - BurstCallback Handle Sweep

What was wrong:
- `BurstCallback` used Sequential layout despite being a source-owned single-function-pointer wrapper.
- The same file also contains queue wrappers with Unity-owned native-container internals.

What was done:
- Converted `BurstCallback` to explicit Size=8 with `FunctionPointer<BurstCallbackDelegate>` at offset 0.
- Left `BurstCallbackQueue` and `ParallelEventWriter` Sequential because they embed `NativeQueue` and parallel-writer internals.

Cinematic Cheats used:
- None. This is Burst callback ABI hygiene.

Exact Microseconds saved:
- Estimated 0-2 us per 10k callback wrapper reads. Main gain is source-owned function-pointer layout.

Verification:
- `BurstCallback.cs` unaligned 8-byte `FieldOffset` scan returned 0 hits.
- `git diff --check -- Assets/_Project/Scripts/Core/BurstCallback.cs` passed with CRLF warnings only.
- No build or rebuild was launched.

<SELF_AUDIT>
  <STRUCT_LAYOUT_VERIFICATION>
    <BurstCallback size="8">_function@0:FunctionPointer&lt;BurstCallbackDelegate&gt;.</BurstCallback>
  </STRUCT_LAYOUT_VERIFICATION>
  <COMPILE_GUARD>No build launched after this BurstCallback handle sweep.</COMPILE_GUARD>
</SELF_AUDIT>

## 2026-05-20 Alignment Pass - Crash Telemetry Toxic Chemistry Sweep

What was wrong:
- `CrashTelemetryBuffer.cs` still had Sequential binary headers for crash export and live telemetry.
- `ToxicOutgassingChemistryTypes.cs` still had Sequential DTO rows for toxic grid state, source rows, constants, mocks, combat-damage transfer, and telemetry.

What was done:
- Converted `CrashExportHeader=16` and `LiveTelemetryRecord=32` to explicit layout.
- Converted toxic chemistry DTO rows to explicit layout while preserving sizes: `ToxicityStateDTO=32`, `ToxicOutgassingGridHeaderDTO=64`, `ToxicitySourceDTO=48`, `ToxicOutgassingConstants=64`, `MockFlowField=32`, `MockWorldSampler=32`, `ToxicityCombatDamageSignal=64`, and `ToxicityGridTelemetryEntry=64`.

Cinematic Cheats used:
- Toxic rows preserve the existing cheap chemistry approximation path: mock flow/sampler and scalar density fields, no Navier-Stokes expansion.

Exact Microseconds saved:
- Estimated 1-8 us per 10k toxic chemistry rows plus 0-4 us per crash export batch. Static estimate only.

Verification:
- `rg -n "StructLayout\(LayoutKind\.Sequential" Assets/_Project/Scripts/CrashTelemetryBuffer.cs Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryTypes.cs` returned 0 hits.
- Unaligned 8-byte `FieldOffset` scan over both files returned 0 hits.
- `git diff --check` passed with CRLF warnings only.
- No build or rebuild was launched.

<SELF_AUDIT>
  <STRUCT_LAYOUT_VERIFICATION>
    <ToxicOutgassingGridHeaderDTO size="64">GridOriginAUP@0:double3; CellSizeMeters@24:float; GlobalQualityWeight@28:float; buffer ids/version @32/36/40/44:uint; Resolution@48:ushort; ActiveSources@50:ushort; ActiveEntities@52:ushort; Flags@54:byte; _pad0@55:byte; _pad1@56:ulong.</ToxicOutgassingGridHeaderDTO>
    <ToxicitySourceDTO size="48">AUP@0:double3; EmissionRate@24:float; Density@28:float; ChemicalHash@32:uint; _pad0@36:uint; _pad1@40:ulong.</ToxicitySourceDTO>
    <CrashExportHeader size="16">Magic@0:ulong; EntryCount@8:uint; StructSizeBytes@12:uint.</CrashExportHeader>
  </STRUCT_LAYOUT_VERIFICATION>
  <COMPILE_GUARD>No build launched after this crash/chemistry sweep.</COMPILE_GUARD>
</SELF_AUDIT>

## 2026-05-20 Alignment Pass - Graphics Material TBDR DTO Sweep

What was wrong:
- `ShinobuMaterialResponseRuntime.cs` still had Sequential shader/material/Vault DTO rows.
- `TBDRPipelineSurgeonTypes.cs` still had Sequential fixed DTO rows for budget counters, POI transforms, mock camera matrices, AUP GPU localization, texture streaming, telemetry, shader globals, and indirect draw args.

What was done:
- Converted all material response DTO rows to explicit layout.
- Converted fixed TBDR culling/shader rows to explicit layout.
- Left `MockScatterBuffer` Sequential because it embeds Unity `NativeArray` wrappers.

Cinematic Cheats used:
- Existing graphics path remains GPU/indirect-data driven: material aging and culling are scalar/DTO payloads, not per-object MonoBehaviour simulation.

Exact Microseconds saved:
- Estimated 2-14 us per 10k material/culling row reads or GPU uploads. Static estimate only.

Verification:
- `ShinobuMaterialResponseRuntime.cs` Sequential scan returned 0 hits.
- `TBDRPipelineSurgeonTypes.cs` Sequential scan reports only `MockScatterBuffer`, the documented NativeArray-wrapper exception.
- Unaligned 8-byte `FieldOffset` scan over both files returned 0 hits.
- `git diff --check` passed with CRLF warnings only.
- No build or rebuild was launched.

<SELF_AUDIT>
  <STRUCT_LAYOUT_VERIFICATION>
    <MaterialResponseTelemetryEntry size="64">Frame/Flags/VisibleCount/UploadedBytes @0/4/8/12; upload/texture/quality floats @16/20/24/28; StateHash/CsvGeneration/LayoutHash/_pad0 @32/36/40/44; Wear/Salt/Bio/Power means @48/52/56/60.</MaterialResponseTelemetryEntry>
    <PoiTransformDTO size="112">LocalToWorld@0:float4x4; CameraRelativePositionRadius@64:float4; MeshId@80:uint; InstanceId@84:uint; VertexCount@88:uint; DistanceSq@92:float; SortKey@96:uint; Flags@100:uint; _pad0@104:ulong.</PoiTransformDTO>
    <AupGpuLocalizationInput size="48">CellX@0:long; CellY@8:long; CellZ@16:long; Local@24:float3; BoundsRadius@36:float; MeshId@40:uint; InstanceId@44:uint.</AupGpuLocalizationInput>
  </STRUCT_LAYOUT_VERIFICATION>
  <COMPILE_GUARD>No build launched after this graphics DTO sweep.</COMPILE_GUARD>
</SELF_AUDIT>

## 2026-05-20 Alignment Pass - Audio Virtualization Contract Sweep

What was wrong:
- `AudioVirtualizationContracts.cs` still used Sequential layout for virtual voice ingress/state/selection/statistics/telemetry/tuning/mock DTO rows.
- The editor smoke test still asserted old Sequential source markers for two key DTOs.

What was done:
- Converted all source-owned virtual voice contract rows to explicit layout while preserving existing sizes.
- Kept `AcousticAup` lanes aligned at offsets 0, 40, and 80 where embedded.
- Updated `ShinobuAcousticDspSmokeTester` to assert Explicit layout for `VirtualVoiceDTO=48` and `VirtualVoiceSortKey=16`.

Cinematic Cheats used:
- Audio remains on the existing dear-lie SDF/Sabine/DSP approximation path. No heavy propagation simulation was introduced.

Exact Microseconds saved:
- Estimated 2-16 us per 10k virtual voice/sort/telemetry rows. Static estimate only.

Verification:
- `rg -n "StructLayout\(LayoutKind\.Sequential" Assets/_Project/Scripts/Audio/Virtualization/Contracts/AudioVirtualizationContracts.cs` returned 0 hits.
- Unaligned 8-byte `FieldOffset` scan over the contract file returned 0 hits.
- Smoke test source needles now match explicit layout strings.
- `git diff --check` passed with CRLF warnings only.
- No build or rebuild was launched.

<SELF_AUDIT>
  <STRUCT_LAYOUT_VERIFICATION>
    <VirtualVoiceDTO size="48">AupMeters@0:double3; Volume@24:float; Pitch@28:float; ClipHash@32:uint; SourceEntityID@36:uint; Importance@40:float; Padding@44:uint.</VirtualVoiceDTO>
    <VirtualVoiceRequest size="128">SourceAup@0:AcousticAup; SourceVelocity@40:float3; Volume/Priority/Pitch/Doppler @52/56/60/64; Sabine/LowPass/Delay @68/72/76/80; Event/Clip/Source/Stationary @84/88/92/96; byte flags @100..104; _reserved1@108:uint.</VirtualVoiceRequest>
    <AcousticEchoTap size="144">SourceAup@0:AcousticAup; ListenerAup@40:AcousticAup; Position@80:float3; scalar lanes @92..108; hashes @112/116/120; byte flags @124/125; reserved lanes @126/128/132/136.</AcousticEchoTap>
  </STRUCT_LAYOUT_VERIFICATION>
  <COMPILE_GUARD>No build launched after this audio virtualization contract sweep.</COMPILE_GUARD>
</SELF_AUDIT>

## 2026-05-20 Alignment Pass - Audio DSP Propagation Kernel Sweep

What was wrong:
- Adaptive stem, echolocation, acoustic portal propagation, and depth-stress synthesis fixed DTO/state rows still used Sequential layout.

What was done:
- Converted fixed rows in `AdaptiveStemAudioMixer.cs`, `AcousticEcholocationRaymarch.cs`, `AcousticPortalPropagation.cs`, and `DepthStressGranularSynthesisKernel.cs` to explicit layout.
- Preserved all existing sizes and kept `AcousticAup`/`double` lanes 8-byte aligned.

Cinematic Cheats used:
- The acoustic path remains SDF/Sabine/oscillator approximation. No heavy acoustic wave or pressure simulation was introduced.

Exact Microseconds saved:
- Estimated 1-10 us per 10k audio DSP/propagation rows. Static estimate only.

Verification:
- Touched-file Sequential scan returned 0 hits.
- Unaligned 8-byte `FieldOffset` scan over touched files returned 0 hits.
- `git diff --check` passed with CRLF warnings only.
- No build or rebuild was launched.

<SELF_AUDIT>
  <STRUCT_LAYOUT_VERIFICATION>
    <AcousticPathQuery size="112">SourceAup@0:AcousticAup; ListenerAup@40:AcousticAup; ListenerRight@80:float3; NodeCount@92:int; EdgeCount@96:int; MaxNodeExpansions@100:int; QualityTier@104:byte; DisablePortalPath@105:byte; reserved@106/108.</AcousticPathQuery>
    <AcousticPathResult size="104">LastPortalAup@0:AcousticAup; distance/delay/transmission/lowpass/itd/room/pathfinding @40..64; node indices @68..84; StateHash@88:uint; status bytes @92..95; reserved@96:uint.</AcousticPathResult>
    <KineticImpactSineOscillatorState size="24">Phase@0:double; LowPassState@8:float; AgeSeconds@12:float; pad floats @16/20.</KineticImpactSineOscillatorState>
  </STRUCT_LAYOUT_VERIFICATION>
  <COMPILE_GUARD>No build launched after this audio DSP propagation sweep.</COMPILE_GUARD>
</SELF_AUDIT>

## 2026-05-20 Alignment Pass - Scanner Data Mining Route Sweep

What was wrong:
- `ScannerDataMiningRouter.cs` still used Sequential layout for scanner result/spatial/entity state/mock/query/telemetry/settings DTO rows.

What was done:
- Converted all Sequential rows in `ScannerDataMiningRouter.cs` to explicit layout while preserving sizes.
- Kept `double3`, `long`, and `ulong` lanes on 8-byte offsets.

Cinematic Cheats used:
- Scanner occlusion remains SDF/mock DTO based; no Unity physics query expansion was introduced.

Exact Microseconds saved:
- Estimated 1-8 us per 10k scanner DTO rows. Static estimate only.

Verification:
- `rg -n "StructLayout\(LayoutKind\.Sequential" Assets/_Project/Scripts/Gameplay/ScannerDataMiningRouter.cs` returned 0 hits.
- Unaligned 8-byte `FieldOffset` scan over the file returned 0 hits.
- `git diff --check` passed with CRLF warnings only.
- No build or rebuild was launched.

<SELF_AUDIT>
  <STRUCT_LAYOUT_VERIFICATION>
    <ActiveScanStateDTO size="128">TargetAUP@0:double3; LastOriginAUP@24:double3; SectorHash@48:long; DepletionMask@56:ulong; pads@64/72; scalar lanes @80..124.</ActiveScanStateDTO>
    <ScannerSpatialEntityDTO size="64">AUP@0:double3; SectorHash@24:long; DepletionMask@32:ulong; EntityHash@40:uint; SphereRadius@44:float; MetadataIndex@48:uint; Flags@52:uint; DepletionWordIndex@56:uint; _pad0@60:uint.</ScannerSpatialEntityDTO>
    <ScannerTelemetryEntry size="64">TargetAUP@0:double3; _pad0@24:ulong; Frame/TargetHash/Flags/CandidateCount/CompletedCount/EstimatedMicroseconds @32..52; Progress01@56:float; HitDistance@60:float.</ScannerTelemetryEntry>
  </STRUCT_LAYOUT_VERIFICATION>
  <COMPILE_GUARD>No build launched after this scanner route sweep.</COMPILE_GUARD>
</SELF_AUDIT>
