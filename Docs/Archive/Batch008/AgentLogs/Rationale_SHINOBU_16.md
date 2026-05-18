# Rationale_SHINOBU_16

Date: 2026-05-18
Agent: SHINOBU_16
Status: CORE TASKS COMPLETE / H-PHI + ASYNC IO FALLBACK + LOW-TIER VISUAL BANDWIDTH + PHASE READBACK + FRONT-ONLY POINTERS + AUP TELEMETRY + BLACKBOX STRIDE + SCALABILITY SIGNAL + EDITOR AUP FACADE POLISHED / FULL UNITY COMPILE BLOCKED BY DEPENDENCY

## Intake Decision

Problem: Existing hazard implementation is unknown; prompt forbids SphereCollider trigger damage and requires a coarse mathematical heat/radiation field.

Solution: Follow fake-first coarse cellular automaton over native ping-pong buffers, expose O(1) trilinear sampling, and keep cross-domain outputs as typed unmanaged signals or mocks until concrete project contracts are found.

Rejected Alternatives: Unity trigger volumes and per-entity physics overlap checks were rejected because they scale with GameObject count, allocate or churn broadphase state when misused, and violate the prompt's collider eradication target. Full thermodynamic convection and particle radiation were rejected by the cinematic cheat mandate.

Scalability potential: Low uses 16^3 cells and sparse cadence; Middle uses 32^3 cells at conservative cadence; High keeps 32^3 with richer telemetry and visual upload; Ultra can increase visual sampling/detail in shaders while simulation remains bounded.

Hardware Impact: Expected low-end impact is lower broadphase and trigger-callback pressure on i3/MX350 by replacing collider spam with cache-linear Burst jobs and O(1) samples. Exact microsecond savings are PENDING PROFILER PROOF.

## Phase Decision

Problem: Thermodynamics mutates gameplay truth and also feeds presentation.

Solution: Run diffusion/emission/decay/rebase in SIMULATION, swap buffers and publish telemetry/signals in POST_SIMULATION, and upload heat distortion data in VISUAL_SYNC.

Rejected Alternatives: Running all work from `Update()` or an editor-only manager was rejected because the execution phase mandate forbids hidden gameplay scheduling.

Scalability potential: Low can skip visual upload pages and decimate grid resolution; High/Ultra can spend saved CPU on better shader distortion and denser debug visualization.

Hardware Impact: Phase-gated work keeps MX350 main-thread stalls measurable and makes load shedding explicit. Exact microsecond savings are PENDING PROFILER PROOF.

## Tasks 01-05 Decision

Problem: No current `StreamingAssets` directory was found, and archive rationale showed older weather/radiation systems but no active binary constants file for this batch.

Solution: Implement a cold `thermodynamic_constants.h8bin` reader and force `GenerateEmergencyMockConstants()` when the file is absent or malformed. The emergency constants are 16-byte scalar input compatible with the prompt: base water temp, heat diffusion, radiation diffusion, and decay.

Rejected Alternatives: Failing initialization on missing archaeology was rejected because the prompt requires a blind mock path. Creating ScriptableObject constants was rejected because runtime SO mutation is forbidden and not Burst-friendly.

Scalability potential: Low/MX350 can run with emergency constants and 16^3 active cells. Middle/High/Ultra can use the same constants with richer visual texture upload and editor tuning.

Hardware Impact: Hot path remains 0 file I/O and 0 managed allocation. Cold binary read cost is outside frame simulation. Exact runtime microseconds are PENDING PROFILER PROOF.

## Collider Eradication Decision

Problem: Existing hazard history includes trigger/radius damage routes, and the assignment identifies SphereCollider spam as the primary failure mode.

Solution: The SHINOBU path creates no colliders. Damage comes from trilinear field sampling and one-second per-entity throttled signal emission.

Rejected Alternatives: `SphereCollider.isTrigger`, `OnTriggerStay`, and `Physics.OverlapSphereNonAlloc` were rejected for hazard truth because they scale with physics broadphase rather than grid resolution.

Scalability potential: Low uses fewer cells and same query API; Ultra spends saved broadphase cost on heat-haze texture quality rather than more collider events.

Hardware Impact: Expected i3/MX350 gain depends on scene collider count. The specific removed cost is broadphase/trigger callback churn; profiler proof remains absent.

## Mock Dependency Decision

Problem: The metabolism and geological producers are not safe dependencies during concurrent batch work.

Solution: `MockHazardGenerator` seeds one 1000C source and one radiation leak. A local unmanaged `partial MockDamageSignal` in the Thermodynamics namespace proves blind damage without depending on Core signal archaeology; `CombatDamageSignal` is emitted only through throttled output.

Rejected Alternatives: Adding a same-namespace Core signal was rejected because duplicate signal names are build-blocking debt. Reusing an untracked Core mock was rejected after direct csc showed it was not a stable assembly contract. Direct player-health calls were rejected as cross-domain coupling.

Scalability potential: Mock sources are disabled once real source registration exists; no production content fork is created.

Hardware Impact: One mock entity costs one trilinear sample per scheduled solve. Damage spam is capped to one signal/second/entity.

## Core Solver Decision Tasks 06-10

Problem: Heat/radiation must spread across a 1 km class volume without trigger colliders, broadphase callbacks, or realistic fluid/radiation simulation cost.

Solution: A Burst `IJobParallelFor` evaluates a 32^3 or 16^3 raw-float cellular automaton. Temperature and radiation read front buffers, add source-grid emission, diffuse through six neighbors, apply mock SDF shielding with one scalar multiply, and write back buffers. Radiation decay is fused into the grid pass at a 1 Hz cadence.

Rejected Alternatives: Nav/physics overlap volumes were rejected because they scale with object count. Full convection, isotope particles, and voxel raycasts were rejected by the cinematic-cheat mandate and 0.2 ms target. A single in-place grid was rejected because it corrupts neighbor reads.

Scalability potential: Low uses 16^3 cells and coarse visuals. Middle uses 32^3 scalar truth. High keeps the same bounded solver and uploads better heat haze. Ultra spends saved cost on shader distortion and denser editor diagnostics, not higher gameplay chaos.

Hardware Impact: 16^3 is 4096 cells versus 32768 cells, an 8x iteration reduction for MX350/i3 class devices. Atomic source emission is bounded by source radius and source count. Exact microseconds remain PENDING PROFILER PROOF; direct Thermodynamics csc compile is clean.

## Query Signal Scale Decision Tasks 11-17

Problem: Coarse macro-cells cause blocky damage and visuals if entities read nearest cells, while 60 Hz damage emission can choke the SignalBus.

Solution: Entity sampling uses trilinear interpolation over eight cells. Per-entity damage timers accumulate heat/radiation and publish at one-second cadence. High-temperature cells publish capped `ThermalUpdraftSignal` events. The runtime records a fixed 300-frame telemetry ring and writes binary dumps on NaN/fault.

Rejected Alternatives: Nearest-cell damage, direct health writes, and per-frame damage signals were rejected. Direct VFX or silt references were rejected; the updraft contract stays signal-only. Unbounded text logs were rejected in favor of fixed binary black-box dumps.

Scalability potential: Low samples fewer cells by reducing grid resolution. Middle/High keep smooth entity gradients. Ultra can raise visual density and shader quality while the gameplay query cost remains O(1).

Hardware Impact: Eight scalar reads per entity replace physics-trigger residence. Signal cost is capped to 64 queued outputs/frame and one damage publication/sec/entity. Binary dump cost occurs only on fault.

## Human Control Facade Decision Tasks 18-20

Problem: Designers need immediate control over hazard constants and visibility into an invisible field, but runtime ScriptableObject tuning and managed CSV parsing violate zero-GC discipline.

Solution: `Thermodynamics Tuner` reads/writes unmanaged constants through GlobalDataVault buffer `(BufferID)70016` when the Vault exists. A fixed 4096-byte CSV buffer parses key/value overrides on a 1 Hz cold cadence and writes sanitized constants back into the Vault-backed view. SceneView gizmos read Vault mirrors `(BufferID)70017/70018`, not live simulation pointers, and draw cold/hot/radiation wire cubes in local macro-grid coordinates.

Rejected Alternatives: Editing Core enum IDs was attempted and removed because the domain boundary did not require it. ScriptableObject runtime constants, `string.Split`, LINQ, and direct render-domain dependencies were rejected. Gizmos reading simulation pointers directly were rejected to keep editor tooling decoupled from job-owned buffers.

Scalability potential: Low ignores editor mirror cost at runtime unless the tuner is opened. Middle/High get live tuning. Ultra can increase editor draw density without changing gameplay buffers.

Hardware Impact: The hot simulation path does not copy Vault mirrors unless editor visualization requests them. CSV parsing is cold-path only and fixed-buffer. Expected low-end impact is zero during shipping play without the editor facade active.

## Compile Wall Decision

Problem: Full Unity script compile fails before a clean whole-project result due unrelated domains.

Solution: Run full Unity batchmode for evidence, then run a targeted csc pass against the generated `Hecton8.Thermodynamics.rsp` with the current `Hecton8.Core.Memory.ref.dll` and available `Hecton8.Core.dll` to verify the SHINOBU asmdef in isolation.

Rejected Alternatives: Editing FloraGenomics, SpatialAudio, or Inventory from this domain was rejected as architectural sabotage. Hiding the failure was rejected; the wall is documented with exact files and lines in `Status_SHINOBU_16.md`.

Scalability potential: Once external compile blockers are fixed, Thermodynamics already has a clean direct csc pass and updated asmdef dependencies.

Hardware Impact: No runtime hardware impact; this is a build pipeline wall. The direct csc pass emitted `Hecton8.Thermodynamics.dll` and `.ref.dll` at 2026-05-17 20:28:49.

## Ultra Polish H-Phi Eviction Decision

Problem: The first complete runtime used private persistent `NativeArray<T>` fields. That passed local compile, but it violated the Ultra mandate's H-Phi rule: critical persistent buffers must be Vault-owned, not feudal MonoBehaviour-owned.

Solution: Replace every persistent thermodynamics buffer field with `VaultBufferHandle<T>` and resolve method-scoped `NativeArray<T>` views or raw pointers only at the point of work submission. IDs `(BufferID)70016-70038` cover constants, editor mirrors, front/back temperature/radiation grids, source grids, source/entity state, signal queues, telemetry ring/scratch, CSV bytes, and binary constant bytes. `GlobalRegistry.DataVault` is authoritative; a standalone `GlobalDataVault` fallback exists only for isolated mock execution when the registry is absent.

Rejected Alternatives: Keeping H8Memory-owned private arrays was rejected by the H-Phi mandate. Adding more Core enum entries was rejected after the existing named IDs covered constants/mirrors and local cast IDs avoided further `.Contracts` churn. Re-running a full Unity compile after a one-line hot-path cache change was rejected because the prior full compile wall is external and the Ultra mandate explicitly forbids rebuild spam.

Scalability potential: Low keeps 16^3 cells, cold CSV, capped signal output, and no collider broadphase. Middle keeps 32^3 truth with one trilinear sample path. High keeps richer heat-haze upload and telemetry. Ultra spends saved CPU/GPU budget on shader distortion and denser editor diagnostics without increasing gameplay damage spam.

Hardware Impact: H-Phi eviction removes unmanaged lifetime fragmentation and makes buffer ownership globally inspectable. Static shader property IDs remove repeated string-property lookups in visual sync. Expected MX350/i3 win remains architectural until profiler capture; targeted csc proof is clean in `Build_SHINOBU_16_thermo_csc_polish_r2.log`.

## Ultra Polish I/O Pressure Decision

Problem: The CSV and binary constants path still had synchronous file reads in cold/SlowTick code. Even if not per-frame, a Steam Deck MicroSD can stall the main thread on metadata or tiny-file reads.

Solution: Add one persistent config worker in `ThermodynamicsHazardGridRuntime.FileWorker.cs`. The worker owns MMF reads with sequential stream fallback, timestamp short-circuiting, and writes into Vault-backed byte buffers `(BufferID)70037/70038`. Main-thread `Tick()` only checks ready state and parses bytes already staged in the Vault; `SlowTick()` only enqueues a CSV request.

Rejected Alternatives: `Task.Run` per poll was rejected by `STRM_Async_Standard`. `FileSystemWatcher` was rejected because it adds event/delegate churn and platform variability. Keeping synchronous `File.Exists`/`FileStream` in `SlowTick()` was rejected because the Ultra mandate explicitly called out MicroSD pressure. A fully blocking binary load before first simulation was rejected; emergency constants now start immediately and worker-loaded constants override when ready.

Scalability potential: Low/MX350 starts from emergency constants with no storage wait and picks up CSV/binary overrides asynchronously. Middle/High/Ultra get the same authoring bridge without tying simulation cadence to disk latency.

Hardware Impact: Expected low-end gain is hitch avoidance, not steady-state CPU time. File I/O has moved off the main thread; unchanged CSV timestamps now skip data reads after metadata check on the worker. Exact milliseconds saved require MicroSD/player profiling.

## Ultra Polish ARM64 Staging Decision

Problem: The thermodynamics Vault staged outgoing combat damage in the external `CombatDamageSignal` contract. That type is outside this domain and has its own binary layout policy, so using it as persistent SHINOBU runtime storage weakens the "check every struct" claim.

Solution: Add local `ThermodynamicsCombatDamageSignal` as a 64B sequential staging DTO with explicit padding. Burst jobs write this aligned DTO into `(BufferID)70032`; `PublishQueuedSignals()` converts it to the existing `CombatDamageSignal` only on the stack at publication time.

Rejected Alternatives: Storing external `CombatDamageSignal` directly in the Vault was rejected for ARM64 audit clarity. Creating a new production damage lane was rejected because `CombatDamageSignal` already exists. Attempting to route mock damage through `SignalWardenMockDamageSignal` was tried, but targeted csc proved that type is not available from the referenced compiled contracts in this workspace; reverting to the prompt-required local `MockDamageSignal` avoided a compile wall.

Scalability potential: Low keeps the same one-signal/sec/entity cap. High/Ultra can consume existing `CombatDamageSignal` richer presentation without increasing thermodynamics staging cost.

Hardware Impact: Persistent damage staging is now 64B-aligned SHINOBU-owned memory. Runtime gain is mainly alignment risk reduction; exact microsecond delta is not claimed.

## Ultra Polish Stream Fallback Decision

Problem: The config worker's stream fallback used `FileStream.ReadByte()`. It was already off the main thread, but one managed stream call per byte is still bad storage behavior on MicroSD-class devices and weakens the I/O pressure audit when MMF is unavailable.

Solution: Replace the byte loop with `Span<byte>` over the existing Vault-backed destination pointer and read through `FileStream.Read(Span<byte>)` until the fixed capacity is full or EOF is reached. This preserves the primary MMF path, keeps all file I/O on the persistent worker, and avoids a managed byte array or `ArrayPool` dependency.

Rejected Alternatives: Keeping `ReadByte()` was rejected because fallback should not become O(bytes) stream calls. Allocating a temporary `byte[]` was rejected because it creates managed storage and makes the worker path harder to audit. `ArrayPool<byte>` was rejected because it introduces global managed pool state for a 4096-byte fixed Vault buffer. DirectStorage was rejected by `STRM_DirectStorage_Reality_Check` because no native plugin/integration exists in this project.

Scalability potential: Low/MX350 keeps emergency constants immediately and loads binary/CSV overrides asynchronously. Middle/High/Ultra keep authoring hot-reload without tying simulation cadence to file latency. Ultra visual tuning can iterate through the same CSV bridge without changing gameplay truth.

Hardware Impact: Stream fallback now reads in runtime-provided chunks instead of one byte per call. Expected gain is lower worker tail latency and less storage syscall pressure when MMF is unsupported; exact milliseconds require device profiling.

## Ultra Polish Low-Tier Visual Bandwidth Decision

Problem: The first heat-haze visual link uploaded the 3D temperature texture every dirty grid version on all tiers. That is acceptable for 32^3 desktop visual fidelity, but on MX350/toaster mode the simulation already admits coarser visual truth. Uploading every 16^3 change spends bandwidth on a visual that can lag a few frames without affecting gameplay.

Solution: Add `LowTierVisualUploadStride = 4`. When the active grid is 16^3 and the existing texture already matches resolution, VISUAL_SYNC skips three out of four dirty uploads while leaving `_visualDirty` set. Resolution changes still rebuild/upload immediately. High-tier 32^3 upload cadence is unchanged.

Rejected Alternatives: Disabling the heat texture on low tier was rejected because it would remove the visual bridge required by Task 15. Reducing gameplay simulation cadence was rejected because damage truth must remain current. Per-cell dirty pages were rejected in this pass because `Texture3D.SetPixelData` is whole-slice and a page protocol would expand scope without profiler evidence.

Scalability potential: Low/MX350 keeps scalar gameplay truth but spends less PCIe/driver bandwidth on visual heat distortion. Middle/High keep full dirty-grid visual sync. Ultra can still layer heavier shader distortion on top of the same authoritative heat grid.

Hardware Impact: Low tier can skip up to 75% of heat texture uploads when the grid changes every frame. Exact frame-time and bandwidth savings are PENDING PROFILER PROOF; targeted csc proof is clean in `Build_SHINOBU_16_thermo_csc_visual_r10.log`.

## Ultra Polish Phase-Safe Readback Decision

Problem: `TrySample()` and editor Vault readback could call `CompleteForColdReadbackIfIdle()`. That method routed into `LateFrameTick()`, meaning a read API could trigger job completion, front/back swap, SignalBus publication, telemetry commit, and visual texture upload outside the registered POST_SIMULATION/VISUAL_SYNC path.

Solution: Delete `CompleteForColdReadbackIfIdle()` and make read APIs consume only the current stable front-buffer snapshot. Completed back-buffer work is resolved by the registered `LateFrameTick()` phase, or by teardown through `ReleaseNativeState()`.

Rejected Alternatives: Keeping the helper was rejected because it smuggled phase work through query/readback calls. Calling only `_simulationHandle.Complete()` from `TrySample()` was rejected because it still creates a query-path sync point and violates the job discipline mandate. Returning back-buffer data directly was rejected because external readers must never see owner back buffers.

Scalability potential: Low/MX350 avoids hidden query stalls when AI/player sampling happens at high cadence. Middle/High/Ultra keep deterministic phase order: simulation writes back, POST_SIMULATION swaps/publishes, VISUAL_SYNC uploads.

Hardware Impact: Removed a potential read-path sync point and phase fan-out. Exact microsecond savings require profiler proof; targeted csc proof is clean in `Build_SHINOBU_16_thermo_csc_phase_r11.log`.

## Ultra Polish Front-Only Pointer Surface Decision

Problem: `TryGetUnsafeGridPointers()` exposed both front and back macro-grid pointers to external consumers. The owner job pipeline needs back-buffer write pointers internally, but external read consumers must not see back buffers because back is in-progress owner state.

Solution: Keep the DTO shape for compatibility, but make the public read API populate only `TemperatureFront` and `RadiationFront`. `TemperatureBack` and `RadiationBack` remain null for public callers. Private job scheduling still resolves back-buffer pointers directly inside the owner before scheduling diffusion/rebase jobs.

Rejected Alternatives: Removing the back fields from `ThermodynamicsHazardGridPointers` was rejected because it changes the public DTO shape more than needed in a concurrent batch. Leaving back pointers exposed was rejected because it violates the double-buffer isolation rule. Adding a writer registration API was rejected because no current consumer requires cross-domain writes.

Scalability potential: Low/MX350 and high tiers both keep cache-stable read snapshots. Future external Burst readers can sample the front grid without observing torn back-buffer writes.

Hardware Impact: Runtime microsecond gain is not claimed. The win is correctness: no external pointer can accidentally read or write the owner back buffer through the public surface. Targeted csc proof is clean in `Build_SHINOBU_16_thermo_csc_frontonly_r12.log`.

## Ultra Polish AUP Telemetry + Source Ref Containment Decision

Problem: `GetHazardSourceRef()` was public while only internal mock seeding used it. A public ref into the Vault source array could let another domain mutate hazard sources while `EmissionJob` reads them. Separately, blackbox telemetry downcast `_gridOriginAup` from `double3` to `float3`, which violates the AUP precision rule for absolute positions.

Solution: Make `GetHazardSourceRef()` private and implement it with `UnsafeUtility.AsRef` over the Vault pointer. External producers continue through `TryUpsertSource()`, which refuses mutation while simulation jobs are active. Telemetry now stores `GridOrigin = float3.zero` for local-frame context plus `GridOriginHash`, a millimeter-quantized FNV-style hash of the absolute `double3` origin.

Rejected Alternatives: Leaving the public ref was rejected because it bypassed the active-job guard. Exposing a writer pointer registration API was rejected because no current producer requires Burst-side source writes. Keeping absolute AUP as `float3` telemetry was rejected because it corrupts 100km world-scale precision.

Scalability potential: Low/MX350 keeps source mutation serialized through main-thread guarded upserts. High/Ultra keep the same source path and can use `GridOriginHash` to correlate blackbox dumps without paying for large managed strings or wider telemetry structs.

Hardware Impact: No fake microsecond gain is claimed. The win is race containment and removal of an absolute AUP float downcast. Targeted csc proof is clean in `Build_SHINOBU_16_thermo_csc_aup_r13.log`.

## Ultra Polish Blackbox Stride Decision

Problem: `ThermodynamicsHazardTelemetryEntry` declared a 64B stride, but the dump writer serialized only 56B of explicit fields after the `GridOriginHash` change. That makes the blackbox header lie about row size and weakens post-mortem tooling.

Solution: Add explicit `_pad0` and `_pad1` uint tail fields to the telemetry DTO, set them to zero in `ScanTelemetryJob`, and write them to every `.bin`/`.h8dump` telemetry row. The declared `UnsafeUtility.SizeOf<ThermodynamicsHazardTelemetryEntry>()` stride now matches serialized row bytes.

Rejected Alternatives: Relying on implicit `[StructLayout(Size = 64)]` tail padding was rejected because the binary writer is field-based, not raw memory copy. Reducing the declared Size to 56 was rejected because it breaks 8-byte/cache-line forensic discipline and future fixed-stride readers.

Scalability potential: Low/MX350 pays no steady-state cost beyond two zeroed uints in one telemetry entry per solve. Middle/High/Ultra get deterministic fixed-width blackbox rows for crash tooling without strings or variable-length payloads.

Hardware Impact: No frame-time gain is claimed. The win is blackbox correctness: every telemetry row is exactly 64 serialized bytes. Targeted csc proof is clean in `Build_SHINOBU_16_thermo_csc_blackbox_r14.log`.

## Ultra Polish Scalability Signal + Directed Updraft Decision

Problem: Task 13 names `SystemHealthIndex`, but the runtime only polled `GlobalRegistry.ScalabilityTier` through `UsesLowResolution()` during Tick. That violated the hot-path service-cache law and missed a typed health-pressure signal already present in the project. Task 12 also needed a sharper proof that vertical heat transfer actually biases hot water upward rather than merely emitting updraft signals.

Solution: Register as `IScalabilityChangedEventListener`, cache the tier on registration/event, and make Tick use `_cachedScalabilityTier`. Consume `SignalBus<SystemHealthIndexSignal>` snapshots with an index loop; critical/adrenaline pressure latches low grid resolution for 120 frames and sets `TelemetryFlagHealthPressureLowTier`. Rework vertical heat flux so heat gain from below and heat loss upward use the stronger coefficient, while heat gain from above and loss downward use the weaker coefficient.

Rejected Alternatives: Per-frame `GlobalRegistry.ScalabilityTier` polling was rejected by the DI mandate. Directly querying `HomeostasisBrain` was rejected as cross-domain coupling. Simulating convection was rejected by the Dear Lie mandate. Keeping the old isotropic-ish vertical math was rejected because it only partially satisfied the "hot water rises" task.

Scalability potential: Low/MX350 and any critical system-health pressure use 16^3 truth with a hysteresis window. Middle/High/Ultra keep 32^3 truth unless a typed health-pressure event forces temporary load shedding. Visual overkill remains shader-side and decoupled from gameplay damage truth.

Hardware Impact: Removing per-frame registry polling is a small architectural win; exact microseconds are not claimed. The material gain is that critical health pressure now sheds 8x cell iteration count without direct service coupling. Targeted csc proof is clean in `Build_SHINOBU_16_thermo_csc_scalability_r15.log`.

## Ultra Polish Editor AUP Facade Decision

Problem: After the runtime AUP telemetry fix, the editor gizmo facade still cast the absolute `double3` grid origin to a Unity `Vector3`. It was editor-only, but it preserved a bad 100km-world pattern in a required human-control tool.

Solution: Keep `TryGetVaultGridReadback()` as the data source, discard the absolute origin in `ThermodynamicsTunerWindow`, and draw SceneView cubes in local macro-grid coordinates around `Vector3.zero`. The runtime still owns absolute origin as `double3`; debug visuals now show grid shape/intensity without lying about AUP precision.

Rejected Alternatives: Keeping the cast was rejected because editor-only code still teaches bad domain practice. Adding a dependency on camera/origin-shift runtime services was rejected because the facade should remain isolated and only needs scalar grid visibility. Drawing at absolute Unity world coordinates was rejected because Unity `Vector3` cannot represent the full HECTON-8 AUP space correctly.

Scalability potential: Low and editor-heavy machines draw at most 4096 cubes from Vault mirrors. Middle/High/Ultra can increase diagnostic density later without touching gameplay simulation or AUP math.

Hardware Impact: No runtime frame-time gain is claimed; this is editor-only. The gain is precision correctness and avoiding accidental AUP float-copy patterns in future debug tooling. Targeted csc proof is clean in `Build_SHINOBU_16_thermo_csc_editoraup_r16.log`.

<SELF_AUDIT>
20-TASK CHECK:
01 [PASS] Binary archaeology fallback: cold `.h8bin` loader plus `GenerateEmergencyMockConstants()`.
02 [PASS] Trigger collider eradication: no `SphereCollider`, `OnTriggerStay`, or overlap hazard damage in Thermodynamics.
03 [PASS] CS1612 purge: internal mock source refs use private `UnsafeUtility.AsRef`; external producers use guarded `TryUpsertSource()`; no grid properties wrap native buffers.
04 [PASS] ARM64 padding: raw `float` grids and 40B `HazardSourceDTO`, no `Pack=1`.
05 [PASS] Blind mocks: `MockHazardGenerator`, local unmanaged `MockDamageSignal`, no metabolism dependency.
06 [PASS] 3D diffusion: Burst 6-neighbor cellular automaton.
07 [PASS] Ping-pong: front/back Vault handles swap; public unsafe readback exposes front pointers only.
08 [PASS] Emission: inverse-square source injection with atomic float CAS.
09 [PASS] Dear Lie SDF: one local mock shielding scalar replaces voxel raycasts.
10 [PASS] Half-life: radiation decay fused into the diffusion pass at 1 Hz.
11 [PASS] Entity query: eight-cell trilinear interpolation from stable front buffers; read APIs do not force job completion or buffer swap.
12 [PASS] Updraft: capped `ThermalUpdraftSignal` lane plus directed vertical heat flux.
13 [PASS] Hardware tier: 32^3 to 16^3 with hysteresis, cached scalability tier, and typed `SystemHealthIndexSignal` pressure latch.
14 [PASS] AUP rebase: integer cell-shift job preserves local hazard field.
15 [PASS] Visual link: RFloat `Texture3D` upload with cached shader property IDs and low-tier upload stride.
16 [PASS] Damage throttle: one accumulated signal per second per entity, staged in local aligned DTO then published to existing `CombatDamageSignal`.
17 [PASS] Telemetry: 300-frame Vault ring, NaN flag, millimeter-quantized `GridOriginHash`, health-pressure low-tier flag, explicit 64B row padding, and `.bin`/`.h8dump` binary dumps whose serialized row bytes match the DTO stride.
18 [PASS] Editor facade: `Thermodynamics Tuner` edits Vault constants.
19 [PASS] CSV override: fixed 4096-byte Vault parser fed by persistent background MMF worker with `Span<byte>` stream fallback, not main-thread file I/O.
20 [PASS] Gizmo visualizer: SceneView cubes read Vault mirrors, not job-owned live pointers, and draw in local macro-grid coordinates without absolute AUP float casts.

ARM64 CHECK:
HazardSourceDTO layout: offset 0 `double3 AUP` 24B; offset 24 `float Intensity` 4B; offset 28 `float Radius` 4B; offset 32 `uint HazardTypeHash` 4B; offset 36 `uint _pad0` 4B; total 40B, multiple of 8, no `Pack=1`.
ThermodynamicsHazardConstants: 8 floats, 32B.
ThermodynamicsHazardSample: 4 floats + `float3` + uint, 32B.
ThermodynamicsHazardGridPointers on 64-bit: 4 pointers 32B + 2 ints 8B = 40B.
ThermalUpdraftSignal: explicit 64B.
MockDamageSignal: 48B.
ThermodynamicsCombatDamageSignal: offset 0 `float3 WorldPoint` 12B; offset 12 `float3 Direction` 12B; offset 24 `float Magnitude` 4B; offset 28 `uint DamageType` 4B; offset 32 `uint TargetHash` 4B; offset 36 `uint SourceHash` 4B; offset 40 `uint Frame` 4B; offset 44 `ushort SourceId` 2B; offset 46 `ushort TargetId` 2B; offset 48 `byte Channel` 1B; offset 49 `byte Flags` 1B; offset 50 `byte IntegrityDelta` 1B; offset 51 `byte _pad0` 1B; offset 52 `uint _pad1` 4B; offset 56 `uint _pad2` 4B; offset 60 `uint _pad3` 4B; total 64B.
ThermodynamicsHazardTelemetryEntry: offset 0 `float MaxGridTemperature` 4B; offset 4 `float MaxRadiationLevel` 4B; offset 8 `float DiffusionComputeTimeMs` 4B; offset 12 `float3 GridOrigin` 12B; offset 24 `uint Frame` 4B; offset 28 `uint GridVersion` 4B; offset 32 `uint SourceCount` 4B; offset 36 `uint Flags` 4B; offset 40 `uint ShiftSequence` 4B; offset 44 `uint NaNCellIndex` 4B; offset 48 `uint ActiveResolution` 4B; offset 52 `uint GridOriginHash` 4B; offset 56 `uint _pad0` 4B; offset 60 `uint _pad1` 4B; total 64B.

ZERO-GC CHECK:
`Tick()` calls `EnsureNativeState()`, `ApplyPendingConfigLoads()`, registration, resolution hysteresis, mock seeding, and job scheduling. Persistent buffers are Vault handles; no `new NativeArray`, LINQ, `foreach`, closures, boxing, runtime `GetComponent`, `FindObjectOfType`, `GameObject.Find`, or collider APIs were found in `Assets/_Project/Scripts/Thermodynamics`. Config file reads are on one persistent background worker using MMF plus `Span<byte>` stream fallback; main thread only parses ready Vault bytes. Read APIs no longer call job completion or `LateFrameTick()`. Cold allocations remain isolated to worker creation, dump writing, and lazy `Texture3D` creation on visual resolution change.

AUP CHECK:
Sources and entities store `double3` absolute universe positions. Sampling and emission subtract `_gridOriginAup` before casting to `float3`; direct absolute AUP-to-float casts are not used for distance math. Blackbox telemetry stores a millimeter-quantized `GridOriginHash` for absolute origin correlation instead of downcasting `_gridOriginAup` to `float3`. Editor gizmos draw the Vault mirror in local macro-grid coordinates and discard absolute AUP from readback.

DEAR LIE CHECK:
The physical lie is deliberate: heat and radiation are a coarse 16^3/32^3 six-neighbor cellular automaton with mock SDF shielding, directed vertical heat flux, and trilinear reads. Low-tier heat-haze visuals additionally skip three out of four dirty texture uploads while gameplay samples the latest grid. No particles, Navier-Stokes, raycast radiation, or trigger damage volumes exist in this domain.

DEPENDENCY CHECK:
Cross-domain output uses `SignalBus<ThermalUpdraftSignal>`, prompt-required local `MockDamageSignal` for blind proof, existing `CombatDamageSignal` for production damage, `SignalBus<SystemHealthIndexSignal>` for health-pressure load shedding, `ScalabilityEvents` for cached tier changes, and `GlobalRegistry.DataVault`. No `Hecton8.World` runtime reference and no direct metabolism/rendering/health component calls. Existing `SignalWardenMockDamageSignal` was audited, but it was unavailable to the targeted Thermodynamics compile from current referenced assemblies.

H-PHI CHECK:
All persistent arrays are Vault-owned through `VaultBufferHandle<T>` IDs `(BufferID)70016-70038`. Method-local `NativeArray<T>` variables are resolved Vault views only; jobs receive raw pointers from those views. Public unsafe pointer readback exposes front-buffer pointers only; back-buffer pointers remain owner-private in job scheduling. Source refs are private to mock seeding; external mutation is guarded by `TryUpsertSource()`.

BLACKBOX CHECK:
The 300-frame `ThermodynamicsHazardTelemetryEntry` ring is Vault-backed. Each entry is 64B and the dump writer serializes all 64B fields, including explicit tail padding. Flags include NaN, low-tier, rebase, and health-pressure low-tier. NaN/fault flags trigger `Dump_THERMODYNAMICS.bin`, `Dump_THERMODYNAMICS.h8dump`, `Dump_SHINOBU_16.bin`, and `Dump_SHINOBU_16.h8dump`.

COMPILE GUARD:
Targeted Thermodynamics csc pass after editor AUP facade polish is clean: `Docs/AgentLogs/Build_SHINOBU_16_thermo_csc_editoraup_r16.log`, 0 bytes, exit code 0. One failed attempt to use `SignalWardenMockDamageSignal` is recorded in `Build_SHINOBU_16_thermo_csc_io_r3.log` and was resolved without external edits. Full Unity compile was not spammed again; prior wall is external and documented.
</SELF_AUDIT>

## Polish Mandate Result

Problem: The batch protocol requires reading `<POLISH_MANDATE>` only after all core tasks are checked or blocked.

Solution: After tasks 01-20 were checked, `Docs/Tasks/CURRENT_BATCH.md` was searched with a CLI regex for `<POLISH_MANDATE>`. The tag was absent. Local anti-bloat audit was still executed against Thermodynamics source.

Rejected Alternatives: Reading the polish tag early was rejected by protocol. Inventing a polish mandate was rejected because disk evidence is authoritative.

Scalability potential: No code change required. Anti-bloat scan confirms the hot path remains collider-free and free of managed collections in new Thermodynamics code.

Hardware Impact: No runtime impact. Audit evidence only.

