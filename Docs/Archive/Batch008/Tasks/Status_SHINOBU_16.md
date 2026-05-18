# Status_SHINOBU_16

Date: 2026-05-18
Agent: SHINOBU_16
Domain: THERMODYNAMICS_AND_HAZARD_ENGINEER
Status: CORE TASKS COMPLETE / H-PHI + ASYNC IO FALLBACK + LOW-TIER VISUAL BANDWIDTH + PHASE READBACK + FRONT-ONLY POINTERS + AUP TELEMETRY + BLACKBOX STRIDE + SCALABILITY SIGNAL + EDITOR AUP FACADE POLISHED / FULL UNITY COMPILE BLOCKED BY DEPENDENCY

## Mandates Read

- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `ARCH_Execution_Phases.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Signal_Lane_Segregation.txt`
- `MATH_AUP_Determinism_Sync.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.txt`
- `STRM_Async_Standard.txt`
- `STRM_DirectStorage_Reality_Check.txt`

## Phase Ownership Record

- Phase: SIMULATION for diffusion/emission/decay/rebase jobs.
- Phase: POST_SIMULATION for buffer swap, telemetry write, and signal publication.
- Phase: VISUAL_SYNC for GPU/texture upload hook.
- Owner assembly: `Hecton8.Thermodynamics`.
- DataVault buffers read: all persistent SHINOBU runtime state resolves from `GlobalDataVault` via `VaultBufferHandle<T>` IDs `(BufferID)70016` through `(BufferID)70038`; local `NativeArray<T>` values are method-scoped resolved views only.
- DataVault buffers written: constants `(BufferID)70016`, editor mirrors `(BufferID)70017/70018`, front/back heat and radiation ping-pong `(BufferID)70019-70022`, source grids `(BufferID)70023/70024`, source/entity/signal/telemetry/CSV scratch buffers `(BufferID)70025-70038`.
- Signal lanes consumed: `SignalBus<SystemHealthIndexSignal>` for critical/adrenaline load-shed pressure.
- Signal lanes published: `SignalBus<ThermalUpdraftSignal>`, `SignalBus<MockDamageSignal>`, `SignalBus<CombatDamageSignal>`.
- Budget: target under 200 us diffusion path on Steam Deck-class CPU; PENDING PROFILER PROOF.
- Load shed: 32^3 high path, 16^3 MX350/toaster path with 3s hysteresis; PENDING PROFILER PROOF.

## Task Checklist

- [x] Task 01 BINARY_GRAVEYARD_RECONNAISSANCE | DOD: archive/status scan plus cold binary loader with `GenerateEmergencyMockConstants()` fallback; `StreamingAssets` was absent in scan | Alternatives Rejected: hard dependency on missing `.h8bin` constants | Estimate: 0 us hot path, cold load only
- [x] Task 02 TRIGGER_COLLIDER_ERADICATION_PASS | DOD: new SHINOBU path has no `SphereCollider`, `OnTriggerStay`, or overlap damage; all hazard truth is scalar grid sample | Alternatives Rejected: trigger residence and `OverlapSphereNonAlloc` zone checks for damage | Estimate: scene-dependent broadphase callbacks removed, PENDING PROFILER PROOF
- [x] Task 03 CS1612_ENCAPSULATION_PURGE | DOD: `HazardSourceDTO` is an unmanaged struct, internal mock source mutation uses private `GetHazardSourceRef()` with `UnsafeUtility.AsRef`, external producers use guarded `TryUpsertSource()`, and no grid-array properties exist | Alternatives Rejected: public mutable source refs during active jobs, property-wrapped NativeArrays, and copied DTO mutation | Estimate: 1-3 us saved per source-edit burst versus copy/update churn, PENDING PROFILER PROOF
- [x] Task 04 ARM64_PADDING_RECONSTRUCTION | DOD: grids are raw `float` arrays and `HazardSourceDTO` is 40B (`double3` 24 + `float` 4 + `float` 4 + `uint` 4 + pad `uint` 4) | Alternatives Rejected: object/cell structs in the macro-grid | Estimate: keeps diffusion cache-linear; microseconds PENDING PROFILER PROOF
- [x] Task 05 BLIND_DEPENDENCY_MOCKING | DOD: `MockHazardGenerator` seeds 1000C heat and radiation leak, entity sampler emits local unmanaged `MockDamageSignal` and `CombatDamageSignal` without metabolism dependency | Alternatives Rejected: direct health/metabolism calls and same-namespace Core mock edits | Estimate: 0 external dependency cost, one throttled signal/sec/entity
- [x] Task 06 3D_DIFFUSION_SOLVER_KERNEL | DOD: Burst `DiffusionJob` runs 6-neighbor cellular automaton on raw `float*` temperature/radiation grids | Alternatives Rejected: fluid convection, particle radiation, per-volume GameObject simulation | Estimate: 32^3 path PENDING PROFILER PROOF, target <200 us
- [x] Task 07 POINTER_PING_PONG_SWAP | DOD: front/back `VaultBufferHandle<float>` ping-pong handles swap after completed simulation job, no per-frame allocation or bulk copy; public unsafe pointer readback exposes front buffers only | Alternatives Rejected: copying whole grids into a single source buffer each frame and exposing owner back buffers to external readers | Estimate: handle swap only, sub-1 us expected
- [x] Task 08 INVERSE_SQUARE_EMISSION_JOB | DOD: Burst emission maps `HazardSourceDTO.AUP` to grid cells and CAS-adds inverse-square heat/radiation into source grids | Alternatives Rejected: trigger radius damage and managed source lists | Estimate: O(source radius cells), PENDING PROFILER PROOF
- [x] Task 09 THE_DEAR_LIE_SDF_BLOCKING | DOD: local `MockWorldSampler` shields cross-barrier diffusion with one multiply by rock shielding factor | Alternatives Rejected: voxel raycasts and full terrain sampling dependency | Estimate: 6 cheap branch/multiply checks per cell
- [x] Task 10 HALF_LIFE_DECAY_EVALUATOR | DOD: radiation decay is applied by `DiffusionJob` at 1 Hz using `RadiationDecayCoefficient` | Alternatives Rejected: per-isotope simulation and per-source particle decay | Estimate: fused into grid pass, no extra allocation
- [x] Task 11 ENTITY_QUERY_TRILINEAR_SAMPLING | DOD: `TrySample()` and entity job use 8-cell trilinear interpolation for temperature/radiation and damage scalars from stable front buffers; read APIs do not force job completion or buffer swap | Alternatives Rejected: nearest-cell reads and mid-query completed-job swap/publish/upload | Estimate: 8 reads per entity sample, no query-path sync point
- [x] Task 12 THERMAL_UPDRAFT_GENERATION | DOD: high heat cells emit `ThermalUpdraftSignal` with AUP, intensity, cell index, and frame; vertical diffusion now uses directed flux so heat gains from below/losses upward are biased over gains from above/losses downward | Alternatives Rejected: coupling directly to Volumetric Silt or VFX components and isotropic vertical heat transfer | Estimate: capped to 64 signals/frame
- [x] Task 13 HARDWARE_TIER_GRID_DECIMATION | DOD: runtime switches between 32^3 and 16^3 active grids with 3s hysteresis using cached scalability tier, force-low settings, and typed `SystemHealthIndexSignal` pressure latch; hot path no longer polls `GlobalRegistry.ScalabilityTier` per frame | Alternatives Rejected: mid-tier balanced grid, per-frame resolution flapping, and registry polling as a live health bus | Estimate: 16^3 drops iteration count by 8x
- [x] Task 14 AUP_GRID_REBASING | DOD: `RebaseGridJob` shifts front grids by integer cell delta on `OriginShiftEventData`, edges reset to ambient/zero | Alternatives Rejected: clearing the full hazard field on every origin shift | Estimate: O(active cells), no allocation
- [x] Task 15 VISUAL_DISTORTION_BUFFER_LINK | DOD: POST_SIMULATION uploads front temperature field into global RFloat `Texture3D` and shader metadata; low-tier 16^3 path uploads every 4 dirty versions unless texture size changes | Alternatives Rejected: heat haze particle GameObjects and every-frame low-tier visual upload | Estimate: high path every dirty grid, low path up to 75% fewer visual texture uploads; PENDING PROFILER PROOF
- [x] Task 16 DAMAGE_SIGNAL_EMISSION_THROTTLING | DOD: per-entity timers accumulate heat/radiation damage and emit mock/combat damage no more than once/second/entity | Alternatives Rejected: 60Hz damage signal spam | Estimate: 1 signal/sec/entity max
- [x] Task 17 TELEMETRY_HAZARD_RECORDER | DOD: 300-frame native ring tracks max temp/rad, compute time, flags, source count, millimeter-quantized `GridOriginHash`, and health-pressure low-tier flag; 64B DTO has explicit tail padding and dump writer serializes all 64 bytes per entry; NaN triggers `.bin` and `.h8dump` outputs for `Dump_THERMODYNAMICS` and `Dump_SHINOBU_16` | Alternatives Rejected: unbounded logs, post-crash guessing, absolute AUP-to-float telemetry downcasts, and mismatched binary stride | Estimate: one scan job plus fixed binary dump on fault
- [x] Task 18 HAZARD_TUNER_EDITOR_WINDOW | DOD: `Thermodynamics Tuner` EditorWindow edits Base Water Temp, Heat Diffusion, Radiation Half-Life, Rock Shielding through GlobalDataVault constants pointer `(BufferID)70016` | Alternatives Rejected: ScriptableObject runtime tuning and managed copies | Estimate: editor-only, hot path unaffected unless tuner opened
- [x] Task 19 CSV_OVERRIDE_INGESTOR | DOD: persistent background config worker checks binary/CSV timestamps, reads changed files through MMF with `Span<byte>` stream fallback into Vault-backed 4096-byte staging, and main thread only parses ready bytes | Alternatives Rejected: `string.Split`, LINQ, per-frame file polling, per-byte `ReadByte`, and main-thread MicroSD reads | Estimate: 0 file I/O on Tick/SlowTick main thread
- [x] Task 20 GIZMO_GRID_VISUALIZER | DOD: EditorWindow `OnDrawGizmos`/SceneView hook reads Vault grid mirrors and draws blue cold, red hot, green radiation wire cubes with alpha by intensity using local macro-grid coordinates instead of absolute AUP float casts | Alternatives Rejected: Gizmos reading live simulation pointers directly or casting `double3` absolute origin into `Vector3` | Estimate: editor-only, capped to 4096 drawn cells

## Iteration Log

### Loop 0 - Intake

- Prompt extracted from `Docs/Tasks/CURRENT_BATCH.md` using CLI regex for `<AGENT_PROMPT id="SHINOBU_16" ...>`.
- `Status_SHINOBU_16.md` was absent at session start. No old SHINOBU_16 status data found.
- `Rationale_SHINOBU_16.md` was absent at session start. No old SHINOBU_16 rationale data found.
- Next: source archaeology and task 01-05 implementation.

### Loop 1 - Tasks 01-05

- Added `HazardSourceDTO`, `ThermodynamicsHazardConstants`, pointer grid surface, telemetry entry, and `MockHazardGenerator`.
- Added `ThermodynamicsHazardGridRuntime` with H8Memory persistent buffers, source refs, mock source seeding, and throttled mock/combat signal output.
- Unity batchmode artifact: `Docs/AgentLogs/Build_SHINOBU_16_unity_compile.log`.
- Compile status: `Hecton8.Thermodynamics.dll` was emitted under `Library/Bee/artifacts/1900b0aEDbg.dag`; full Unity script compile is `[BLOCKED BY DEPENDENCY]` by unrelated `Assets/_Project/Scripts/World/FloraGenomics/FloraGenomeContracts.cs` missing `long3` at lines 128 and 467.
- Next: read checklist, re-extract prompt after task 3 boundary, execute tasks 06-10.

### Loop 2 - Tasks 06-10

- Implemented Burst grid diffusion, ping-pong swap, inverse-square emission, mock SDF shielding, and 1 Hz radiation decay.
- Re-extracted `SHINOBU_16` prompt with CLI from `Docs/Tasks/CURRENT_BATCH.md`.
- Verification: direct Thermodynamics csc pass later superseded early full-Unity block.

### Loop 3 - Tasks 11-15

- Implemented trilinear entity sampling, updraft signals, hardware decimation, AUP rebase, and 3D heat texture upload.
- Self-read audited `ThermodynamicsHazardGridRuntime.cs` for managed allocations, collider calls, and cross-domain using directives.

### Loop 4 - Tasks 16-20

- Implemented per-entity damage throttling, black-box telemetry, GlobalDataVault-backed tuner constants, CSV override ingestion, and Vault-backed SceneView gizmos.
- Added `Hecton8.Core.Memory` asmdef reference; removed unnecessary `Hecton8.World` dependency.
- Added local unmanaged `partial MockDamageSignal` in Thermodynamics namespace.

### Loop 5 - Verification / Compile Wall

- `Docs/AgentLogs/Build_SHINOBU_16_thermo_csc_r6.log`: clean, 0 bytes, csc exit code 0 for `Hecton8.Thermodynamics`; emitted `Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.Thermodynamics.dll` and `.ref.dll` at 2026-05-17 20:28:49.
- `Docs/AgentLogs/Build_SHINOBU_16_unity_compile_r3.log`: full Unity compile `[BLOCKED BY DEPENDENCY]` outside SHINOBU by `FloraGenomeContracts.cs` lines 128/529 missing `long3`, `FloraGenomeJobs.cs` line 108 missing `long3`, and `Inventory/Shinobu19EconomyLedger.cs` line 1562 missing `NativeMultiHashMap<,>`.

### Loop 6 - Polish / Final Report

- Searched `Docs/Tasks/CURRENT_BATCH.md` for `<POLISH_MANDATE>` after all tasks were checked; tag was not present.
- Anti-bloat audit found no `SphereCollider`, `OnTriggerStay`, `OverlapSphere`, managed arrays, `List<>`, `System.Linq`, `Hecton8.World` using, TODO, or `NotImplementedException` in Thermodynamics files.
- Appended final report to `Docs/AgentLogs/LOG_SHINOBU_16.md`.

### Loop 7 - Ultra Polish / H-Phi Eviction

- Re-read `Docs/Tasks/CURRENT_BATCH.md`, `Docs/AgentLogs/Rationale_SHINOBU_16.md`, and `Docs/PROJECT_STATE_STATIC_XRAY.md` under the Ultra mandate; task count remains exactly 20.
- Evicted persistent local `NativeArray<T>` ownership from `ThermodynamicsHazardGridRuntime`. Persistent thermodynamics buffers are now `VaultBufferHandle<T>` IDs `(BufferID)70016-70038`; jobs receive raw pointers from resolved Vault views.
- Added cached shader property IDs for heat-grid texture/global metadata; removed string shader property lookup from the visual sync publish path.
- Static audit: no `SphereCollider`, `OnTriggerStay`, `OverlapSphere`, `System.Linq`, `foreach`, runtime `GetComponent`, `FindObjectOfType`, `GameObject.Find`, `H8Memory.Allocate`, `H8Memory.Release`, or `[StructLayout(Pack=1)]` in `Assets/_Project/Scripts/Thermodynamics`. Only `ResolveArray<T>()` remains as a method-scoped Vault view resolver, not a field.
- Targeted compile: `Docs/AgentLogs/Build_SHINOBU_16_thermo_csc_polish_r2.log` is clean, 0 bytes, csc exit code 0 for `Hecton8.Thermodynamics`.
- Full Unity compile was not re-run in this loop to avoid rebuild spam. Last full evidence remains `Docs/AgentLogs/Build_SHINOBU_16_unity_compile_r3.log`, blocked outside SHINOBU by FloraGenomics/Inventory errors documented in Loop 5.

### Loop 8 - Ultra Polish / I/O Pressure + ARM64 Staging

- Re-extracted `SHINOBU_16` with CLI; prompt length 11250 chars and task count remains exactly 20.
- Added `ThermodynamicsHazardGridRuntime.FileWorker.cs`: one persistent background worker thread, MMF read path, sequential stream fallback, timestamp short-circuit, and Vault-backed byte staging. `Tick()` applies ready bytes only; `SlowTick()` only enqueues CSV load requests.
- Converted persistent combat-damage staging away from external `CombatDamageSignal` storage. Vault buffer `(BufferID)70032` now stores local `ThermodynamicsCombatDamageSignal` 64B sequential DTO and converts to `CombatDamageSignal` only at publish.
- Added `.h8dump` fatal telemetry outputs alongside existing `.bin` dumps.
- Signal audit: attempted to switch mock damage to existing `SignalWardenMockDamageSignal`; targeted csc proved it is not available from the referenced compiled contracts in this workspace, so the prompt-required local `MockDamageSignal` remains for blind proof while production damage still publishes `CombatDamageSignal`. This was one failed compile attempt, resolved; no compile wall.
- Static audit: no persistent private `NativeArray<T>` fields, no `new NativeArray`, no `SphereCollider`, no trigger/overlap hazard damage, no `Pack=1` in Thermodynamics, no main-thread config file reads in `Tick()`/`SlowTick()`. Remaining file I/O is background config worker or fatal dump writer.
- Targeted compile: `Docs/AgentLogs/Build_SHINOBU_16_thermo_csc_io_r8.log` is clean, 0 bytes, csc exit code 0 for `Hecton8.Thermodynamics`.

### Loop 9 - Ultra Polish / Stream Fallback Throughput

- Re-read `AGENTS.md`, re-extracted the full `SHINOBU_16` XML prompt with CLI, and re-read task-relevant mandates: `STRM_Async_Standard`, `STRM_DirectStorage_Reality_Check`, `OPT_Zero_GC_Policy_AllocFree_Mandate`, `OPT_Native_Memory_Collections_JobSystem_Protocol`, `ARCH_Signal_Lane_Segregation`, and `MATH_AUP_Determinism_Sync`.
- Replaced the background stream fallback's per-byte `FileStream.ReadByte()` loop with a `Span<byte>` over the existing Vault-backed destination pointer and `FileStream.Read(Span<byte>)` chunks. This keeps MMF as the primary path and keeps the fallback off the main thread without allocating a managed byte array.
- Static audit: no `ReadByte`, no `File.ReadAll`, no collider/trigger/overlap hazard damage, no `System.Linq`, no `foreach`, no runtime `GetComponent`/`FindObjectOfType`, no `Material.SetFloat`, no `Instantiate`, no `[StructLayout(Pack=1)]`, no `new NativeArray`, and no persistent private `NativeArray<T>` fields in Thermodynamics. The remaining `ResolveArray<T>()` hit is a method-scoped Vault view resolver.
- Targeted compile: `Docs/AgentLogs/Build_SHINOBU_16_thermo_csc_io_r9.log` is clean, 0 bytes, csc exit code 0 for `Hecton8.Thermodynamics`.

### Loop 10 - Ultra Polish / Low-Tier Visual Bandwidth

- Added `LowTierVisualUploadStride = 4` in VISUAL_SYNC. The 16^3 toaster path keeps gameplay simulation every scheduled solve but uploads the heat-haze `Texture3D` only every fourth dirty grid version unless the texture must be rebuilt for a resolution change.
- High-tier 32^3 visual upload cadence is unchanged. This is a Dear Lie visual throttle only; scalar hazard truth, trilinear entity sampling, damage, telemetry, and signals still use the latest front grid after each simulation swap.
- Static audit after the visual gate: no collider/trigger/overlap hazard damage, no `ReadByte`, no `System.Linq`, no `foreach`, no runtime find/get component calls, no `Material.SetFloat`, no `Instantiate`, no `[StructLayout(Pack=1)]`, no `new NativeArray`, and no persistent private `NativeArray<T>` fields in Thermodynamics. The remaining `ResolveArray<T>()` hit is a method-scoped Vault view resolver.
- Targeted compile: `Docs/AgentLogs/Build_SHINOBU_16_thermo_csc_visual_r10.log` is clean, 0 bytes, csc exit code 0 for `Hecton8.Thermodynamics`.

### Loop 11 - Ultra Polish / Phase-Safe Readback

- Re-read `ARCH_Execution_Phases` and `OPT_Native_Memory_Collections_JobSystem_Protocol`. The audit found `TrySample()` and editor Vault readback could call `CompleteForColdReadbackIfIdle()`, which routed into `LateFrameTick()` and therefore could swap buffers, publish signals, commit telemetry, and upload visuals from a read API.
- Removed `CompleteForColdReadbackIfIdle()` entirely. `TrySample()` and editor readback now read the stable front-buffer snapshot only; completed simulation work is resolved only by registered `LateFrameTick()` or teardown.
- Remaining `.Complete()` calls in Thermodynamics are now limited to `LateFrameTick()` POST_SIMULATION swap window and `ReleaseNativeState()` teardown.
- Static audit after phase readback polish: no collider/trigger/overlap hazard damage, no `ReadByte`, no `System.Linq`, no `foreach`, no runtime find/get component calls, no `Material.SetFloat`, no `Instantiate`, no `[StructLayout(Pack=1)]`, no `new NativeArray`, and no persistent private `NativeArray<T>` fields in Thermodynamics. The remaining `ResolveArray<T>()` hit is a method-scoped Vault view resolver.
- Targeted compile: `Docs/AgentLogs/Build_SHINOBU_16_thermo_csc_phase_r11.log` is clean, 0 bytes, csc exit code 0 for `Hecton8.Thermodynamics`.

### Loop 12 - Ultra Polish / Front-Only Unsafe Pointer Surface

- Audited `TryGetUnsafeGridPointers()` against the double-buffer rule. The method exposed owner back-buffer pointers even though no current source usage requires them.
- Changed public unsafe pointer readback to expose `TemperatureFront` and `RadiationFront` only. `TemperatureBack` and `RadiationBack` fields remain in the DTO for internal/legacy shape compatibility but are left null by the public read API. Owner jobs still receive back-buffer pointers through private scheduling code only.
- Targeted compile: `Docs/AgentLogs/Build_SHINOBU_16_thermo_csc_frontonly_r12.log` is clean, 0 bytes, csc exit code 0 for `Hecton8.Thermodynamics`.

### Loop 13 - Ultra Polish / AUP Telemetry + Source Ref Containment

- Audited source mutation and AUP telemetry. `GetHazardSourceRef()` was public even though only internal mock seeding used it; external mutation during active jobs would race `EmissionJob`.
- Changed `GetHazardSourceRef()` to private and implemented it through `UnsafeUtility.AsRef` over the Vault pointer. External producers keep the guarded `TryUpsertSource()` path, which returns false while simulation jobs are active.
- Replaced blackbox `GridOrigin = (float3)_gridOriginAup` with `GridOrigin = float3.zero` plus `GridOriginHash`, a millimeter-quantized hash of the `double3` AUP. This removes the last absolute AUP-to-float cast in Thermodynamics; the remaining `float3` cast is a local delta for damage `WorldPoint`.
- Static audit: no public `ref HazardSourceDTO`, no `(float3)_gridOriginAup`, no stale `Padding0`, no collider/trigger/overlap hazard damage, no `ReadByte`, no `Pack=1`, no `new NativeArray`, and no persistent private `NativeArray<T>` fields in Thermodynamics. The remaining `ResolveArray<T>()` hit is a method-scoped Vault view resolver.
- Targeted compile: `Docs/AgentLogs/Build_SHINOBU_16_thermo_csc_aup_r13.log` is clean, 0 bytes, csc exit code 0 for `Hecton8.Thermodynamics`.

### Loop 14 - Ultra Polish / Blackbox Stride Integrity

- Audited blackbox binary serialization against `UnsafeUtility.SizeOf<ThermodynamicsHazardTelemetryEntry>()`. The DTO declared 64B, but the dump writer serialized 56B of explicit fields.
- Added explicit telemetry tail pads `_pad0` and `_pad1` and writes both fields to every `.bin`/`.h8dump` telemetry row. The binary stride now matches the declared 64B entry size.
- `ScanTelemetryJob` zeroes the new pad fields in Burst, keeping deterministic dump bytes.
- Re-extracted `SHINOBU_16` prompt exactly with CLI after the change; prompt length 11250 chars and task count remains 20.
- Static audit: no `ReadByte`, no collider/trigger/overlap hazard damage, no `Pack=1`, no public `ref HazardSourceDTO`, no `(float3)_gridOriginAup`, no stale `Padding0`, no `new NativeArray`, and no persistent private `NativeArray<T>` fields in Thermodynamics. The remaining `ResolveArray<T>()` hit is a method-scoped Vault view resolver.
- Targeted compile: `Docs/AgentLogs/Build_SHINOBU_16_thermo_csc_blackbox_r14.log` is clean, 0 bytes, csc exit code 0 for `Hecton8.Thermodynamics`.

### Loop 15 - Ultra Polish / Scalability Signal + Directed Updraft Flux

- Audited Task 13 against `ARCH_Global_Registry_ServiceLocator_DI_Init`. `UsesLowResolution()` polled `GlobalRegistry.ScalabilityTier` from the Tick path.
- Added cached scalability tier state through `IScalabilityChangedEventListener` and `ScalabilityEvents`. `GlobalRegistry.ScalabilityTier` is now read only during cold registration refresh; Tick uses `_cachedScalabilityTier`.
- Added typed `SignalBus<SystemHealthIndexSignal>` consumption. Critical/adrenaline pressure latches the runtime into the 16^3 path for 120 frames and telemetry marks `TelemetryFlagHealthPressureLowTier`.
- Reworked vertical heat diffusion to directional flux: gain from below and loss upward use the stronger coefficient; gain from above and loss downward use the weaker coefficient. This better matches Task 12's "hot water rises" rule without adding fluid simulation.
- Static audit: no collider/trigger/overlap hazard damage, no `ReadByte`, no `Pack=1`, no public `ref HazardSourceDTO`, no `(float3)_gridOriginAup`, no `new NativeArray`, and no persistent private `NativeArray<T>` fields in Thermodynamics. The remaining `ResolveArray<T>()` hit is a method-scoped Vault view resolver. The remaining `GlobalRegistry.ScalabilityTier` hit is cold registration refresh, not the Tick resolution loop.
- Targeted compile: `Docs/AgentLogs/Build_SHINOBU_16_thermo_csc_scalability_r15.log` is clean, 0 bytes, csc exit code 0 for `Hecton8.Thermodynamics`.

### Loop 16 - Ultra Polish / Editor AUP Facade

- Re-audited Task 20 after the runtime AUP cleanup. `ThermodynamicsTunerWindow` still cast the absolute `double3` grid origin into a Unity `Vector3` for SceneView gizmos.
- Changed the gizmo visualizer to draw the Vault mirror in local macro-grid coordinates and discard absolute origin from readback. Runtime AUP remains `double3`; editor debug no longer demonstrates an unsafe 100km-world float cast.
- Re-extracted `SHINOBU_16` prompt with CLI; prompt length 11250 chars and task count is 20 using `Task\s+\d{2}:`.
- Static audit: no `(float)originAup`, no `new Vector3((float)`, no `(float3)_gridOriginAup`, no collider/trigger/overlap hazard damage, no `ReadByte`, no `Pack=1`, no public `ref HazardSourceDTO`, no `new NativeArray`, and no persistent private `NativeArray<T>` fields in Thermodynamics. The remaining `ResolveArray<T>()` hit is a method-scoped Vault view resolver.
- Targeted compile: `Docs/AgentLogs/Build_SHINOBU_16_thermo_csc_editoraup_r16.log` is clean, 0 bytes, csc exit code 0 for `Hecton8.Thermodynamics`.

