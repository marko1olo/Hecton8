# SHINOBU_225 Rationale

Date: 2026-05-20
Agent: SHINOBU_225
Role: LASER_CUTTER_DOD_REWRITE
Status: PENDING VERIFICATION

## Decision 000: Scope And Mandate Selection

Problem: Laser cutter task crosses tools, physics queries, AUP precision, VFX staging, DataVault/global authority, and telemetry. Editing without narrowing the mandate set creates refactor spread.

Solution: Read and apply eight task-relevant mandates: tools/raycast/heat, ARM64 layout, zero-GC, native memory/jobs, visual fake first, AUP precision, execution phases, and signal lane segregation. Treat `SHINOBU_225` XML block as the only batch directive.

Rejected Alternatives: Reading the full mandate registry would pollute context and violate the 2-8 mandate rule. Relying on memory would miss current R45 global authority boundaries.

Scalability potential: Low/MX350 gets bounded request counts and shader fakes. Middle keeps full deterministic cutter truth at normal cadence. High increases debug/telemetry density only after budget proof. Ultra spends saved CPU on richer GPU sparks/deformation, not more simulation truth.

Hardware Impact: Expected low-end gain comes from removing synchronous physics stalls and prefab burst churn. Exact i3/MX350 gain is PENDING PROFILER; static estimate range is 40-3000 us depending on old call site.

## Decision 001: Product Route Relevance

Problem: A laser cutter rewrite could become isolated tech work with no first-20-minutes relevance.

Solution: Scope it as removal of an equipment-route blocker: cutter interaction against salvage/module surfaces must not stall the Copper Wire/tool route through sync physics or prefab spawn spikes.

Rejected Alternatives: Building broad global systems unrelated to the route. New global authority surfaces remain blocked unless existing source already provides lanes/buffers or the work is owner-local.

Scalability potential: Same gameplay truth across devices, presentation weight scales by `GlobalQualityWeight`.

Hardware Impact: Prevents low-end frame hitch during repeated cutter impacts; exact microseconds PENDING PROFILER.

## Decision 002: Existing Raycast Backend Boundary

Problem: The batch prompt names synchronous cutter `Physics.Raycast` as the threat, but source archaeology shows `LaserCutter.TryGetCutHit` already routes through `IInteractionSignalService.TryRaycastPrimary`, and `EquipmentInteractionHandler` schedules `RaycastCommand.ScheduleBatch` later instead of blocking the cutter frame.

Solution: Keep the existing equipment interaction raycast owner as the live route and add SHINOBU-owned cutter DTO/jobs/tooling around it for deterministic profiling, staged hit evaluation, telemetry, and future dispatcher binding. Do not create a second gameplay raycast dependency in `LaserCutter`.

Rejected Alternatives: Replacing `TryRaycastPrimary` with a parallel static scheduler would duplicate raycasts, double physics query load, and create a race with the interaction owner. Forcing direct `Physics.RaycastNonAlloc` would violate the mandate and regress the already-deferred route.

Scalability potential: Low devices continue using one deferred interaction ray per cutter frame. Middle/high/ultra can run SHINOBU mock and visual-overkill evaluation buffers for richer presentation without changing gameplay truth.

Hardware Impact: Avoids adding an estimated 40-120 us duplicate physics query on i3/MX350-class hardware while preserving the no-blocking raycast route.

## Decision 003: Sparks As Signals, Not ParticleSystem Mutation

Problem: `LaserCutter.UpdateSparks` still moves a serialized `ParticleSystem`, changes emission rate, and calls `Play/Stop` during active cutting. Even without `Instantiate`, this is component-state churn in the tool hot path and does not guarantee GPU-only visual staging.

Solution: Replace the hot spark operation with a `DebrisSpawnSignal`/tool spark signal path using absolute hit AUP and continuous `GlobalQualityWeight` intensity/count scaling. Remove cutter-adjacent `ParticleSystem` references in `LaserCutter`, `SealedDoor`, and `SargassumCutResponder` so focused cutter scans show zero particle-system code.

Rejected Alternatives: Pooling or rate-tuning the existing `ParticleSystem` keeps CPU component writes in the cutter loop. Spawning prefab spark bursts is explicitly forbidden. Keeping inert serialized fields was rejected after static scan proved they still leave cutter-focused ParticleSystem evidence.

Scalability potential: Low quality collapses spark quantity to zero GPU requests. Middle/high raises density smoothly. Ultra spends saved CPU on up to 500 GPU spark/debris requests and shader glow, not extra physical particles.

Hardware Impact: Expected low-end gain is 20-150 us during cutter impact frames by removing transform/emission/Play/Stop calls; exact profiler proof remains pending.

## Decision 004: Owner-Local Buffer IDs

Problem: New cutter DOD buffers need DataVault residency, but extending the global `BufferID` enum is a cross-domain authority change that can collide with parallel agents.

Solution: Use a SHINOBU owner-local buffer ID block cast to `BufferID`, mirroring existing scoped systems such as ballistics and procedural wreckage. Document IDs in contracts and keep them inside the Tools domain.

Rejected Alternatives: Editing `H8Memory.cs` would enlarge global authority for a tool-local feature. Allocating persistent `NativeArray` fields outside DataVault violates the native memory mandate.

Scalability potential: Low/middle/high/ultra share fixed capacities with bounded request/result buffers; only quality scalar and staged visual counts vary.

Hardware Impact: Fixed vault buffers avoid allocator churn. Expected i3/MX350 benefit is stability rather than a single measurable microsecond win.

## Decision 005: Shader Deformation Truth Boundary

Problem: Cutter impact wants visible molten scars and hull dents, but CPU mesh mutation or runtime mesh rebuilds are over budget for an equipment hot path.

Solution: `EvaluateCutterRaycastHitsJob` writes `LaserCutDeformationStateDTO`, `LaserCutGlowDecalRequestDTO`, and `LaserCutImpactVfxDTO` only. The deformation is a shader/decal contract: center AUP, normal, radius, depth, heat, and progress. Geometry truth remains outside the cutter.

Rejected Alternatives: Runtime vertex edits and `RecalculateNormals` are main-thread hazards. Mesh scar prefabs would add object lifetime churn. A binary low/high visual switch was rejected; spark count, glow, and dent radius consume continuous `GlobalQualityWeight`.

Scalability potential: Low uses tiny dent radius and zero-to-low GPU spark quantity. Middle uses standard glow/decal density. High and Ultra increase radius/lifetime/spark density through continuous weights without changing gameplay damage truth.

Hardware Impact: Avoids estimated 300-3000 us mesh mutation spikes on i3/MX350-class devices. Actual gain remains PENDING PROFILER.

## Decision 006: Editor And Report Scope

Problem: Designers need tuning and static evidence, but runtime UI or manual grep would either cost gameplay frames or fail repeatability.

Solution: Add editor-only UI Toolkit tuner, editor gizmo, and `Cutter_Raycast_Inquisition` static report writer. Runtime remains DataVault/signal based; editor code is isolated in `Editor` folders or `UNITY_EDITOR` guards. Write a SHINOBU sidecar report and add a non-destructive appendix field to the shared construction report.

Rejected Alternatives: In-game debug UI was rejected because it adds player-route cost. IMGUI was rejected because the mandate and existing tuner pattern use UI Toolkit. Full rewrite of another agent's shared construction report was rejected; SHINOBU adds only an appendix and keeps the detailed report in a sidecar file.

Scalability potential: No runtime cost on any tier. Top-tier devices only gain optional editor visualization density during development.

Hardware Impact: Runtime impact is zero in builds. Editor-only overhead is irrelevant to i3/MX350 gameplay budgets.

## Decision 007: Request ABI Padding Correction

Problem: The first implementation used offsets 52/56/60 of `LaserCutRequestDTO` for Frame, Flags, and RequestSequence. The XML contract explicitly reserves bytes 52-63 as padding. That made the ABI technically 64 bytes but semantically wrong for blind MemCpy/layout proof.

Solution: Restore `LaserCutRequestDTO` to the exact mandated shape: `double3 RayOriginAUP@0`, `float3 RayDirection@24`, `float CuttingPower@36`, `float MaximumDistance@40`, `uint ToolHashID@44`, `uint ParentEntityID@48`, and explicit `_pad0/_pad1/_pad2` at 52/56/60. Move frame/flags/sequence/cooldown metadata into a separate 64-byte `LaserCutRequestMetaDTO` stored in owner-local `RequestMetaBuffer=71336`.

Rejected Alternatives: Keeping metadata inside padding was rejected because it violates the exact batch ABI. Shrinking metadata to a 16-byte row was rejected because a 64-byte row avoids false-sharing ambiguity and keeps request/meta lanes cache-line regular under parallel writes.

Scalability potential: Low devices get predictable cache-line fetches and no unaligned metadata reads. Middle/high/ultra keep the same truth buffer but can use the meta lane for richer telemetry/cooldown proofs without touching the request ABI.

Hardware Impact: Expected i3/MX350 gain is structural, not a measured frame win: fewer cache-line surprises and exact ARM64-aligned row loads. Profiler proof remains pending behind CPU/build guard.

## Decision 008: Hot Resolve Only, Cold Acquire Only

Problem: Live cutter staging and scheduled raycast setup could still fall back to `GlobalRegistry.DataVault` or acquire Vault handles if bootstrap was missed. That is a hidden hot-path authority lookup/allocation hazard.

Solution: Keep `EnsureInitialized()` as the cold bootstrap/acquire gate. Live `QueueLiveRequest`, `TryScheduleRaycastBatch` after bootstrap, evaluation finalization, and GPU VFX staging now resolve already-acquired handles with `allowAcquire:false`. If the Vault route is not booted, the hot path fails closed instead of polling GlobalRegistry or calling `GetGenerationHandle`.

Rejected Alternatives: Reacquiring handles opportunistically during tool fire was rejected because it hides boot failures inside gameplay. Caching private `NativeArray` aliases was rejected because Vault sovereignty requires generation handles plus method-local resolved views.

Scalability potential: Low devices avoid metadata traffic during repeated trigger frames. Middle/high/ultra keep the same route; extra visual density comes from cached quality weights, not additional authority lookups.

Hardware Impact: Expected low-end gain is removal of unpredictable metadata/registry stalls in the cutter path; exact microseconds remain PENDING PROFILER.

## Decision 009: Door VFX Decoupling

Problem: `SealedDoor` temporarily called `LaserCutterDodRuntime.StageGpuSparkSignal`, creating a Gameplay -> Tools source dependency for a generic door spark effect.

Solution: Replace that call with a local `DebrisSpawnSignal` publisher using door-local species hashes and continuous `HomeostasisBrain.GlobalQualityWeight` quantity scaling. Tools owns cutter DTOs; Gameplay door owns its prop VFX signal.

Rejected Alternatives: Keeping the Tools call was rejected as cross-domain coupling. Reintroducing ParticleSystem sparks was rejected by Task 02.

Scalability potential: Low collapses door spark quantity toward zero; middle/high/ultra increase GPU spark/debris quantity continuously without a binary quality branch.

Hardware Impact: Keeps door-cut feedback on the signal/GPU path and avoids direct tool-runtime coupling. Microsecond proof pending.

## Decision 010: Exact Spark Continuum And Live Tuning Consumption

Problem: Task 11 explicitly requires GPU spark presentation to scale from 0 on minimum quality to 500 on Ultra, and the Editor Facade must tune the actual Burst evaluation math. A fixed 8..128 constant range and hardcoded dent/glow/battery values leave part of the mandate decorative.

Solution: Set `LowSparkCount=0`, `UltraSparkCount=500`, feed tuning `LowSparkCount`, `UltraSparkCount`, `SparkIntensityScale`, `DentRadiusMinMeters`, `DentRadiusMaxMeters`, `GlowLifetimeSeconds`, and `BatteryWattsAtPowerOne` into `EvaluateCutterRaycastHitsJob`, and smooth presentation with `math.smoothstep(GlobalQualityWeight)`.

Rejected Alternatives: Keeping the old 128 cap was rejected because it fails the XML ceiling. Applying tuning only in the editor was rejected because designers would see controls that do not affect the runtime job. Binary tier thresholds were rejected because HECTON requires continuous quality shedding.

Scalability potential: Low collapses spark quantity to zero and keeps shader deformation at minimal radius. Middle raises density and lifetime gradually. High/Ultra spends saved CPU on up to 500 GPU-only spark requests, larger shader dent radius, longer glow, and richer decal presentation without changing simulation truth.

Hardware Impact: Low-end silicon avoids visual request pressure under thermal clamp; Ultra routes visual overkill to GPU lanes. Exact i3/MX350 microseconds remain PENDING PROFILER.

## Decision 011: Telemetry Tail Converted To Burst Work Estimate

Problem: The black-box ring recorded battery watts but did not expose even a deterministic proxy for Burst work intensity, leaving Task 15 weaker than the forensic mandate.

Solution: Replace the final telemetry tail reserve at byte 124 with `BurstWorkEstimateMicros`, derived inside `EvaluateCutterRaycastHitsJob` from hit state, quality curve, and spark count. The UI Toolkit tuner now displays the estimate as `Burst us`.

Rejected Alternatives: Measuring wall-clock time inside Burst was rejected because the job has no deterministic timer and such timing would break rollback consistency. Editor-only profiler markers were rejected as the sole black-box source because dumps must survive runtime fault capture.

Scalability potential: Low estimates shrink with zero spark count; Ultra estimates rise with 500-count visual pressure, giving QA a stable proxy for quality-weight stress.

Hardware Impact: No extra allocation and one uint write per telemetry row. Profiler proof remains pending.

## Decision 012: Preserve Job-Computed Spark Count At Signal Boundary

Problem: `EvaluateCutterRaycastHitsJob` computed tuned `SparkCount`, but `PublishImpactSignals` routed the DTO through the live staging helper, which recalculated quantity from cached quality and intensity. That silently discarded tuning `SparkIntensityScale` and the exact 0..500 job result.

Solution: Split signal publication into `PublishGpuSparkSignals`. Live `StageGpuSparkSignal` computes direct tool presentation quantity from the same no-acquire Vault tuning fields (`LowSparkCount`, `UltraSparkCount`, `SparkIntensityScale`) and stages an impact DTO. Post-evaluation publishing now forwards `LaserCutImpactVfxDTO.SparkCount` directly to `DebrisSpawnSignal.Quantity` and disables VFX-buffer restaging.

Rejected Alternatives: Leaving the recalculation was rejected because it makes the Burst DTO a lie. Adding another DTO just for signal publication was rejected because the existing impact VFX row already carries the required scalar.

Scalability potential: Low remains zero requests. Middle/high/ultra preserve designer-authored spark scale and the full 500-count Ultra ceiling through the actual signal boundary.

Hardware Impact: Removes one redundant VFX-buffer write per published impact and keeps signal emission deterministic from the completed Burst row. Exact microseconds remain PENDING PROFILER.

## Decision 013: Pure Readers And Cold Scheduler Binding

Problem: The continuation mandate tightened Global Systems Doctrine: `TryGet*`, `Resolve*`, and `Read*` accessors must be pure. SHINOBU runtime still had public `TryGetTuning`, `TryGetLatestTelemetry`, and gizmo readers calling `EnsureInitialized()`, and `TryGetTuning` could seed default tuning as a hidden write. A second defect was structural: cold boot acquired only core request lanes, while the simulation scheduler later used `allowAcquire:false` for command/result lanes that might not have been bound yet.

Solution: Public readers now only resolve already-bound handles with `allowAcquire:false` and return false if boot has not provided the lane. Default tuning seeding moved into cold `EnsureInitialized()` through `EnsureTuningSeeded`. Cold boot now binds scheduler, hit, deformation, battery, decal, impact VFX, telemetry, request, and meta lanes before hot scheduling. The foreign scalability-state handle is cached during cold boot only; hot quality refresh resolves the cached handle or falls back to `HomeostasisBrain.GlobalQualityWeight` without a hot `TryGetGenerationHandle`.

Rejected Alternatives: Keeping hidden reader bootstrap was rejected because it violates read purity and masks missing boot order. Letting `TryScheduleRaycastBatch` acquire scheduler buffers opportunistically was rejected because it puts allocation/authority work back into the tool route. Caching `NativeArray` fields was rejected because Vault generation handles remain the only persistent memory identity owned by the runtime.

Scalability potential: Low devices avoid cold allocation spikes during cutter/gizmo/editor polling. Middle/high/ultra keep the same truth route; richer presentation still comes from continuous quality and tuning rows, not new authority paths.

Hardware Impact: Estimated 5-40 us worst-case hitch avoidance on i3/MX350-class hardware by removing reader-triggered Vault acquisition and hidden boot work from tool/editor polling paths. Deferred scheduler false-negative avoidance has correctness value first; profiler proof remains pending behind the external compile wall.

## Decision 014: Hot Registry Poll Removal And WFC Door Cache

Problem: `LaserCutter` still read `GlobalRegistry.Audio`, `Input`, `InteractionSignals`, `HabitatDeconstruction`, `SargassumCut`, and `Localization` inside methods on the firing, diagnosis, deconstruction, and damage routes. The WFC door cut path also attempted `TryGetComponent/GetComponentInParent<SealedDoor>()` every sustained damage pass. Separately, private runtime helpers named `TryBind*` and `TryResolveOrAcquire` made cold bind/acquire paths look like read accessors under the stricter Global Systems Doctrine.

Solution: Added `CacheColdDependencies()` and called it from cold lifecycle boundaries (`Awake`, `OnEnable`, `OnSpawn`, `OnEquip`), with `ClearColdDependencies()` on disable/despawn/destroy to avoid stale pooled-service references. Hot methods now consume cached interfaces. WFC sealed-door lookup is cached by target entity id, so a sustained beam pays the component search only on target change. Runtime helper names now expose binding/acquisition intent (`BindCoreBuffers`, `BindSchedulerBuffers`, `BindOrAcquireBuffer`), while public `TryGet*` readers route through pure `ReadBoundBuffer`/`ReadCoreBuffers` with no acquire, no boot, no signal publish, and no default seeding.

Rejected Alternatives: Leaving direct registry reads was rejected because `GlobalRegistry` is cold DI, not a live query bus. Adding a new signal lane for private service refresh was rejected because the dependencies are stable service identities, not fan-out state. Repeating component lookup for WFC doors was rejected because the sustained cutter path already has a stable hit target id. Caching `NativeArray` fields was again rejected; Vault generation handles remain the persistent memory identity.

Scalability potential: Low devices avoid registry and component-search traffic during sustained cutting. Middle devices keep identical gameplay truth with lower jitter risk. High and Ultra can spend the saved CPU headroom on the existing shader/decal/GPU spark continuum without increasing authority routes or DTO layout.

Hardware Impact: Static estimate is 3-25 us avoided on i3/MX350-class sustained cutter frames by removing repeated registry reads and WFC component lookup. Exact profiler proof remains PENDING PROFILER behind the external compile wall.

## Decision 015: Legacy GlobalSignals Bridge Removal In Cutter Door Route

Problem: After hot registry cleanup, the focused laser cutter path still had two `LaserCutter` `GlobalSignals.Publish` calls for acoustic loop and haptic micro-vibration, and one `SealedDoor` WFC state publish through the same legacy bridge. The payloads already have typed unmanaged `SignalBus<T>` lanes, so the wrapper only preserved an obsolete authority surface in a laser-cut route.

Solution: Publish `ToolAcousticSignal`, `HapticRequest`, and `WfcOutpostStateChangedSignal` directly through `SignalBus<T>.Push`. This keeps the existing owner lanes and capacities from Core; SHINOBU does not add a new signal type, does not edit `GlobalSignals`, and does not create a duplicate door-state route.

Rejected Alternatives: Keeping `GlobalSignals.Publish` was rejected because the Global Systems Doctrine marks direct queues as legacy/documented bridge lanes only. Creating a SHINOBU-specific door signal was rejected because WFC outpost state already has a typed lane and one fact must not gain two routes. Patching all project-wide `GlobalSignals.Publish` call sites was rejected as out-of-domain compile-wall churn.

Scalability potential: Low devices avoid unnecessary bridge work on repeated cutter feedback frames. Middle/high/ultra keep the same gameplay truth and spend visual budget through the existing shader/decal/GPU spark continuum rather than additional signal surfaces.

Hardware Impact: Static estimate is 1-6 us avoided per sustained feedback frame on i3/MX350-class hardware by removing the `GlobalSignals` wrapper from the cutter/door route. Exact profiler proof remains PENDING PROFILER behind the external compile wall.

## Decision 016: Legacy Operational String Boundary Retained As Cold Compatibility

Problem: `LaserCutter` still contains `BuildLegacyOperationalSummaryString`, `BuildLegacyOperationalDirectiveString`, and `new string(buffer.Buffer, ...)`. Deleting that path looks attractive for zero-GC purity, but `PlayerTool` defines the legacy compatibility API and `ToolStackValidator` explicitly validates those overrides across tools.

Solution: Leave the legacy string bridge intact and prove it is not the normal HUD/PDA route. `PlayerToolManager`, `HUDQuickBar`, and `PDALoadoutTab` use `WriteOperational*` / `TryWriteCurrentToolOperational*` span paths. The managed string bridge remains for cold compatibility and editor/static validation, not per-frame cutter feedback.

Rejected Alternatives: Removing the override was rejected because it can break the base tool API and validator outside SHINOBU's domain. Rewriting `PlayerTool` globally was rejected as cross-domain churn. Claiming the `new string` is hot was rejected by source evidence: active UI callers consume span-writing APIs.

Scalability potential: Low/middle/high/ultra runtime HUD stays on the bounded fixed-buffer path. The legacy bridge does not scale visual fidelity and must not become a gameplay hot route.

Hardware Impact: No new microsecond saving is claimed. This is a boundary proof preventing unnecessary compile-wall churn while keeping the zero-GC runtime path intact.

## Decision 017: Dispatcher Frame And Owner Visual Clock Authority

Problem: After removing sync raycasts, prefab sparks, hot registry polling, and legacy signal bridge publishes, the cutter route still carried nondeterministic Unity clocks: `Time.frameCount` in tool packets, WFC door flags, black-box dump throttling, and VFX/acoustic/haptic signals; `Time.time` in recovery feedback gates and beam jitter. Those reads are small, but they bypass dispatcher phase authority and can desync forensic proof from simulation frames.

Solution: Replace frame identity in `LaserCutter`, `LaserCutterDodRuntime`, `WfcLaserCutRuntime`, and `SealedDoor` with a local helper reading `TimeSliceScheduler.CurrentFrameId` and falling back to frame 1 when cold/editor boot has not begun. Replace wall-clock feedback/jitter with `_visualClockSeconds`, advanced once from owner-provided `ToolTick(deltaTime)` after finite clamping to 0..0.1 seconds.

Rejected Alternatives: Keeping `Time.frameCount` for visual-only lanes was rejected because the same payloads feed black-box, WFC state, and signal proof routes. Using `Time.time` only for jitter was rejected because it creates a second clock source inside a tool that already receives owner-phase delta. Querying dispatcher services through `GlobalRegistry` was rejected because `TimeSliceScheduler.CurrentFrameId` is the existing static phase snapshot used elsewhere.

Scalability potential: Low devices get the cheapest deterministic phase read and finite visual clock. Middle/high/ultra keep identical gameplay truth while spending visual budget on shader/decal/GPU spark continuum; quality weight still scales fidelity, not time authority or DTO identity.

Hardware Impact: Static estimate is 1-3 us worst-case cleanup across repeated frame-payload sites on i3/MX350-class hardware, but the main gain is deterministic proof and rollback-safe telemetry alignment. Profiler proof remains PENDING behind the external compile wall.

## Decision 018: Adjacent Responder Cold Caches And Validator Drift

Problem: Loop 12 cleaned the four primary runtime files, but adjacent cutter responders still had route-time service reads (`SealedDoor` audio, `SargassumCutResponder` cut manager), the editor mock generator still used `Time.frameCount`, and the inquisition report schema could overwrite newer proof fields with an older metric set.

Solution: Cache `IAudioService` and `SargassumCutManager` in cold lifecycle methods, consume those fields in route methods, move editor mock frame identity to `TimeSliceScheduler.CurrentFrameId`, and extend `Cutter_Raycast_Inquisition` to count `GlobalSignals.Publish`, Unity `Time.*`, dispatcher frame helpers, cold registry cache sites, legacy string bridge hits, and non-blocking `TryFinalizeCompleted` fence sites. Added an explicit telemetry-ring write invariant for `EvaluateCutterRaycastHitsJob`: scheduled cutter batches are capped at 64 rows while the black-box ring is 300 rows, so one evaluation batch cannot wrap and write the same telemetry slot twice.

Rejected Alternatives: Leaving adjacent responders outside the scan was rejected because sealed doors and sargassum cuts are direct laser-cutter effects. Treating editor-only `Time.frameCount` as harmless was rejected because CI/mock evidence should use the same frame authority. Replacing the fault dump file path with a broad native/MMF export was rejected for this loop because it touches platform/file ownership beyond SHINOBU's narrow route; the current managed file writer remains non-steady-state fault handling and is documented as residual risk until a shared black-box exporter exists.

Scalability potential: Low devices avoid route-time service lookups and keep editor stress data aligned with dispatcher frames. Middle/high/ultra keep the same gameplay truth while richer presentation still flows through shader/decal/GPU spark scalar lanes.

Hardware Impact: Static estimate is 2-8 us avoided on i3/MX350-class cutter-adjacent feedback frames by removing responder service lookups and stale validator blind spots. Profiler proof remains PENDING behind Unity import/playmode and compile-wall constraints.

## Decision 019: Read-Route Diagnosis Purity And Raw Black-Box Export

Problem: The Loop 13 route still had two residual defects. First, `WriteOperationalSummary`, `WriteOperationalDirective`, and legacy operational string bridges could call `ReadDiagnosisNow()`, which performed a live cutter hit query and `TryGetComponent` diagnosis from a HUD/read route. Second, `LaserCutterDodRuntime.DumpBlackBox` and the adjacent `WfcLaserCutRuntime.DumpBlackBox` used `BinaryWriter` field loops, making the fault artifact fragile against DTO layout changes and slower than the raw native dump pattern used elsewhere.

Solution: Removed `ReadDiagnosisNow()` entirely. Operational writers now show only active explicit secondary-fire diagnosis for a bounded dispatcher-frame window, or fall through to lockout/recovery/heat/ready state without scene search. `CutterDiagnosis` severity is now a byte code, with string severity text resolved only for the explicit cold log call. Added `_legacyOperationalBuffer` so base compatibility strings do not reuse the telemetry scratch. Converted both cutter black-box exports to stackalloc little-endian headers and raw chronological `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr` span writes with entry-size guards: 128 bytes for `LaserCutTelemetryEntry`, 96 bytes for `WfcLaserCutTelemetryEntry`.

Rejected Alternatives: Keeping HUD-triggered diagnosis was rejected because read accessors and operational writers must not search scene state or publish/sync owner facts. Deleting the legacy string overrides was rejected because `PlayerTool` and `ToolStackValidator` still own that compatibility surface. Calling `TetherBlackBoxDumpWriter` from Tools was rejected because it is a Physics helper and would add a new cross-domain dependency. Keeping `BinaryWriter` was rejected because it serializes field policy instead of the actual DTO ABI.

Scalability potential: Low devices avoid periodic HUD-poll hit/component diagnosis and get cheaper fault dumps during non-finite failures. Middle/high/ultra preserve the same gameplay truth; saved CPU remains allocated to shader/decal/GPU spark presentation and richer postmortem data, not extra physical simulation.

Hardware Impact: Static estimate is 5-25 us avoided on HUD polling frames where the old operational route would raycast/diagnose, plus fault-path dump work reduced from per-field managed writes to two raw block writes. Profiler/GCMonitor/fault-export proof remains PENDING behind Unity import and CPU-gated compile constraints.

## Decision 020: Post-Compaction Validator And API Sanity

Problem: After context compaction, the strongest residual risks were not algorithmic: stale memory could hide an API-name mismatch in the legacy operational bridge, and the inquisition validator could fail itself if it counted forbidden strings from its own source file. A premature build remained illegal under the CPU/compiler gate.

Solution: Re-read disk state, checked `PlayerTool` for the exact legacy method names, verified `LaserCutter` overrides `BuildLegacyOperationalSummaryString` and `BuildLegacyOperationalDirectiveString`, verified `Cutter_Raycast_Inquisition` skips its own file before pattern counting, and re-ran focused fixed-string scans over the runtime cutter surface. Also confirmed the raw fault-dump pattern matches existing project usage of `FileStream.Write(ReadOnlySpan<byte>)`, `FileOptions.WriteThrough`, `Flush(true)`, and root `Hecton8.Core.asmdef` unsafe policy.

Rejected Alternatives: Launching `dotnet build` was rejected because CPU remained 100% and the user explicitly forbids rebuilds under load. Editing validator counting without proof was rejected because the source already excludes itself and changing it would be churn. Removing legacy operational overrides was rejected again because `ToolStackValidator` and `PlayerTool` define that compatibility surface.

Scalability potential: Low devices keep read-route diagnosis out of HUD polling and raw fault export avoids managed field loops during non-finite recovery. Middle/high/ultra keep the same truth route while saved CPU remains available for the existing shader/decal/GPU spark continuum.

Hardware Impact: No new runtime code was changed in this pass. Verification preserves the Loop 14 static estimate: 5-25 us avoided on affected HUD polling frames, with fault export reduced from managed per-field writes to raw block writes. Profiler/GCMonitor proof remains PENDING.

## Decision 021: WFC Cut Compile-Wall And Read-Route Closure

Problem: Subagent audits found three real defects in the adjacent WFC laser-cut path. `WfcLaserCutRuntime` imported `Hecton8.Power` and `Hecton8.Logistics.Grid.Contracts`, called `WfcOutpostGridRegistry.TryGetGrid`, accepted/mutated concrete `SealedDoor`, and could read `GlobalRegistry.DataVault` plus acquire Vault buffers from `TryApplyDoorCut()`. Separately, `ResolveSuitEnergyNormalized()` could call `EnsurePlayerBindings()` and run component lookup from hot read-like tool routes when a binding was missing.

Solution: Moved WFC runtime boot to `EnsureInitialized(IDataVault)` and made `TryApplyDoorCut()` consume only core contract facts: sector hash, cell index, current flags, AUPs, power, heat, and progress delta. `LaserCutter` remains the `SealedDoor` owner caller and applies progress after the Tools runtime returns progress+frame. WFC hot buffers now flow through `ReadBoundBuffers()` and already-created Vault handles only; acquisition remains cold boot. Removed `Hecton8.Power`, `Hecton8.Logistics.Grid.Contracts`, `WfcOutpostGridRegistry`, and `WfcOutpostGridConstants` from the route, using `WfcOutpostGeneratedSignal.CellCount` and `WfcOutpostPersistenceConstants.CellCount` instead. Renamed hot energy/tension/pull reads to `ReadCached*` and removed hidden player binding repair from those read routes.

Rejected Alternatives: Keeping grid lease validation in Tools was rejected because it creates direct Tools -> Power runtime coupling and an asmdef edge. Keeping hot Vault acquisition under `TryResolveBuffers` was rejected because missed boot would become a gameplay hitch. Passing `SealedDoor` into Tools was rejected because the Tools runtime should not mutate Gameplay concrete state. Keeping `EnsurePlayerBindings()` inside `ResolveSuitEnergyNormalized()` was rejected because read-looking methods must not search components or repair state.

Scalability potential: Low devices fail closed if WFC buffers were not cold-bound and avoid registry/Vault acquisition on sustained cutter frames. Middle devices keep identical door truth with lower jitter risk. High and Ultra still spend saved budget on the existing shader clip, molten scalar, haptic/acoustic feedback, and GPU spark continuum; no quality tier changes truth ownership, DTO layout, save identity, or authority route.

Hardware Impact: Static estimate is 5-40 us worst-case hitch avoidance on i3/MX350-class hardware by removing hot Vault acquire/registry grid validation and missed-cache component repair from sustained cutter routes. Compile-wall risk is structurally reduced by removing direct Power/Logistics imports from WFC Tools runtime. Profiler/Unity import proof remains PENDING because CPU gate is 100% and no build is legal.

## Decision 022: Event Lane Cold-Boot And Sargassum Cut Registration Removal

Problem: Two residual route-time repair paths remained after Loop 16. `LaserCutterEvents.Enqueue()` could call `EnsureInitialized()` from heat/beam publish, and `EnsureInitialized()` also initialized broad legacy `GlobalSignals` queues even though cutter events use a typed `SignalBus<LaserCutterEventPayload>` lane. `SargassumCutResponder.RegisterCut()` registered itself as `IUpdatable` through `GlobalRegistry.TryRegisterUpdatable()` from a physics/cut impulse solely to decay local debug/cooldown fields while the actual cut-mask visual truth is already owned by `SargassumCutManager`.

Solution: Keep cutter event lane configuration in cold listener/source registration only. `Enqueue()` now fails closed if the lane was not configured and uses `SignalBus<T>.TryPush` only after cold boot; `FlushPending()` reads an existing snapshot and does not initialize anything. Removed `GlobalSignals.InitializeAllQueues()` from `LaserCutterEvents`. Converted `SargassumCutResponder` into a pure cut impulse bridge: it caches `SargassumCutManager` cold, writes the global mask through that owner, and gates debris with a dispatcher-frame stamp from `TimeSliceScheduler.CurrentFrameId` instead of registering itself into the dispatcher.

Rejected Alternatives: Lazy event-lane init from `Enqueue()` was rejected because the first heat/beam payload could allocate native queues and snapshot buffers during gameplay. Keeping broad `GlobalSignals.InitializeAllQueues()` was rejected because it initializes unrelated legacy lanes from a cutter-specific route. Keeping sargassum self-registration was rejected because it mutates dispatcher membership from a collision callback for a debug/cooldown problem; the cut-mask owner already carries the visual decay.

Scalability potential: Low devices fail closed on missing event-lane boot and avoid one-shot dispatcher/registry churn on sargassum cuts. Middle devices keep identical cutter and sargassum visual truth with less first-use jitter. High and Ultra retain the typed event and debris/spark lanes, spending saved budget on shader/decal/GPU visual density, not per-cluster tick registration.

Hardware Impact: Static estimate is 5-40 us first-event hitch risk avoided for cutter event lane allocation, 3-20 us avoided on late-frame cold-drain repair, and 4-25 us avoided on first sargassum cut impulse by removing runtime dispatcher registration. Profiler/GCMonitor proof remains PENDING because CPU gate is 100% and no Unity MCP editor endpoint is available.

## Decision 023: DOD Scheduler Hot Boot Fence

Problem: `LaserCutterDodRuntime.TryScheduleRaycastBatch()` still repaired a missing `_dataVault` by calling `EnsureInitialized()` from the scheduling route. That made the method name look hot/no-acquire while still allowing cold Vault binding and allocation pressure to occur during active tool use.

Solution: The scheduler now fails closed when `_dataVault` is null. Cold lifecycle still owns `LaserCutterDodRuntime.EnsureInitialized(vault)` through `LaserCutter.EnsureDodRuntimesInitialized()`. `Cutter_Raycast_Inquisition` now fails on `EnsureInitialized(` inside `TryScheduleRaycastBatch`.

Rejected Alternatives: Keeping lazy scheduler boot was rejected because missed boot becomes a first-use hitch. Querying `GlobalRegistry.DataVault` from the scheduler was rejected because the route already has an explicit cold Vault binding contract. Running a rebuild immediately was rejected under the user CPU/compiler gate.

Scalability potential: Low devices avoid schedule-time Vault repair spikes. Middle devices keep identical deterministic request truth. High and Ultra still spend budget on shader/decal/GPU spark fidelity; no quality weight changes truth ownership, DTO layout, save identity, or authority route.

Hardware Impact: Static estimate is 5-35 us first-schedule hitch risk avoided on i3/MX350-class hardware when cold boot is missed. Profiler proof remains PENDING.

## Decision 024: Collider Target Registry And WFC Owner-Phase Context

Problem: Subagent audit found two active-route defects. `TryApplyWfcDoorCut()` and `ProcessDeconstructMode()` still performed `TryGetComponent`/`GetComponentInParent` on target change during sustained cutter input. `WfcLaserCutRuntime.TryApplyDoorCut()` also pulled `SignalBus` snapshots per hit to refresh active WFC grid and system stress context.

Solution: Added `LaserCutterTargetRegistry`, a fixed 4096-slot collider id cache. `SealedDoor` registers its collider from lifecycle, and `BaseModule` registers child colliders from lifecycle using a cold scratch list. Active beam routes now resolve cached door/module identities by collider id only. WFC signal snapshot reads moved into `WfcLaserCutRuntime.RefreshOwnerPhaseContext()`, called from `LaserCutter` owner phase and cold runtime initialization; `TryApplyDoorCut()` reads cached active-grid/stress values only.

Rejected Alternatives: Keeping target-change component traversal was rejected because it still runs inside active input. Extending `InteractableRegistry` was rejected for this loop because its current resolve path lazily climbs transforms and performs `TryGetComponent` on first hit. Adding a new signal lane for one private cutter caller was rejected by the signal-lane mandate. Moving WFC ownership into Tools was rejected; the door owner still applies visual/progress state after the Tools runtime returns scalar progress.

Scalability potential: Low devices pay lifecycle registration outside the beam route and avoid hit-frame hierarchy traversal. Middle devices keep the same door/salvage truth with lower jitter risk. High and Ultra keep the same route and spend saved CPU on existing molten clip, haptic/acoustic, and GPU spark overkill.

Hardware Impact: Static estimate is 3-30 us target-change hitch risk avoided for component traversal and 2-20 us avoided on WFC door frames with populated signal snapshots. Exact profiler proof remains PENDING.

## Decision 025: Origin Bridge And Proof Drift Closure

Problem: Post-Loop-18 audit found five residual proof breaks: cutter-adjacent AUP conversion still called `GlobalSignals.CurrentRuntimeOriginAup()`, `LaserCutterDodRuntime.EnsureInitialized()` still had an implicit DataVault fallback shape, mock trigger generation force-completed a job without a shipping compile fence, explicit secondary diagnosis still used component discovery, and WFC black-box dumps used `Dump_TOOL_RESAK_SOLVER.bin` instead of the mandated SHINOBU dump path. The validator also did not count those failure modes.

Solution: `LaserCutter`, `SealedDoor`, and `SargassumCutResponder` now cache runtime-origin AUP snapshots from owner lifecycle/owner phase and their conversion helpers read only those cached snapshots. `LaserCutterDodRuntime.EnsureInitialized` now requires an explicit `IDataVault`; the editor facade performs the cold `GlobalRegistry.DataVault` bind before mock/tuning entry points. `GenerateMockCutterTriggers` keeps the deterministic same-frame stress readback only inside `UNITY_EDITOR || DEVELOPMENT_BUILD`. `BuildDiagnosisFromHit` resolves module identity through `LaserCutterTargetRegistry` and treats non-module cuttable contact as scalar diagnosis only. WFC and cutter black-box dumps both target `Docs/AgentLogs/Dump_SHINOBU_225.bin`. `Cutter_Raycast_Inquisition` now fails on origin bridge reads, implicit runtime DataVault fallback, explicit secondary component lookup, and unfenced mock force-complete.

Rejected Alternatives: Editing `HectonFloatingOrigin` or `GlobalSignals` core ownership was rejected as cross-domain compile-wall churn; SHINOBU only needs a cached owner-phase snapshot for this route. Keeping `GlobalSignals.CurrentRuntimeOriginAup()` was rejected because it is a legacy bridge read hidden inside AUP conversion. Keeping an optional Vault parameter on `EnsureInitialized` was rejected because missed boot could become runtime repair. Keeping mock force-complete available in player builds was rejected because it creates a same-frame schedule/readback loop. Keeping `TryGetComponent<ICuttable>` as a diagnosis fallback was rejected because it reopens active-route scene search.

Scalability potential: Low devices avoid bridge reads, component traversal, cold list growth, and missed-boot repair in cutter-adjacent frames. Middle devices keep identical tool truth with lower jitter risk. High and Ultra retain the same truth ownership and spend the recovered budget on shader dent/glow, acoustic/haptic polish, and GPU spark overkill; quality weight still scales presentation only and never DTO layout, save identity, or authority route.

Hardware Impact: Static estimate is 2-15 us avoided by removing origin bridge reads on cutter-adjacent AUP conversion, 5-40 us avoided for missed-boot Vault repair risk, 3-20 us avoided for explicit secondary diagnosis traversal, and no shipping runtime cost for the editor/CI mock force-complete fence. Profiler, GCMonitor, Unity import, and player-build proof remain PENDING.

## Decision 026: DOD Runtime Presentation Origin Snapshot

Problem: Loop 19 removed `GlobalSignals.CurrentRuntimeOriginAup()` from cutter-adjacent conversion helpers, but `LaserCutterDodRuntime` still read `HectonFloatingOrigin.CurrentTotalOffsetDouble` inside scheduled raycast building, scheduled hit evaluation, and VFX spark publication. That property is backed by `GlobalRegistry.FloatingOrigin`, so the DOD runtime still had a direct core registry bridge in active Tools runtime code.

Solution: Add `LaserCutterDodRuntime.CachePresentationOriginAup(double3)`, `ClearPresentationOriginAup()`, and a private `ReadPresentationOriginAup()` fallback. `LaserCutter.RefreshCachedRuntimeOriginAup()` now pushes finite owner-phase origin snapshots and clears invalid samples before request scheduling and active tool work; `ClearHandles()` resets the snapshot on runtime rebind/fail. `BuildCutterRaycastsJob`, `EvaluateCutterRaycastHitsJob`, and `PublishGpuSparkSignals` now read the cached snapshot only. `Cutter_Raycast_Inquisition` gained `dod_runtime_direct_origin_sites`, and the sidecar/shared reports were regenerated with a zero count.

Rejected Alternatives: Editing `HectonFloatingOrigin` was rejected as a core-domain change. Leaving direct `HectonFloatingOrigin.CurrentTotalOffsetDouble` reads in Tools was rejected because it is still a GlobalRegistry-backed bridge even though it is not `GlobalSignals.CurrentRuntimeOriginAup`. Passing origin through every public scheduler/VFX method was rejected for this loop because the owner already refreshes the origin snapshot at `Awake`, `OnEnable`, `OnSpawn`, `OnEquip`, `UsePrimary`, `UseSecondary`, and `ToolTick`.

Scalability potential: Low devices avoid bridge/registry reads in scheduled cutter/VFX frames. Middle devices keep identical tool truth with lower jitter risk. High and Ultra keep the same authority route and spend recovered budget on shader dent/glow and GPU spark density; `GlobalQualityWeight` still changes presentation only.

Hardware Impact: Static estimate is 2-15 us bridge/registry risk avoided on i3/MX350-class active cutter frames. Profiler, GCMonitor, Unity import, and player-build proof remain PENDING because CPU gate is 99% and no rebuild is legal.

## Decision 027: Presentation Origin Fail-Closed And Batch Capture

Problem: Loop 20 removed direct floating-origin registry reads from `LaserCutterDodRuntime`, but the replacement `ReadPresentationOriginAup()` still returned `double3.zero` when the owner-phase snapshot was absent. That is a fail-open large-world bug: at 50 km from origin, a missing snapshot would convert hit AUP into a false local VFX point and could publish spark feedback against the wrong camera-relative space. A second issue was phase drift: evaluation/finalization could read a later cached origin than the one used to build the `RaycastCommand` batch.

Solution: Replace the zero fallback with `TryReadPresentationOriginAup(out double3)`. `TryScheduleRaycastBatch` now fails closed without scheduling when the owner-phase snapshot is missing and suppresses queued requests through already-bound no-acquire request/counter buffers. The exact finite presentation origin used for `BuildCutterRaycastsJob` is stored in scheduled raycast state, passed into `EvaluateCutterRaycastHitsJob`, then carried into post-evaluation `PublishGpuSparkSignals` so local spark coordinates use the same batch origin. `ClearPresentationOriginAup` clears cached and scheduled origins, and direct live spark staging returns before any `SignalBus` push when continuous quality/tuning resolves the spark quantity to zero.

Rejected Alternatives: Keeping a zero fallback was rejected because it converts an authority failure into visually plausible but spatially false feedback. Reading the latest cached origin during finalization was rejected because origin shifts between schedule/evaluation/publish phases can desynchronize local VFX coordinates. Delaying queued requests until a later origin snapshot was rejected because it can apply old tool input in the wrong dispatcher frame.

Scalability potential: Low devices now truly emit zero spark requests when quality/tuning collapses quantity to zero. Middle devices keep deterministic batch-local presentation coordinates through the deferred raycast/evaluation sequence. High and Ultra keep the same truth route and spend saved CPU on shader dent/glow and GPU spark density; `GlobalQualityWeight` still affects presentation only, not DTO layout, save identity, or authority route.

Hardware Impact: Static estimate remains 2-15 us bridge/registry/fallback risk avoided on i3/MX350-class active cutter frames, with correctness value dominating the microsecond estimate. Profiler, GCMonitor, Unity import, and player-build proof remain PENDING because rebuild is still gated.

## Decision 028: DOD Debug Gizmo Origin Boundary

Problem: `LaserCutterDodDebugGizmo` was editor-only, but it still called `HectonFloatingOrigin.CurrentTotalOffsetDouble` directly before converting stored cutter AUP rows to local gizmo coordinates. That left a proof hole: runtime VFX used cached owner-phase origin snapshots, while the debug surface could silently show a different origin source and mask large-world drift during validation.

Solution: Added `LaserCutterDodRuntime.TryGetPresentationOriginForGizmo(out double3)` as a pure no-acquire cached-origin reader. The debug gizmo now returns without drawing if the owner-phase snapshot is missing or invalid. `Cutter_Raycast_Inquisition` gained `dod_debug_gizmo_direct_origin_sites` and now fails if the DOD gizmo reopens direct floating-origin bridge reads.

Rejected Alternatives: Keeping the editor-only bridge was rejected because debug tools are part of the proof surface; a misleading gizmo is worse than no gizmo. Falling back to `double3.zero` was rejected because it draws plausible but false local coordinates at large world offsets. Querying `LaserCutter` instances from the gizmo was rejected because that would add scene search to a read/draw route.

Scalability potential: Low devices and CI get a fail-closed debug overlay instead of a hidden bridge read. Middle/high/ultra preserve identical gameplay truth while debug drawing follows the same owner-phase origin snapshot used by shader/decal/GPU spark presentation. No quality weight changes DTO layout, save identity, or authority route.

Hardware Impact: Runtime frame impact is zero because the gizmo is editor-only. Static editor-path estimate is 1-5 us avoided by removing a direct floating-origin bridge read per gizmo draw pass, with correctness/proof value more important than the microsecond estimate. Profiler and Unity import proof remain PENDING behind the generated-project coverage gap and external compile wall.

## Decision 029: WFC Dead Property Accessor Eradication

Problem: `WfcLaserCutRuntime` still exposed `public static uint DoorsCutCount => _doorsCutCount;`. It had no project caller and violated the current raw-field/no-property stance for cutter-adjacent runtime proof. Even if cold, it gave future code a method-dispatched facade around runtime state instead of forcing telemetry rows as the proof route.

Solution: Removed the dead static property and added `wfc_runtime_property_accessor_sites` to `Cutter_Raycast_Inquisition`. The validator now fails if the removed `DoorsCutCount` property accessor is restored. The WFC telemetry row still carries `DoorsCutCount` as a raw field for black-box proof and save/telemetry inspection.

Rejected Alternatives: Keeping the property for debug convenience was rejected because no current code references it and editor/debug readers already have telemetry. Replacing it with another public getter was rejected for the same hidden-method reason. A rebuild was rejected because CPU sampled 100% and the generated project still omits the edited DOD/WFC/editor files.

Scalability potential: Low devices keep runtime proof in fixed telemetry memory and avoid method-facade drift. Middle/high/ultra keep identical WFC door truth and can still read richer proof from telemetry without changing authority route, DTO layout, save identity, or quality-weight behavior.

Hardware Impact: Direct runtime gain is sub-microsecond because the accessor had no caller. The concrete value is architectural: it prevents a future hot read path from growing around a property method and keeps WFC proof tied to raw telemetry rows. Profiler proof remains PENDING.

## Decision 030: Cutter-Adjacent Property Facade Eradication

Problem: Loop 24 scan found property facades still exposed around cutter-adjacent runtime state: `LaserCutterEvents.PendingCount`, `LaserCutterListenerRegistry.Count`, `LaserCutter.HeatLevel`, unused `LaserCutter.IsOverheated`, and `SealedDoor` state/progress booleans. These were not unmanaged DTO properties, but they still encourage method-dispatched public polling of tool/door state and weaken the raw-field/no-property proof demanded by the latest mandate.

Solution: Replaced pending/listener/heat reads with explicit pure `ReadPendingCount()`, `ReadCount()`, and `ReadHeatLevel()` methods. Removed unused `IsOverheated` and unused public `SealedDoor` properties. Kept door progress normalization owner-private through `ReadProgressNormalized()`. Updated the only cross-file consumers in `SystemDispatcher` and `SuitHUDV4CanvasOverlay`. Extended `Cutter_Raycast_Inquisition`, sidecar/shared reports, and self-audit with `cutter_property_accessor_sites=0`.

Rejected Alternatives: Keeping the property syntax for convenience was rejected because no gameplay authority should grow around public facades for cutter/door truth. Replacing the properties with new compatibility getters for every removed door field was rejected because no project caller existed. Broadly refactoring `SystemDispatcher` or HUD property style was rejected as out of domain; SHINOBU touched only the one-line consumers required by this cut.

Scalability potential: Low devices avoid future hot polling routes around public cutter/door state. Middle devices keep identical tool and door truth. High and Ultra retain the same DTO layout, save identity, and SignalBus/Vault authority route, spending budget only on the existing shader dent/glow and GPU spark continuum.

Hardware Impact: Direct measured gain is pending and likely sub-microsecond for current callsites. The concrete value is architectural: it closes future accessor drift on the cutter route and keeps state proof in explicit reads, owner-private math, DTO rows, and telemetry rather than public property sugar.

## Decision 031: Hot Managed Route Guard

Problem: Loop 25 scan found one remaining `new string` in `LaserCutter.BuildStringFromBuffer`. It is the inherited `BuildLegacyOperational*String` compatibility bridge, not a live cutter hit route, but the proof tooling did not distinguish that cold bridge from true hot-route managed iteration/text allocation. That left a verification gap: a future `foreach`, LINQ query, string interpolation, or `new string` inside `UsePrimary`, `ToolTick`, cut application, WFC hit application, or DOD schedule/evaluate/VFX routes could pass the existing report.

Solution: Hardened `Cutter_Raycast_Inquisition` with method-window counters for hot managed iteration and hot managed text allocation. The scanner now checks cutter/DOD/WFC/door/sargassum route windows and reports `hot_managed_iteration_sites=0`, `hot_managed_text_allocation_sites=0`, and `laser_cutter_new_string_bridge_sites=1`. The legacy string bridge remains documented as cold compatibility; active HUD/PDA routes use `FixedCharBuffer` writers.

Rejected Alternatives: Deleting `BuildLegacyOperational*String` was rejected because `PlayerTool` and `ToolStackValidator` still define that compatibility surface across tools. Broadly editing the base `PlayerTool` string API was rejected as outside SHINOBU_225's cutter-route domain. Treating the one `new string` as a hot failure was rejected because focused method-window proof shows it is outside the active cutter/DOD/WFC route windows.

Scalability potential: Low devices keep sustained cutter frames free of iterator/string allocation drift. Middle devices preserve identical truth routes with better proof coverage. High and Ultra still spend budget on shader dent/glow and GPU spark overkill; `GlobalQualityWeight` remains presentation-only and does not alter DTO layout, save identity, or authority route.

Hardware Impact: Static estimate is 5-60 us hitch/GC-risk avoided if future hot managed iteration or text allocation would have been introduced into sustained cutter frames. Current direct runtime gain is 0 us because the patch hardens editor proof and documents one existing cold bridge. Profiler proof remains PENDING.
