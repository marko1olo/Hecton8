# SHINOBU_151 Status - Dynamic Point Light Culling Director

Status: POLISH STATIC PASS / COMPILE BLOCKED BY CPU GATE
Domain: Echelon 7 Graphics & Lighting
Task Count: 20
Route Blocker Improved: First 20 minutes abyss/habitat traversal no longer needs hundreds of Unity `Light` objects for abyss proxy emission.

## Compile Gate

- Guard check 2026-05-19 latest: CPU load `99` after prior `100`.
- `dotnet`/`csc` process check: none running.
- Action: no `dotnet build` launched. This obeys the explicit CPU gate. Compile/runtime proof remains pending.

## Project-Scale Light Archaeology

- Static scan found no `LightDistanceCull` script and no matching `Vector3.Distance` distance-cull offender tied to lights.
- Static scan found legacy gameplay-owned Unity `Light` toggles in `PlayerFlashlight.cs`, `RepairTool.cs`, `Gameplay/DeployableFlare.cs`, `Gameplay/GravTrap.cs`, and `Visor/HectonFlashlightVoxelShadowProvider.cs`.
- Static scan found `13` authored Light YAML components under `Assets/_Project` and `375` LODGroup YAML hits. These are not SHINOBU_151-owned source files and were not deleted in a dirty multi-agent workspace.
- Migration route is now explicit: those owners must publish `DynamicPointLightSourceDTO` rows and commit SourceManifest buffer `71458`; this culler owns only the mathematical survivor selection and GPU payload.

## Mandates Read

- `AGENTS.md`
- `Docs/Actual Domains of Project.txt`
- `.agents-skills/REND_Abyssal_Lighting_Voxel_Occlusion_Shadows.txt`
- `.agents-skills/REND_URP_Graphics_HotPath_Optimization_HLOD.txt`
- `.agents-skills/OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `.agents-skills/OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `.agents-skills/DATA_Runtime_Struct_Layout_ARM64.txt`
- `.agents-skills/MATH_AUP_Determinism_Sync.txt`
- `.agents-skills/DBG_Telemetry_Crash_Reporting_PostMortem.txt`
- `.agents-skills/ARCH_Execution_Phases.txt`

## Loop 1 - Tasks 01-05

- [x] Task 01 MONOBEHAVIOUR_CULLING_ERADICATION | DOD used: owned runtime static scan has no `Light.enabled`, `new Light`, or `Vector3.Distance`; route submits DTOs, not components. Rejected: deleting unrelated player/tool light scripts outside domain. Estimate: avoids roughly 35 us per 5000-light frame in Unity Light state churn plus unbounded renderer rebuild risk. Evidence: STATIC PASS; compile blocked by CPU gate.
- [x] Task 02 UNITY_LOD_GROUP_LIGHT_PURGE | DOD used: no LODGroup-owned light disabling becomes authoritative; culling control is in Vault data and GPU payload limits. Rejected: mutating mesh/world LOD systems outside lighting domain. Estimate: avoids renderer-side light list rebuild spikes. Evidence: STATIC PASS; compile blocked by CPU gate.
- [x] Task 03 CS1612_ENCAPSULATION_PURGE | DOD used: DTO contracts expose public fields only; static scan of contracts/jobs has no `get;`/`set;`. Rejected: properties over NativeArray elements. Estimate: removes defensive-copy hazard in Burst loops. Evidence: STATIC PASS; compile blocked by CPU gate.
- [x] Task 04 ARM64_PADDING_RECONSTRUCTION | DOD used: `LightCullStateDTO` is `[StructLayout(LayoutKind.Explicit, Size = 32)]` with offsets `0/4/8/12/16` and pad bytes `20..31`; editor test covers offsets. Rejected: sequential layout and `Pack=1`. Estimate: 5000 states = 160 KB aligned stream. Evidence: STATIC PASS; compile blocked by CPU gate.
- [x] Task 05 EMERGENCY_MOCK_LIGHT_DATA | DOD used: `GenerateMockLightCullingDataJob` writes 5000 deterministic sources/states into Vault and locks/unlocks source/state buffers with `try/finally`. Rejected: waiting on base-building lamps or creating GameObjects. Estimate: isolated stress source with zero Unity-object overhead. Evidence: STATIC PASS; compile blocked by CPU gate.

## Loop 2 - Tasks 06-10

- [x] Task 06 BURST_FRUSTUM_CULLING_KERNEL | DOD used: `EvaluateLightCullingJob` is Burst synchronous fast/standard and `[NoAlias]`; it subtracts camera AUP before casting to `float3`; frustum planes are extracted from VP matrix without managed `Plane[]`. Rejected: absolute float world positions and `GeometryUtility.CalculateFrustumPlanes`. Estimate: 5000-source worker pass bounded by native arrays. Evidence: STATIC PASS; compile blocked by CPU gate.
- [x] Task 07 SQUARED_DISTANCE_INTENSITY_LOD | DOD used: distance fade uses `math.lengthsq` and squared thresholds; owned hot path has no `math.sqrt` or `Vector3.Distance`. Rejected: sqrt distance checks. Estimate: saves one sqrt per evaluated light. Evidence: STATIC PASS; compile blocked by CPU gate.
- [x] Task 08 SDF_OCCLUSION_BAKING | DOD used: fixed four-sample SDF gate forces intensity to zero when blocked; mock SDF shell is sqrt-free. Rejected: CPU ray tracing, Physics.Raycast, `math.length`, and per-fragment CPU raymarching. Estimate: O(4N) scalar samples instead of scene queries. Evidence: STATIC PASS; compile blocked by CPU gate.
- [x] Task 09 LIGHT_IMPORTANCE_SORTING | DOD used: `SortLightImportanceJob` radix sorts uint keys with stack buckets and scratch arrays. Rejected: managed sort, LINQ, comparer delegates. Estimate: O(4N) sort over 5000 keys. Evidence: STATIC PASS; compile blocked by CPU gate.
- [x] Task 10 THE_DEAR_LIE_DEFERRED_SUBMISSION | DOD used: top survivors are written to prewarmed double-buffered `GraphicsBuffer.LockBufferForWrite`; no Unity `Light` creation/toggle path. Rejected: component enable/disable and first-upload buffer allocation. Estimate: caps shader loop to 8..64 lights instead of 5000 submissions. Evidence: STATIC PASS; compile blocked by CPU gate.

## Loop 3 - Tasks 11-15

- [x] Task 11 CONTINUOUS_SCALABILITY_LIGHT_LIMIT | DOD used: `ResolveMaxActiveLights` maps `GlobalQualityWeight` and thermal pressure continuously from 8 to 64; cadence also lerps 5 Hz to 60 Hz. Rejected: low/high binary quality branch. Estimate: shader loop bound collapses under thermal pressure. Evidence: STATIC PASS; compile blocked by CPU gate.
- [x] Task 12 AUP_PRECISION_FRUSTUM_PLANES | DOD used: camera frustum planes are shifted into camera-local space before Burst plane tests against AUP-local light offsets. Rejected: absolute float plane tests. Estimate: prevents far-map precision false culls. Evidence: STATIC PASS; compile blocked by CPU gate.
- [x] Task 13 ROLLBACK_NETCODE_STATE_FENCE | DOD used: edit test scans rollback contracts for absence of dynamic-light DTO/payload names; route card declares presentation-only ownership. Rejected: hashing visual culling in Merkle truth. Estimate: rollback bandwidth unchanged. Evidence: STATIC PASS; compile blocked by CPU gate.
- [x] Task 14 ZERO_INIT_OVERHEAD_BYPASS | DOD used: large fully-overwritten Vault buffers request `NativeArrayOptions.UninitializedMemory`; tiny counters/cursor, source manifest, and the forensic telemetry ring use clear memory; source count now comes from committed Vault manifest `71458`, so uncommitted source/SDF buffers publish count `0` instead of reading garbage. Rejected: bulk zero-fill for source, state, sort, payload, SDF streams; also rejected private active-count authority and uninitialized forensic dumps. Estimate: avoids load/init memset over large streams while keeping blackbox sane. Evidence: STATIC PASS; compile blocked by CPU gate.
- [x] Task 15 TELEMETRY_CULLING_RECORDER | DOD used: 300-entry 64-byte telemetry ring plus `Dump_LIGHT_DIRECTOR.bin` writer; ring is cold-cleared to avoid dumping uninitialized bytes before 300 frames have elapsed; timeout flag is latched and written to counters only after the active job completes; dump is throttled to once per scheduled job. Rejected: debug strings, garbage initial blackbox records, repeated timeout IO, and main-thread writes to counters while jobs are active. Estimate: 19.2 KB fixed ring, one aggregate write per completed frame. Evidence: STATIC PASS; compile blocked by CPU gate.

## Loop 4 - Tasks 16-20

- [x] Task 16 CULLING_TUNER_EDITOR_WINDOW | DOD used: UI Toolkit tuner exposes quality, fade, importance, SDF threshold, mock generation, CSV reload, dump, and numeric readout without per-refresh label concatenation. Rejected: runtime Canvas and IMGUI churn. Estimate: editor-only; runtime 0 us. Evidence: STATIC PASS; compile blocked by CPU gate.
- [x] Task 17 CSV_CULLING_PROFILES_INGESTOR | DOD used: byte scratch parser hashes names with FNV-1a and writes unmanaged profile rules. Rejected: `string.Split`, managed lists, LINQ. Estimate: cold/editor path only. Evidence: STATIC PASS; compile blocked by CPU gate.
- [x] Task 18 LIVE_FRUSTUM_DEBUG_GIZMO | DOD used: `OnDrawGizmos` draws green/yellow/red wire cubes from Vault states/sources without marker GameObjects. Rejected: scene debug objects. Estimate: editor-only; runtime guarded. Evidence: STATIC PASS; compile blocked by CPU gate.
- [x] Task 19 DYNAMIC_LIGHT_BOUNCE_INJECTION | DOD used: top survivors become `CustomDynamicProbeLightDTO` records in Vault buffer `71454`; `TryGetProbeBounceReadback` exposes the owner-local stream for the probe-grid owner without scheduling a cross-owner blocking job. Rejected: realtime GI, ray tracing, Unity GI, and direct `InjectDynamicLightJob.Complete()` from the culling director. Estimate: bounded by submitted count 8..64. Evidence: STATIC PASS; compile blocked by CPU gate.
- [x] Task 20 SELF_AUDIT_AND_ARCHITECTURE_VERIFICATION | DOD used: route card, ledger entry, rationale, editor tests, static scans, and final XML log are present or pending final append. Rejected: chat-only report. Estimate: no runtime cost. Evidence: STATIC PASS; compile blocked by CPU gate.

## Loop 5 - Self-Read Audit

- [x] Re-read own code for GC allocations in Tick/LateFrame. Result: hot path uses Vault arrays, manual VP frustum extraction, and GPU buffer mapping; cold file IO and GraphicsBuffer allocation remain outside per-light jobs.
- [x] Re-read own code for stale Vault handle and alias overlap. Result: culling job locks owned buffers, unlocks after completed handle; mock seed locks use `try/finally`; Burst job arrays are `[NoAlias]`; no cross-owner probe job is completed in late frame; native ready gate now requires every declared SHINOBU_151 Vault handle.
- [x] Re-read own code for AUP subtraction before float math. Result: Evaluate and GPU payload jobs subtract `Settings.CameraAup` before float math.
- [x] Re-read own code for binary quality switches. Result: static scan found no low/high branch in owned culling runtime; quality is continuous.
- [x] Re-read own code for unverified compile assumptions. Result: compile not run because CPU gate is red at `100`; status remains not runtime-complete. Additional polish caught and fixed unseeded SDF/source reads, managed frustum scratch, hidden mock sqrt, direct probe injection, missing Unity `.meta` files, and the private active-source-count gap by adding `DynamicPointLightSourceManifestDTO[1]` in Vault buffer `71458`.
- [x] Re-read own code for `JobHandle.Complete()` abuse. Result: VISUAL_SYNC calls `Complete()` only after `IsCompleted`; timeout handling does not block; remaining same-method fences are cold/editor mock generation and teardown lock drain, now annotated in source and rationale.
- [x] Re-read own GPU upload path. Result: `GraphicsBuffer.LockBufferForWrite` copy now unlocks in `finally`, and shader scalar vectors are filled from `default` values instead of constructor syntax in VISUAL_SYNC.
- [x] Re-read settings DTO NaN ingress. Result: every serialized scalar entering `DynamicPointLightCullingSettingsDTO` now passes a finite fallback/clamp before any Burst job or shader constant sees it.
- [x] Re-read hot DTO access against the assignment's raw-pointer mandate. Result: source/state/GPU/probe/counter writes in the Burst culling jobs now route through `NativeArrayUnsafeUtility` + `UnsafeUtility.AsRef`; DTOs remain public-field-only and job arrays remain `[NoAlias]`.
- [x] Re-read editor/debug readback authority. Result: `TryGetStatesReadback` reports count from committed SourceManifest `71458`, not `_activeSourceCount`, so external writers do not leave the gizmo/tuner path on a stale private mirror.
- [x] Re-read continuous quality math against the polish mandate. Result: active light budget now uses `math.lerp`, `math.step`, and a smooth polynomial curve; thermal pressure is also polynomial, not a binary low-end switch.
- [x] Re-read scheduler fail-closed path. Result: `ScheduleCullingPipeline` now checks every required NativeArray `IsCreated` before reading any `Length`, then clamps count; missing Vault lanes return without touching uninitialized memory.
