# SHINOBU_237 Rationale

Status: POLISH PASS ACTIVE - STATIC PATCHED - BUILD BLOCKED BY CPU GATE

## Decision 0 - Wake Authority Boundary
Problem: Legacy propwash built from CPU particles and per-frame seabed raycasts would violate zero-GC, frame-time, and deterministic presentation boundaries.
Solution: Treat propwash and silt as presentation-only. CPU harvests compact thrust DTOs; GPU owns proximity, particle injection, advection, visible compaction, and indirect draw count.
Rejected Alternatives: Unity ParticleSystem, `Physics.Raycast` seabed checks, CPU terrain sampling, CPU-visible particle counts. These create main-thread dependency, broadphase sync, allocation risk, and transparent-sort pressure.
Scalability potential: Low uses sparse GPU budgets and cheap radial/lift approximation; Middle increases particle count and flow sampling cadence; High adds denser SDF/depth-reactive silt; Ultra spends saved CPU on visual overkill particle density and richer event sampling.
Hardware Impact: i3/MX350 gain is removal of cosmetic raycasts and CPU particle simulation from the owned propwash/silt route. Static estimate: 0 CPU terrain-query us, 0 CPU particle-sim us for this feature path; profiler data pending Unity runtime.

## Decision 1 - DTO Layout
Problem: CPU-to-GPU wake payload must be stable across ARM64, Burst, NativeArray, and StructuredBuffer reads.
Solution: Use explicit 32-byte `PropwashEventDTO`: `LocalPosition` float3 at offset 0, `ThrustVector` float3 at offset 12, `Intensity` float at offset 24, `Radius` float at offset 28.
Rejected Alternatives: double3 GPU payload, property-backed DTOs, bool flags, sequential layout without offset guard. GPU only consumes float data; bool/property patterns invite padding/copy defects.
Scalability potential: DTO layout stays constant across Low/Middle/High/Ultra; quality changes budget/count/cadence, not wire shape.
Hardware Impact: 32-byte stride keeps upload bandwidth predictable. 512 events cost 16384 bytes per upload, small enough for low-end integrated/entry GPUs.

## Decision 2 - Vault Ring Ownership
Problem: Parallel agents cannot depend on unfinished vehicle classes or hot-poll GlobalRegistry for live propwash data.
Solution: Added DataVault ids `PropwashGpuEventRing=71492`, `PropwashGpuRingCursor=71493`, `PropwashGpuTelemetryRing=71494`, `PropwashGpuTuning=71495`, `PropwashGpuWakeProfiles=71496`. Renderer resolves handles cold and publishes GPU buffers from owned phase.
Rejected Alternatives: direct submarine component references, managed vehicle lists, scene search, `GlobalDataVault.TryGetLatestCreated()` in hot path. These break domain decoupling and parallel integration.
Scalability potential: Low can publish few harvested events; Middle/High/Ultra can fill the same 512-slot ring without changing consumer ABI.
Hardware Impact: Ring write is contiguous unmanaged memory. Estimate: <50 us for 1024 source harvest, <5 us telemetry write, profiler pending.

## Decision 3 - Burst Harvest And Mock Source
Problem: Vehicle authority is not guaranteed ready, but shader and draw path need deterministic stress data now.
Solution: `GenerateMockPropwashEventsJob` injects 500 deterministic events; `HarvestKinematicWakeJob` reads kinematic source DTOs through `UnsafeUtility.AsRef` and writes local camera-space propwash events.
Rejected Alternatives: waiting for vehicle controllers, temporary MonoBehaviour emitters, `foreach` over scene objects. These create dependency stalls and managed overhead.
Scalability potential: Low samples 4 events in shader, Middle samples more by continuous quality curve, High/Ultra use the full stress payload.
Hardware Impact: Mock path uploads at most 16 KB event data. i3/MX350 expected cost is dominated by GPU particle work, not CPU harvest.

## Decision 4 - GPU Dear Lie Terrain Proximity
Problem: The CPU should not know if propwash is close enough to the seabed to spawn silt.
Solution: `CS_EvaluateWakeProximity` reads `_PropwashEvents`, samples cave SDF and terrain height textures, and writes silt particles into the GPU particle buffers. `DispatchWakeProximityInjection` executes before the main advection kernel.
Rejected Alternatives: CPU raycast, CPU heightmap sample, or same-frame physics broadphase sync. These burn gameplay frame time for presentation dust.
Scalability potential: Low runs the same event kernel with a small event count/sample budget; Ultra uses dense event payloads and stronger tint/advection.
Hardware Impact: CPU terrain-query cost is 0 us. GPU cost is bounded by 512 event threads before the particle pass.

## Decision 5 - Procedural Indirect Draw
Problem: CPU must not know visible particle count, and prompt requires procedural indirect draw.
Solution: The compute shader clears and increments `_MarineSnowIndirectArgs`; renderer calls `Graphics.DrawProceduralIndirect` with `MeshTopology.Triangles`. Existing marine snow shader already expands six vertices from `SV_VertexID` and reads particles by `SV_InstanceID`.
Rejected Alternatives: `Graphics.RenderMeshIndirect`, CPU-visible count, mesh instance enumeration, or readback. These preserve unnecessary mesh/API dependency or CPU synchronization.
Scalability potential: Low/Middle/High/Ultra all share one draw call; only GPU-written instance count and particle budget vary.
Hardware Impact: One draw submission, no count readback. Exact microseconds pending frame debugger/profiler.

## Decision 6 - Continuous Quality
Problem: Binary quality switches produce visible pops and violate GlobalQualityWeight doctrine.
Solution: Budget and propwash event sample count are continuous: `ResolveParticleBudget` smoothsteps Low to Ultra budget; HLSL lerps event sample count from 4 to active count.
Rejected Alternatives: `if (isLowEnd)` branches, tier-specific DTOs, or authority changes based on quality.
Scalability potential: Low: survival budget and 4 event samples. Middle: increasing event sampling and curl. High: dense SDF-triggered silt. Ultra: full 500-event visual overkill.
Hardware Impact: Low-end devices shed GPU samples while preserving route/layout. High-end devices spend saved CPU on denser visuals.

## Decision 7 - Netcode And Black Box
Problem: Cosmetic dirt must not pollute deterministic rollback/Merkle state, but crashes need forensic history.
Solution: Architecture doc excludes `PropwashGpu*` buffers from rollback authority. `PropwashTelemetryEntry` records 300 frames and dumps raw bytes to `Docs/AgentLogs/Dump_SHINOBU_237.bin` on black-box dump.
Rejected Alternatives: hashing cosmetic particles, logging managed strings every frame, or no forensic record.
Scalability potential: Telemetry payload stays fixed 64B per frame; quality changes recorded values only.
Hardware Impact: 300 x 64B = 19200 bytes black-box storage. Runtime write target <5 us.

## Decision 8 - Build Gate
Problem: Project rule forbids launching `dotnet build` when CPU load is above 50 or another compiler is active.
Solution: Process probe found no `dotnet/csc`; CPU probe returned 100. Build was not launched. Static verification and code scans were recorded instead.
Rejected Alternatives: ignoring CPU gate, launching a competing build, or claiming compile proof without execution.
Scalability potential: Not runtime-relevant.
Hardware Impact: Avoided adding compiler load while machine is already saturated.

## Decision 9 - Propwash Read Doctrine Correction
Problem: Propwash helper names `TryResolve*` and `Resolve*` hid DataVault refresh/allocation behavior and violated the pure read accessor doctrine.
Solution: Propwash hot helpers now use `TryAcquireReadyPropwash*` against already-created handles only. Mutating tuning snapshot capture is named `CapturePropwashTuningSnapshot`; shader parameter materialization is named `BuildPropwash*`.
Rejected Alternatives: Keeping read-looking names or invoking `EnsureNativeState()` from the read path. That creates hidden ownership mutation and possible buffer growth during frame work.
Scalability potential: Low/Middle/High/Ultra all share the same owner-created Vault handles; quality changes data volume, not authority ownership.
Hardware Impact: Prevents surprise rebind/growth work in frame paths. Estimated gain is stability and removal of hidden spikes rather than fixed ALU savings.

## Decision 10 - Propwash GPU Event Double Buffer
Problem: A single `_PropwashEvents` upload buffer risks CPU lock/write contention against the buffer the GPU consumed in the prior frame.
Solution: Added `_propwashEventBufferA/B`; `ClaimPropwashEventUploadBuffer()` writes the inactive buffer through `LockBufferForWrite`, then publishes that buffer as the compute read source.
Rejected Alternatives: `SetData` per frame, single-buffer lock, or CPU readback fencing. These preserve driver stalls and defeat GPU sovereignty.
Scalability potential: Low uploads a sparse 4-sample-effective feed; Ultra can publish the full 512-event ring without changing buffer identity or shader ABI.
Hardware Impact: 16 KB alternating upload avoids same-resource hazard on i3/MX350 class discrete/entry GPUs and keeps Quest-style UMA on the linear write path.

## Decision 11 - Wake Profile Vault Table
Problem: Task 17 required `vehicle_wake_profiles.csv`; the earlier parser only handled key-value propwash tuning.
Solution: Added 64B `PropwashWakeProfileDTO`, `PropwashGpuWakeProfiles=71496`, editor-only source-data staging for `Assets/_SourceData/VFX/Propwash/vehicle_wake_profiles.csv`, and a `ReadOnlySpan<byte>` FNV/float parser into the Vault table. Player builds use deterministic defaults until a VFX `.h8bin` or Data Monolith route hydrates the table.
Rejected Alternatives: `string.Split`, `float.Parse`, ScriptableObject profile lookups, or managed dictionaries. Those allocate and make tuning culture/runtime dependent.
Scalability potential: Low can use conservative engine emission/radius multipliers; Middle/High/Ultra can drive richer turbulence and lift profiles without C# recompilation.
Hardware Impact: Editor/source-data parse only. Player runtime cost is a fixed 64B default row table; no per-frame managed profile lookup or StreamingAssets file IO.

## Decision 13 - Wake Profile Player IO Fence
Problem: Runtime `StreamingAssets` CSV reads for wake profiles conflict with the current binary-payload ledger and would introduce platform-specific file IO risk in player builds.
Solution: `vehicle_wake_profiles.csv` is source-data/editor-only under `Assets/_SourceData/VFX/Propwash`. The player build route keeps `PropwashGpuWakeProfiles` deterministic-default until a VFX `.h8bin` or Data Monolith importer owns hydration.
Rejected Alternatives: Player `StreamingAssets` polling, managed dictionaries, or hidden CSV reads during gameplay. These create stutter and violate Data Monolith readiness boundaries.
Scalability potential: Low/Middle/High/Ultra still scale through GlobalQualityWeight and shader budgets; source profiles tune authoring data without changing runtime authority.
Hardware Impact: Removes player file polling and four wake-profile managed staging arrays from non-editor builds. Expected runtime file IO cost: 0 us.

## Decision 12 - Editor Telemetry Waterfall
Problem: The UI Toolkit facade lacked the requested direct telemetry visualization.
Solution: Added `TelemetryWaterfallElement` that resolves `PropwashGpuTelemetryRing` directly in editor and draws particle budget plus GPU microsecond curves with `Painter2D`.
Rejected Alternatives: IMGUI allocations, runtime HUD text, or log spam. Those hide the real black-box data and add managed churn.
Scalability potential: Designers see whether Low/Middle/High/Ultra budgets are breathing continuously while tuning threshold/curl/override.
Hardware Impact: Editor-only; gameplay cost 0 us.

## Decision 14 - Burst Immediate-Run Fence
Problem: Four renderer-local VFX wake jobs had Burst attributes but their call sites invoked `.Execute()` directly, risking managed execution for scalar vehicle wake, mock flow, propwash fallback, and dynamic mock wake preparation.
Solution: `BuildVehicleWakeSignalJob`, `BuildMockFlowFieldJob`, `GenerateMockPropwashEventsJob`, and `BuildMockWakeSignalJob` now use `IJob.Run()`. This keeps each immediate read/upload path synchronous by design, avoids adding a same-frame scheduled job plus hidden `Complete()`, and still enters the Burst IJob execution route.
Rejected Alternatives: Leaving direct `Execute()`, scheduling then immediately completing, or replacing wake prep with managed vehicle scene iteration. Direct Execute undercuts the Burst mandate; schedule/complete adds a fake fence; scene iteration couples SHINOBU to vehicle ownership.
Scalability potential: Low still emits sparse deterministic wake vectors and shader samples 4 propwash events; Middle/High/Ultra use the same generated rings/buffers with continuously increasing GPU event sampling and particle budget.
Hardware Impact: Removes managed-call fallback from the immediate VFX wake path. Static estimate: avoids managed loop/call overhead for 1 flow row, 1 vehicle wake row, up to 500 propwash rows, and the mock dynamic wake rows; exact microseconds pending Unity Burst profiler.

## Decision 15 - Vehicle Command Propwash Bridge
Problem: The existing vehicle wake route produced a `FluidImpulseSignal`, but it did not append the same vehicle thrust event into `_PropwashEvents`; GPU silt could rely on mock events even while real throttle commands existed.
Solution: Added `CommitVehicleWakePropwashEventJob`, a Burst `IJob` that appends one sanitized `PropwashEventDTO` to `PropwashGpuEventRing` and updates `PropwashGpuRingCursor`. `PublishVehicleWakeImpulse` now derives a camera-local position by converting result and camera runtime positions to AUP, subtracting in double precision, casting the localized delta to `float3`, and running the bridge job before double-buffer upload.
Rejected Alternatives: Reading vehicle MonoBehaviours, adding a direct Vehicles runtime dependency, or pushing only `FluidImpulseSignal`. Scene iteration violates zero-GC/data ownership; direct vehicle references widen the compile wall; fluid impulse alone does not feed the compute propwash buffer.
Scalability potential: Low receives the same single real vehicle event but shader sampling remains at the low continuous floor; Middle/High/Ultra blend that event with denser mock/harvest stress and larger GPU particle budgets.
Hardware Impact: One cooldown-gated Burst `IJob.Run()` plus one 16 KB propwash buffer upload when vehicle throttle publishes. It removes the need for any CPU particle/raycast reaction to real vehicle commands.

## Decision 16 - Unity Profile Finite Guard
Problem: `float.IsFinite` is not safe to assume across all Unity scripting profile/compiler combinations, and the renderer had regressed direct `IJob.Execute()` call-sites after the vehicle bridge edit.
Solution: Replaced touched `float.IsFinite` usage with `math.isfinite`, matching the rest of the Burst/math code, and reasserted `Run()` at all local wake job call-sites.
Rejected Alternatives: Keeping BCL finite helpers or relying on compile failure to catch the profile mismatch. The project rule requires static proof before attempting a build under CPU gate.
Scalability potential: No visual-tier behavior changes; Low/Middle/High/Ultra continue to scale through quality-weighted particle/event budgets.
Hardware Impact: Prevents compatibility failure without adding runtime work. Latest static scan shows 0 direct wake job `Execute()` call-sites and 0 BCL finite helper hits in touched SHINOBU files.

## Decision 17 - Static Report Correction
Problem: A fresh strict `rg` scan contradicted the prior status text and showed five direct local wake `IJob.Execute()` call-sites still present in `HectonMarineSnowRenderer`.
Solution: Replaced the scalar vehicle bridge, vehicle commit bridge, mock flow, mock propwash, and mock dynamic wake direct `Execute()` calls with `Run()`, then re-ran the exact scan and recorded only `Run()` call-sites.
Rejected Alternatives: Trusting stale status text, scheduling plus immediate `Complete()`, or waiting for build output under a saturated CPU gate. Stale status is not proof; schedule/complete would add a fake fence; build is forbidden while CPU remains above 50.
Scalability potential: No quality-tier change. The same Vault ring and GPU sample curve drive Low/Middle/High/Ultra; the fix preserves the Burst entry route for all tiers.
Hardware Impact: Removes managed direct execution risk for five local wake preparation jobs. Latest build gate remains blocked: CPU 100, no active `dotnet/csc`, no build launched.

## Decision 18 - Propwash Vault Compaction Fence
Problem: Propwash `TryAcquireReady*` helpers resolved Vault handles without checking `DataVault.IsCompactionFenceActive`, unlike adjacent VFX resolver paths. That creates a narrow unsafe read window during Vault compaction.
Solution: Added the compaction-fence guard to `TryAcquireReadyPropwashEvents`, `TryAcquireReadyPropwashCursor`, `TryAcquireReadyPropwashTelemetry`, `TryAcquireReadyPropwashTuning`, and `TryAcquireReadyPropwashWakeProfiles`.
Rejected Alternatives: Assuming compaction never overlaps VFX reads, or calling `EnsureNativeState()` from the read path. Assumption-only safety is not acceptable; hidden ensure would reintroduce mutating read behavior.
Scalability potential: No visual-tier behavior changes. Low/Middle/High/Ultra keep the same Vault ids and DTO layouts; during compaction the renderer skips cosmetic propwash reads instead of touching moving memory.
Hardware Impact: Adds one boolean branch per propwash Vault accessor and prevents undefined NativeArray access under compaction. Latest build gate remains blocked: CPU 100, no active `dotnet/csc`, no build launched.

## Decision 19 - Editor Facade Vault Fence
Problem: `PropwashGpuTunerWindow` could mutate tuning or resolve telemetry during Vault compaction, creating an editor-only unsafe access path despite runtime guards.
Solution: Added `IDataVault.IsCompactionFenceActive` checks before tuning writes, telemetry handle binding, and paint-time telemetry resolve.
Rejected Alternatives: Treating editor tooling as harmless or relying on user timing. The tuner directly touches Vault memory; editor-only does not excuse unsafe native access.
Scalability potential: Designer controls still affect Low/Middle/High/Ultra through the same continuous quality override and tuning DTO; compaction windows simply skip the editor action.
Hardware Impact: Editor-only branch checks. Gameplay cost is 0 us; failure mode removed for Play Mode tuning.

## Decision 20 - Gizmo Vault Fence Reuse
Problem: `OnDrawGizmosSelected` directly resolved `_propwashEventHandle`, bypassing the newly fenced propwash accessor path.
Solution: Replaced the direct resolve with `TryAcquireReadyPropwashEvents`, so editor gizmo rendering inherits native readiness and compaction-fence checks.
Rejected Alternatives: Duplicating the fence logic in the gizmo or assuming scene-view reads are safe. One accessor is less error-prone and preserves the single read contract.
Scalability potential: No runtime tier changes. Debug visualization remains editor-only and reads the same compact DTO ring for Low/Middle/High/Ultra tuning.
Hardware Impact: Editor-only. Gameplay cost is 0 us; removes one unsafe native read path during Play Mode compaction.

## Decision 21 - Biome Tint Render Route
Problem: `Hecton_MarineSnow.compute` tagged propwash silt with flag bit 3 and the renderer published `_PropwashBiomeTint`, but the material pass ignored that flag/tint and still emitted generic marine-snow RGB.
Solution: Added `_PropwashBiomeTint` to `Hecton_MarineSnow.shader`, used the particle flag `8u` to lerp visible silt RGB to the Vault-backed biome tint, and published the same cached vector to both compute and material from `RefreshHotGpuBindings`.
Rejected Alternatives: Expanding `MarineSnowParticle` with per-particle RGB lanes, CPU material swaps by biome, or a fixed brown/white propwash color. Those increase GPU stride, add CPU state churn, or remove biome authoring control.
Scalability potential: Low/Middle/High/Ultra keep the same DTO and particle layout. Quality changes event samples and particle budget; tint intensity remains a continuous authoring scalar from `PropwashGpuTuningDTO`.
Hardware Impact: Adds one material vector update only when the value changes and one visible-particle color lerp in the vertex path. CPU particle/raycast cost remains 0 us; GPU stride and Vault ABI are unchanged.

## Decision 22 - HLSL Resolver Name Fence
Problem: The broadened static scan found shader-local `ResolvePropwashEventFlow`. The function was pure math, but its name matched the forbidden read-like `ResolvePropwash*` pattern used to catch hidden Vault mutation paths.
Solution: Renamed the HLSL helper and call-sites to `ComputePropwashEventFlow`, preserving the same shader math while making the static contract exception-free.
Rejected Alternatives: Documenting the HLSL exception or narrowing the scan to C# only. Exceptions make future audits weaker, and the rename has no runtime cost.
Scalability potential: No Low/Middle/High/Ultra behavior change. The same continuous event sampling and particle budget curves remain authoritative.
Hardware Impact: 0 us. This is naming hygiene that keeps static proof strict; latest build gate remains blocked by CPU load 98.49 with no active `dotnet/csc`.

## Decision 23 - Cursor-Aware Propwash GPU Upload
Problem: `UploadPropwashEventGpuBuffer` copied `events[0..activeCount]` and ignored `PropwashRingCursorDTO.WriteCursor`. That is correct only while the ring's oldest event is at slot 0; future kinematic harvest or wrapped vehicle commits could leave the newest valid window split across the end and beginning of the Vault ring.
Solution: Changed upload to accept `PropwashRingCursorDTO`, compute `sourceStart = WriteCursor - EventCount` with wrap, and stream an oldest-to-newest contiguous snapshot into the inactive propwash `GraphicsBuffer`.
Rejected Alternatives: Sorting events on CPU, clearing and rewriting the Vault ring from slot 0 every frame, or forcing the shader to understand circular indexing. Sorting allocates/branches, ring normalization mutates source truth, and shader circular lookup would spend GPU ALU per particle/event sample.
Scalability potential: Low still uploads a small effective event feed and samples 4 events; Middle/High/Ultra can consume a wrapped 512-row ring without changing DTO stride, shader ABI, or quality authority.
Hardware Impact: Adds one integer wrap per uploaded active event, max 512 iterations in the upload copy. Avoids stale/missing wake vectors after ring wrap while preserving the 16 KB double-buffered upload path. Latest build gate remains blocked by CPU load 100 with no active `dotnet/csc`.

## Decision 24 - Editor Status String Purge
Problem: `PropwashGpuTunerWindow` concatenated the tuning version into a status label during live editor writes. This is editor-only, but it weakens the no-GC facade claim and adds no operational value.
Solution: Replaced the dynamic status text with a constant `"Applied PropwashGpuTuning."` message. The version remains in the Vault DTO and telemetry route, not a managed UI string.
Rejected Alternatives: Keeping the concatenation, adding `StringBuilder`, or formatting into a managed label every slider change. All three add editor churn without improving control.
Scalability potential: No runtime visual-tier change. Designer controls still mutate `PropwashGpuTuningDTO` and the renderer still scales through continuous GlobalQualityWeight/tuning curves.
Hardware Impact: Gameplay cost remains 0 us; editor write path avoids one transient string allocation per apply.

## Decision 25 - CSV Numeric Parser Fail-Closed
Problem: `PropwashGpuProfileCsvParser.TryParseFloat` accepted partial numeric tokens, so malformed source data like `1abc` could silently hydrate as `1`.
Solution: After parsing sign, integer, and fractional lanes, the parser now rejects any remaining non-consumed bytes. The cold CSV route fails closed instead of corrupting Vault tuning/profile rows.
Rejected Alternatives: `float.Parse`, culture-aware parsing, or accepting permissive suffixes. Those allocate, vary by locale, or hide bad source data.
Scalability potential: Low/Middle/High/Ultra retain the same runtime DTOs and quality curves; only cold authoring data validation tightens.
Hardware Impact: Cold editor/source-data parse only. Adds one integer compare per numeric token; runtime gameplay cost remains 0 us. Latest build gate remains blocked by CPU load 85.12 with no active `dotnet/csc`.

## Decision 26 - CSV Optional Field Fail-Closed
Problem: Wake profile optional fields ignored parse failures. A present malformed token after `EmissionRate` could silently leave a default value and still accept the row.
Solution: `TryParseOptionalFloat` now treats absent or empty columns as optional, but rejects any present non-empty token that fails the strict numeric parser. `TryApplyWakeProfileLine` rejects the whole row on that failure.
Rejected Alternatives: Keeping permissive defaults, forcing every optional column to exist, or using culture-aware managed parsers. Permissive defaults hide bad data; mandatory columns break authoring flexibility; managed parsers allocate and vary by locale.
Scalability potential: No runtime quality-tier change. Low/Middle/High/Ultra continue to consume the same 64-byte wake profile DTO rows; cold source rows are either valid or skipped.
Hardware Impact: Cold source-data parse only. Adds one branch and strict parse result check per optional token; gameplay cost remains 0 us. Latest build gate remains blocked by CPU load 100 with no active `dotnet/csc`.

## Decision 27 - Procedural Indirect Args ABI
Problem: The marine snow renderer submits `Graphics.DrawProceduralIndirect`, but the args buffer was allocated with indexed indirect size and the compute clear wrote a fifth unused uint. The route worked as over-allocated raw memory, but the ABI label was wrong.
Solution: Added a 16-byte procedural indirect args stride and removed the offset-16 compute store. The four lanes now match non-indexed procedural draw: vertex count, instance count, start vertex, start instance.
Rejected Alternatives: Keeping the indexed-sized buffer, switching back to indexed rendering, or reading visible counts on CPU. Over-allocation weakens the ABI proof; indexed rendering reintroduces mesh semantics; CPU counts violate the task.
Scalability potential: No visual-tier behavior change. Low/Middle/High/Ultra still differ by GPU-written instance count and particle budget, not args layout.
Hardware Impact: Saves 4 bytes in the args buffer and one raw UAV store in the clear kernel. More important: removes an indirect-draw ABI mismatch before Unity import/profiler proof. Latest build gate remains blocked by CPU load 100 with no active `dotnet/csc`.

## Decision 28 - Continuous Proximity Event Budget
Problem: Propwash flow sampling used a continuous quality curve, but `CS_EvaluateWakeProximity` could still evaluate every uploaded event. On low quality, that kept the SDF/height proximity pass hotter than the visual budget implied.
Solution: Added `ComputePropwashEventSampleBudget` in C# and HLSL. C# dispatch now submits only the quality-scaled event budget, and HLSL rejects indices beyond the same budget as a safety guard.
Rejected Alternatives: Binary low-end cutoff, fixed 24-event floor, or leaving full proximity evaluation. Binary cutoff pops; fixed floor ignores thermal pressure; full evaluation spends GPU ALU on cosmetic dirt beyond the visible budget.
Scalability potential: Low collapses proximity SDF work toward the same minimum event sample lane used by flow. Middle increases smoothly. High/Ultra consumes the active ring without changing DTO layout or authority route.
Hardware Impact: Reduces low-quality proximity threads from up to 512 to the continuous sample budget. No CPU particle path is added; one scalar quality computation is reused for dispatch sizing. Latest build gate remains blocked by CPU load 100 with no active `dotnet/csc`.

## Decision 29 - Parallel Harvest Ring Clamp
Problem: `HarvestKinematicWakeJob` used `WrapIndex(RingWriteCursor + index, capacity)` for parallel event writes. If a future vehicle source table supplied more rows than the event ring capacity, wrapped indices could cause multiple worker lanes to write the same slot.
Solution: Clamp the processed source count to the event ring capacity before the parallel write guard. Each active lane now maps to one slot in the bounded ring window.
Rejected Alternatives: Atomic per-event reservation, CPU sorting, or letting the shader interpret an over-capacity circular source table. Atomics and sorting add cost for cosmetic wake vectors; shader circular lookup spends ALU every sample and weakens the ring snapshot contract.
Scalability potential: Low/Middle/High/Ultra keep the same 512-row event ABI and quality-scaled shader sample budget. Higher tiers can fill the ring, but never alias worker writes inside one harvest batch.
Hardware Impact: Adds one integer `min` in the Burst job guard. Removes a future data race without CPU particle work. Latest build gate remains blocked by CPU load 92.29 with no active `dotnet/csc`.

## Decision 30 - Deterministic Unity Script Metadata
Problem: New propwash C# files lacked `.meta` files. Unity would generate GUIDs on import, causing asset database churn and avoidable cross-agent merge noise.
Solution: Added deterministic MonoImporter `.meta` files for `PropwashGpuContracts.cs`, `PropwashGpuTunerWindow.cs`, and `PropwashGpuLayoutValidator.cs`; verified each GUID appears exactly once.
Rejected Alternatives: Letting Unity auto-generate metas or ignoring editor/runtime script GUIDs. Auto-generation makes import state machine-dependent and weakens reproducibility.
Scalability potential: No runtime visual-tier change. This protects asset identity for the editor facade and runtime contracts across Low/Middle/High/Ultra tuning work.
Hardware Impact: Gameplay cost 0 us. Build gate remains blocked by CPU load 100 with no active `dotnet/csc`.

## Decision 31 - Dedicated Rendering Scanner Artifact
Problem: `Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json` is a shared report currently topped by another agent, with SHINOBU_237 preserved only as a nested previous report. Overwriting it would create avoidable cross-agent report churn.
Solution: Added `Docs/Reports/RENDERING_OPTIMIZATION_REPORT_SHINOBU_237.json` as the current SHINOBU proof artifact and recorded the shared-report collision explicitly. The dedicated report preserves the Task 19 metrics without hiding another agent's current report.
Rejected Alternatives: Overwriting the shared report, trusting stale nested report data, or omitting Task 19 proof. Overwrite causes merge noise; stale data is not current proof; omission weakens the scanner contract.
Scalability potential: No runtime tier change. The report proves the visual authority stays on the PropwashEventDTO/Vault/GPU route across all quality weights.
Hardware Impact: Gameplay cost 0 us. Static scan proof remains: 15 non-domain camera speed-line ParticleSystem text hits, 0 emit, 0 collision, 0 VFX raycast, 0 forbidden wake hits.

## Decision 32 - Propwash Overflow Dump Trigger
Problem: `RecordPropwashTelemetry` computed overflow as `eventCount > PropwashEventRingCapacity`, but the upload/debug event count is already clamped to capacity. A real ring overflow recorded in `PropwashRingCursorDTO.DroppedCount` could fail to trigger the 300-frame dump.
Solution: Read the fenced cursor snapshot, copy `DroppedCount` into `PropwashTelemetryEntry.OverflowCount`, write the current `WriteCursor`, and call `DumpBlackBoxOnce()` when overflow or estimated GPU time exceeds 1500 us.
Rejected Alternatives: Comparing clamped event counts, adding a separate managed overflow flag, or dumping every frame. Clamped counts cannot detect overflow; a shadow flag creates duplicate truth; per-frame dump is IO abuse.
Scalability potential: No visual-tier route change. Low/Middle/High/Ultra still differ by event/particle budgets, while overflow evidence remains tied to the single Vault cursor owner.
Hardware Impact: Adds one fenced cursor read in telemetry recording and no CPU particle work. Dump remains failure/spike path only. Latest build gate remains blocked by CPU load 100 with no active `dotnet/csc`.

## Decision 33 - Real Propwash Telemetry Scalars
Problem: `PropwashTelemetryEntry.MaxIntensity` was a 1/0 placeholder and `StrongestLocalPosition` was always default, weakening the 300-frame forensic payload.
Solution: Reused the existing `UploadPropwashEventGpuBuffer` loop to track the strongest sanitized event while building the contiguous GPU snapshot. Telemetry now records the real max intensity and local position.
Rejected Alternatives: A second CPU pass over the ring, GPU readback, or leaving placeholders. A second pass adds redundant CPU work; readback violates GPU sovereignty; placeholders fail black-box requirements.
Scalability potential: Low/Middle/High/Ultra keep the same event ring and shader budget. The telemetry payload gets better evidence without changing quality authority.
Hardware Impact: Adds one compare and occasional float3 assignment per uploaded event inside an already-required max-512 upload loop. No CPU particle simulation, raycast, or readback added. Latest build gate remains blocked by CPU load 100 with no active `dotnet/csc`.

## Decision 34 - Dedicated AUP Rebase Buffer Route
Problem: The old AUP shift path lived inside `CSMain`, so every simulation kernel carried a rebase branch and a pending shift could be applied in the same kernel that performs full particle advection. After adding a dedicated `CS_RebaseParticles` dispatch, the HLSL load routes had to be exact: `CSMain` must read the ping-pong read buffer, while rebase and sonar accumulation must read the current write-bound buffer.
Solution: Added `_rebaseKernel`/`_rebaseThreadGroupSize`, dispatch `CS_RebaseParticles` against the current read buffers before `CSMain`, then zero `_AupShiftOffset` before simulation to prevent double rebase. Patched HLSL so `CSMain` uses `LoadSiltParticle`, and `AccumulateSonarGlow`, `CS_IntegrateSiltParticles`, and `CS_RebaseParticles` use `LoadWrittenSiltParticle` where C# binds the current frame buffer through write slots.
Rejected Alternatives: Leaving the rebase branch only in `CSMain`, clearing all particles on origin shift, or rebasing on CPU. The branch-only path pays a permanent per-particle check; clearing particles causes visible silt pops; CPU rebase violates GPU ownership and scales with live particle count on the main thread.
Scalability potential: Low/Middle/High/Ultra all preserve the same local-float particle space. Rebase cost is paid only on origin-shift frames, while quality continues to scale particle/event budgets through `GlobalQualityWeight` without changing DTO layout or authority route.
Hardware Impact: Normal frames save the dedicated-rebase work entirely. Shift frames add one bounded GPU pass over active particles and avoid CPU readback/rewrite. Latest static scans show 0 forbidden propwash APIs and 0 direct `.Execute()` call-sites; build remains blocked by CPU load 100.00 with no active `dotnet/csc`.

## Decision 35 - Emergency Mock Ring Cursor Parity
Problem: `GenerateMockPropwashEventsJob` loaded `PropwashRingCursorDTO` but ignored `WriteCursor` by setting `baseCursor = 0`. The emergency stress path therefore overwrote slots `0..499` every frame and did not prove the same circular Vault contract used by real vehicle wake commits.
Solution: Changed the mock writer to start from `WrapIndex(cursor.WriteCursor, capacity)` and advance `cursor.WriteCursor` by `eventCount`. The GPU upload path already consumes the cursor, so mock and real wake events now share the same oldest-to-newest ring snapshot semantics.
Rejected Alternatives: Keeping slot-zero mock overwrite as a special case, clearing the ring each mock frame, or teaching the shader a separate mock indexing mode. Special cases weaken Task 05 proof; clearing mutates source truth unnecessarily; shader-side mode adds per-sample branches.
Scalability potential: Low/Middle/High/Ultra keep the same 512-row ring and continuous event sample budget. Higher tiers can stress wrapped ring upload behavior, while low tiers still sample the quality-collapsed event window without DTO or shader ABI changes.
Hardware Impact: Adds one integer wrap before the bounded max-500 mock loop. Removes a diagnostic blind spot without CPU particles, raycasts, allocation, or GPU readback.

## Decision 36 - Gizmo Cursor-Aware Ring Window
Problem: `OnDrawGizmosSelected` rendered the first `events[0..count]` rows from the Vault event ring. After cursor-aware GPU upload, that editor view could display stale ring slots and mislead harvest debugging after wraparound.
Solution: The gizmo now acquires the fenced `PropwashGpuRingCursor`, clamps `EventCount`, computes the same `ComputePropwashUploadStart(cursor.WriteCursor, eventCount, events.Length)` window, and reads each row through `WrapPropwashUploadIndex`.
Rejected Alternatives: Keeping the debug-only stale view, adding a separate managed debug copy, or reordering the Vault ring for editor display. Stale view corrupts evidence; managed copy adds allocation risk and a shadow route; ring reorder mutates source truth for a view.
Scalability potential: No runtime visual-tier change. Low/Middle/High/Ultra keep the same event ring and quality-scaled shader sampling; editor harvest evidence now matches the GPU presentation route at every quality weight.
Hardware Impact: Editor-only. Adds one cursor read and one wrapped index per drawn gizmo row, capped at 32. Gameplay cost remains 0 us.

## Decision 37 - Current Ledger Propwash Boundary Preservation
Problem: The active `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` changed to a short current-format ledger while this pass was running, removing the older SHINOBU_237 addendum context from the working file.
Solution: Do not restore the old historical ledger body. Add only the SHINOBU_237 current facts to the active format: `71492..71496` range, DTO size anchors, cursor-owned ring route, GPU presentation boundary, and pending runtime proof.
Rejected Alternatives: Reverting the whole ledger to HEAD, restoring the prior 4000-line body, or leaving SHINOBU_237 absent. Reverting risks deleting another agent's active rewrite; restoring history creates cross-agent churn; omission loses the payload boundary.
Scalability potential: The ledger row records that `GlobalQualityWeight` scales event/particle budgets without changing DTO layout, route, save identity, or rollback boundary.
Hardware Impact: Documentation only. Runtime cost 0 us; integration risk is reduced by preserving BufferID and payload ABI in the active ledger format.

## Decision 38 - Marine Snow Shader Variant Strip
Problem: `_MATH_LOD_LOW` existed as a shader `multi_compile` variant. In compute it hard-returned zero dynamic wake flow, duplicating the runtime low-tier path; in the material shader it had no conditional use. This widens shader variant surface and weakens the stutter/warmup story.
Solution: Removed `_MATH_LOD_LOW` pragmas and the compile-time branch. Dynamic wake cost still scales through `_DynamicWakeParams.y`, `_DynamicWakeDtoParams.y`, and `GlobalQualityWeight`-derived C# parameters, so low-tier behavior remains continuous and runtime-controlled.
Rejected Alternatives: Keeping the variant, replacing it with another keyword, or stripping Unity instancing/stereo pragmas. Keeping the variant adds avoidable warmup/import surface; another keyword is the same problem under a new name; stripping instancing/stereo without Frame Debugger proof risks XR procedural draw regressions.
Scalability potential: Low/Middle/High/Ultra still use one shader binary with continuous quality params. Low tiers collapse wake slots to the low-tier cap; higher tiers raise slots and flow gain without shader variant swapping.
Hardware Impact: Removes one material/compute keyword axis from marine snow import and warmup. Runtime ALU is unchanged on low tier except the existing param-driven branch remains; no CPU particles or readback were added.

## Decision 39 - Dynamic Wake Low-Tier Continuum
Problem: After removing `_MATH_LOD_LOW`, the live dynamic wake path still encoded low tier as a binary 0.5 threshold in mock wake params, C# wake parameter sanitization, and HLSL DTO flow sampling. That violated the continuous `GlobalQualityWeight` rule and could snap wake richness across one threshold.
Solution: Converted the low-tier lane to a saturated continuous weight. C# now derives it from `ResolveDynamicWakeLowTierWeight`, clamps dynamic wake capacity with `math.lerp(16f, 4f, lowTier)`, and HLSL computes `tierSlotLimit` with `lerp(HECTON_DYNAMIC_WAKE_CAPACITY, HECTON_DYNAMIC_WAKE_LOW_TIER_CAPACITY, lowTier)`. DTO flow now uses `saturate(_DynamicWakeDtoParams.y)` instead of `step(0.5, ...)`.
Rejected Alternatives: Keeping the threshold, adding a second shader keyword, or forcing all devices to the low cap. The threshold creates visible/thermal popping; the keyword reopens variant stutter; all-low wastes high-tier visual budget.
Scalability potential: Low collapses toward four dynamic slots and mostly radial fake flow. Middle uses fractional tier weight to increase slots and blend detail. High/Ultra approach the full 16-slot dynamic wake path plus existing event-ring sampling without changing DTO layout or authority route.
Hardware Impact: Adds a few scalar `saturate`/`lerp` operations and removes branch-style tier decisions. On low-end silicon it prevents sudden wake budget jumps; on high-end hardware it preserves overkill wake richness without CPU particles, raycasts, or readback.

## Decision 40 - Mock DTO Wake Quality Lane
Problem: `RefreshDynamicWakeBinding` still wrote `wakeDtoParams` with a hardcoded low-tier lane of `0f` when mock DTO wakes were active. That left the second HLSL wake loop in high-detail mode regardless of quality.
Solution: Compute `wakeLowTierWeight` once from `ResolveDynamicWakeLowTierWeight(_resolvedScalabilityParams.x)` and publish it into both `wakeParams` and `wakeDtoParams`.
Rejected Alternatives: Leaving DTO mocks as a debug-only exception, disabling DTO wakes at low quality, or adding a separate debug keyword. Debug exceptions rot into false proof; disabling DTO wakes hides validation data; another keyword adds variant/warmup surface.
Scalability potential: Low/Middle/High/Ultra now exercise the same dynamic wake continuum in both structured and DTO mock lanes. Low-tier DTO wake flow favors radial fake flow, while Ultra restores the higher-detail force blend without changing DTO layout.
Hardware Impact: Adds no new allocations and one reused scalar. Low-end GPU ALU now follows the same quality-reduced branchless blend for DTO mocks; CPU remains bounded to shader param upload.

## Decision 41 - Continuous Marine Snow Scalability Lanes
Problem: `_MarineSnowScalabilityParams` still came from static Low/Mid/High/Ultra rows and HLSL consumed it through hard tier checks such as `x <= 0.5` and `x >= 1.5`. That contradicted the continuous quality mandate and made flow, turbulence, and collision fidelity snap.
Solution: Replaced table selection with `BuildContinuousScalabilityParams`. It derives flow quality, stagger cadence, SDF collision cadence, and depth collision cadence from `GlobalQualityWeight`, pressure, stress, and policy masks. Particle capacity now lerps through row capacities before pressure clamping. HLSL uses `saturate`, `smoothstep`, `lerp`, and deterministic particle-index dither for collision cadence.
Rejected Alternatives: Keeping the four static rows, only renaming the tier scalar, or running all collision paths at low quality. Static rows pop; renaming hides the same defect; all-collision low quality spends GPU time on visual contact detail that can be dithered.
Scalability potential: Low reduces flow sampling and collision cadence while preserving propwash/wake presentation through radial fakes. Middle increases flow/collision lanes smoothly. High/Ultra progressively add curl fake, turbulence response, bubble depth shrink, and higher active capacity without changing DTO layout or ownership.
Hardware Impact: Adds cheap scalar math in cold scalability refresh and a dither hash in HLSL collision gating. It removes full-lane collision/flow snaps and keeps MX350 pressure below the old all-or-nothing threshold behavior.

## Decision 42 - Continuous Stress Capacity Shed
Problem: `ResolveActiveParticleCount` still snapped capacity to the low marine-snow row when `ResolveSystemStress01() > 0.8f`. That created an abrupt fidelity drop and used the marine-snow row even when the active fluid type was bubbles or debris.
Solution: Replaced the threshold with `math.smoothstep(0.65f, 0.95f, systemStress01)` and lerped capacity toward the low-row capacity for the active fluid type before density/render-scale scaling.
Rejected Alternatives: Keeping the 0.8 threshold, using pressure enum only, or forcing all fluid types through `LowMarineSnowCount`. The threshold pops; pressure enum alone misses continuous stress; marine-snow row is the wrong target for bubble/debris pools.
Scalability potential: Low stress keeps the full quality-derived capacity. Mid stress sheds gradually. High stress approaches low-row capacity without changing DTO layout, Vault identity, or shader payloads.
Hardware Impact: Adds one smoothstep/lerp in a CPU scalar budget calculation. Avoids sudden GPU particle count collapse and prevents oversizing bubble/debris low-stress fallback.

## Decision 43 - Raw Native Blackbox Dumps
Problem: The silt blackbox used `BinaryWriter` field-by-field serialization, and the propwash dump copied each telemetry entry to a local stack value before writing a span. The task calls for raw `ReadOnlySpan<byte>` dump evidence from the native telemetry rings.
Solution: `TryWriteBlackBoxDump` now writes a 16-byte raw header and ring-ordered native telemetry chunks directly from `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr`. `PropwashTelemetryDump.TryWrite` writes one or two raw contiguous chunks from the native ring and catches IO/permission failures.
Rejected Alternatives: Keeping `BinaryWriter`, adding managed byte arrays, or dumping through GPU readback. BinaryWriter is managed field serialization; byte arrays allocate; GPU readback violates the visual-only forensic path.
Scalability potential: No quality-tier behavior changes. Low/Middle/High/Ultra all use the same fixed telemetry ABI and raw dump route.
Hardware Impact: Failure-path CPU work is reduced from per-field/per-entry calls to at most two native span writes per ring. Gameplay hot path remains unaffected.

## Decision 44 - Build Gate After Raw Dump Patch
Problem: Raw dump code changed unsafe native write paths, but project rules forbid compiling while CPU load is above 50 or compiler processes are already active.
Solution: Re-ran the CPU/compiler gate and recorded the result in status, log, and self-audit. CPU was 94.00 and no `dotnet/csc` process was present, so build remained blocked by CPU load alone.
Rejected Alternatives: Launching `dotnet build` under the CPU gate, or claiming compile proof from static scans. The former violates the compile-wall rule; the latter would be a false verification claim.
Scalability potential: No runtime tier behavior changes. Low/Middle/High/Ultra keep the same raw telemetry ABI and continuous quality routes.
Hardware Impact: Avoided adding compiler load to an already saturated workstation. Runtime cost 0 us.

## Decision 45 - WakeSources Kinematic Bridge Fence
Problem: Task 06 required live kinematic wake harvesting, but directly reading `HydrodynamicKccRuntime.KinematicStateDTO` from the SHINOBU renderer would create a sibling Physics/KCC dependency and widen the compile wall. The project already routes vehicle and apex wake facts through `SignalBus<WakeGeneratedSignal>` into VFX-owned `BufferID.WakeSources`.
Solution: Add `HarvestWakeSourcePropwashJob` as the active bridge from existing `WakeSource` rows into `PropwashEventDTO`. `HectonMarineSnowRenderer` now optionally caches `BufferID.WakeSources` only when another VFX owner has already created it, rejects DataVault compaction fences before resolving, subtracts camera AUP before float cast, clamps writes through continuous `GlobalQualityWeight`, and uploads the cursor-ordered propwash GPU buffer only when the ring changed.
Rejected Alternatives: Direct KCC DTO dependency, scanning vehicles/rigidbodies from the renderer, or allocating a private `NativeArray<WakeSource>` fallback. Direct dependency violates unidirectional assembly routing; scene/object scans recreate CPU wake ownership; private arrays violate Vault sovereignty and duplicate the wake fact.
Scalability potential: Low quality writes at least the bounded minimum lane and uses reduced force/radius multipliers; middle weights raise write limits smoothly; high/ultra can bridge the full 16-source visual lane while GPU event sampling still scales independently. DTO layout, rollback boundary, save identity, and BufferID ownership do not change with quality.
Hardware Impact: Adds one bounded Burst `IJob.Run()` over at most 16 existing wake rows and avoids any CPU particle emission, physics raycast, rigidbody iteration, or scene search. Build gate after the patch was blocked by CPU load 100.00 with no active `dotnet/csc` process.

## Decision 46 - WakeSource Ref-Readonly Bridge Read
Problem: `HarvestWakeSourcePropwashJob` filtered an existing VFX `WakeSource` table by first copying each 128-byte row into a local value. The bridge is bounded to 16 rows, but the copy still violates the same L1-copy discipline that Task 03 demanded for kinematic DTO harvest.
Solution: Marked the bridge job `unsafe`, added `PropwashGpuContracts.WakeSourceStrideBytes=128`, validated `UnsafeUtility.SizeOf<WakeSource>()`, and read each row through `NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr` plus `UnsafeUtility.AsRef<WakeSource>` bound as `ref readonly`.
Rejected Alternatives: Keeping the small bounded copy, reducing `WakeSource` fields in SHINOBU, or adding a private compact staging DTO. The copy is unnecessary; trimming another owner's row would cross ownership; a staging DTO duplicates wake truth and adds another copy.
Scalability potential: Low/Middle/High/Ultra keep the same existing WakeSources route. Higher quality may bridge all 16 rows without copying 2KB of source rows per tick, while low quality writes fewer propwash events through the same continuous write-limit curve.
Hardware Impact: Removes up to 16 x 128B struct copies from the bridge pass and preserves direct L1 reads. Build gate after the patch was blocked by CPU load 100.00 with no active `dotnet/csc` process.

## Decision 47 - Hot GraphicsBuffer Resize Fence
Problem: `EnsureParticleBudget()` was called from the gameplay tick and compared a `GlobalQualityWeight`/pressure-derived capacity against `_allocatedParticleCapacity`. Any continuous quality change could release and recreate five particle `GraphicsBuffer`s during play.
Solution: Split buffer allocation capacity from active simulation capacity. Runtime `EnsureParticleBudget()` now refreshes scalability only; `ResizeParticleBuffers()` is fenced to non-playing editor use. Cold allocation uses tuning/max capacity, while `ResolveActiveParticleCount()` clamps dispatch count by `_resolvedParticleCapacity` so `GlobalQualityWeight` still scales active GPU work continuously.
Rejected Alternatives: Keeping hot resize, reallocating only on quality increases, or freezing quality capacity at boot. Hot resize violates Task 14 and can hitch; grow-only still reallocates in play; boot-only quality freezes thermal response and stops active capacity from breathing.
Scalability potential: Low quality lowers dispatched particles, flow/collision cadence, and event sample cost without changing GPU buffer ownership. Middle weights raise active count smoothly. High/Ultra can spend the cold allocation headroom for visual density without reallocating or changing DTO layout.
Hardware Impact: Removes potential release/recreate of two particle buffers, two metadata buffers, and the visible-index buffer from gameplay. Added cost is one `math.min` clamp in active-count calculation. Build gate after the patch was blocked by CPU load 100.00 with no active `dotnet/csc` process.

## Decision 48 - Hot Vault Lease and Snapshot Purity Fence
Problem: `EnsureNativeState()` was called from the gameplay tick and, even after readiness, could walk Vault handles and run default initializers every frame. `ResolveSiltTuningSnapshot()` also used a read-like name while writing a default DTO back into Vault when the version was zero.
Solution: Added an early ready-lease return to `EnsureNativeState()` when `_nativeStateReady` is true, the cached Vault exists, and no compaction fence is active. Renamed the tuning helper to `CaptureSiltTuningSnapshot()` and removed fallback Vault writeback from the snapshot path; owner-phase initialization remains in `InitializeDefaultSiltTuning`.
Rejected Alternatives: Leaving per-tick `TryGetBufferHandle` polling, moving all tuning access into GlobalRegistry, or keeping the `Resolve*` name with comments. Polling violates the hot Registry/Vault boundary; GlobalRegistry would duplicate the data route; comments do not change behavior.
Scalability potential: Low/Middle/High/Ultra keep the same Vault BufferID ownership and quality scaling. Runtime quality can still change active particle count and shader lanes, but handle acquisition/default publication stays boot/cold.
Hardware Impact: Removes repeated Vault handle lookup/default-initializer passes from every hot tick after readiness. Added hot cost is one cached reference/null/fence check. Build gate after the patch was blocked by CPU load 100.00 with no active `dotnet/csc` process.

## Decision 49 - Direct Floating-Origin AUP Read
Problem: SHINOBU renderer hot paths used `GlobalSignals.CurrentRuntimeOriginAup()` to convert runtime positions and publish `_HectonFloatingOriginOffset`. That method is only a wrapper over `HectonFloatingOrigin.CurrentTotalOffsetDouble`, so keeping it in the renderer preserved an unnecessary legacy GlobalSignals bridge.
Solution: Removed the `GlobalSignals` reads from `TryResolveRuntimeAup` and `RefreshHotGpuBindings`. The renderer now reads `HectonFloatingOrigin.CurrentTotalOffsetDouble` directly, validates the double3 with `math.isfinite`, builds AUP through `AbsoluteUniversePosition.FromAbsolutePosition`, and still downcasts only camera-local deltas.
Rejected Alternatives: Keeping the wrapper, caching a second local origin truth, or using absolute floats. The wrapper is an avoidable legacy route; a local origin truth risks drift; absolute floats break the 100km precision rule.
Scalability potential: Low/Middle/High/Ultra keep the same AUP truth and shader payload layout. Quality changes still scale work, not origin authority.
Hardware Impact: Removes two hot wrapper calls per active tick and avoids GlobalSignals dependency in this domain. Added guard is one finite check for the origin double3. Build gate after the patch was blocked by CPU load 100.00 with no active `dotnet/csc` process.

## Decision 50 - Ecosystem Flow-Field Upload Capacity Fence
Problem: `RefreshFlowFieldUpload` could release and recreate `_flowFieldBuffer` during gameplay when the vegetation bridge's `EcosystemFlowFieldCurrentNative.Length` changed. The flow field is a visual advection input, so reallocating a GPU buffer from the hot tick violates the same memory-stability rule as particle buffer resizing.
Solution: Allocate a cold, designer-tunable `flowFieldUploadCapacity` buffer at boot, clamp it to `40401..262144` rows, validate incoming payload size against `gridResolution * gridResolution`, and fence any resize to non-playing editor. If runtime payload size exceeds the cold capacity or is inconsistent, SHINOBU clears flow-field sampling metadata and falls back to zero flow plus existing curl/radial fake paths instead of reallocating GPU memory.
Rejected Alternatives: Keep hot resize, partially upload a mismatched oversized square, downsample on CPU, or edit the vegetation bridge owner. Hot resize can hitch; partial upload risks shader out-of-bounds/misaligned flow; CPU downsample adds per-frame copy/math in the renderer; editing the bridge crosses the SHINOBU domain boundary.
Scalability potential: Low quality already thins flow sampling/collision cadence through continuous `GlobalQualityWeight`; the fixed buffer keeps ownership stable. Middle/High/Ultra can raise `flowFieldUploadCapacity` in editor for larger vegetation grids without C# recompilation or runtime allocation. If a runtime grid exceeds capacity, visual advection degrades to the cheaper turbulence/dear-lie path rather than changing truth ownership.
Hardware Impact: Removes one possible `GraphicsBuffer.Release/CreateStructuredLockBuffer<float2>` sequence from gameplay. Default cold memory cost is 201x201x8 bytes, about 323 KB; upper editor cap is 512x512x8 bytes, about 2 MB. Hot-path added work is integer payload validation and one capacity comparison before the existing memcpy. Build gate after the patch remained blocked by CPU load 100.00 with no active `dotnet/csc` process.

## Decision 51 - Sonar/Fog RenderTexture Runtime Resize Fence
Problem: `EnsureSonarGlowTexture` and `EnsureFogDensityTexture` run from gameplay dispatch paths and could call `Release()` plus `new RenderTexture` whenever camera pixel dimensions or render-scale sliders changed. That is a frame hitch risk and violates cold ownership for auxiliary VFX textures.
Solution: If the sonar/fog texture already exists and dimensions differ during play, keep the existing texture. Non-playing editor keeps the resize path for tuning. First allocation remains in the cold buffer/bootstrap path after camera resolution is known.
Rejected Alternatives: Keep runtime resize, allocate maximum 4K textures blindly, or disable sonar/fog when resolution changes. Runtime resize can hitch; max 4K wastes mobile VR memory; disabling loses visual evidence. Keeping the previous texture is a bounded quality degradation with stable memory.
Scalability potential: Low devices retain the cheap previously allocated texture and continue scaling intensity/render cadence through existing quality lanes. Middle/High/Ultra can still tune render scale in editor and enter play with larger textures. Runtime quality changes affect shader work and intensity, not texture ownership.
Hardware Impact: Removes two potential `RenderTexture.Release/new/Create` sequences from active frames. Hot-path added work is one `Application.isPlaying` branch only when requested dimensions differ. Build gate after the patch remained blocked by CPU load 100.00 with no active `dotnet/csc` process.

## Decision 52 - DataVault Handle Cache Rebind and Optional WakeSources Probe
Problem: The cached-ready `EnsureNativeState` fast path skipped optional `BufferID.WakeSources` discovery after readiness. If another VFX owner created WakeSources later, SHINOBU never consumed it. `BindDataVault` also cleared only the wake-job and telemetry handles, leaving other cached handles stale across a DataVault service rebind until reacquisition side effects overwrote them.
Solution: Add a 30-frame optional WakeSources handle probe while native state is ready and no compaction fence is active, stopping once the handle is acquired. Expand `BindDataVault` to clear every SHINOBU Vault handle and reset the optional probe frame on service change.
Rejected Alternatives: Poll `TryGetBufferHandle` every frame, allocate a private WakeSources fallback, or require a direct KCC/vehicle dependency. Per-frame polling violates the hot path budget; private fallback duplicates wake truth; direct dependency breaches assembly boundaries.
Scalability potential: Low/Middle/High/Ultra keep the same optional WakeSources route. The probe changes only handle acquisition timing, while `GlobalQualityWeight` still controls how many wake rows become propwash events.
Hardware Impact: Before acquisition, the added cost is one `Time.frameCount` check per tick and one DataVault handle lookup every 30 frames. After acquisition, cost returns to zero. Rebind safety prevents stale handle use after service replacement. Build gate after the patch remained blocked by CPU load 100.00 with no active `dotnet/csc` process.

## Decision 53 - Vault Compaction Handle Cache Invalidation
Problem: `RefreshDataVaultBinding` still cleared only `_vehicleWakeJobResultHandle` and `_telemetryRingHandle` when `IDataVault.IsCompactionFenceActive` was true. That left cached silt tuning, dynamic wake, mock flow, optional WakeSources, propwash event/cursor/telemetry/tuning, and wake-profile handles live across a Vault relocation window. The same helper also compared `_dataVault` to a local copy of itself, so its alleged rebind branch could never execute.
Solution: Added `ClearVaultHandleCache()` and routed DataVault service rebind, compaction-fence invalidation, and native lease teardown through it. Removed the impossible self-compare rebind branch; DataVault authority now enters through lifecycle/hot-swap binding, while `RefreshDataVaultBinding` only gates cached readiness and compaction backoff.
Rejected Alternatives: Keep two-handle invalidation, duplicate the full clear list in each path, or poll `GlobalRegistry.DataVault` from the gameplay tick as a fallback. Two-handle invalidation is stale-handle risk; duplicated clear lists drift; hot registry polling violates the cold identity rule.
Scalability potential: Low/Middle/High/Ultra are unchanged. Quality still controls event count, particle count, and shader cadence only; Vault identity, DTO layout, save identity, and authority route stay fixed.
Hardware Impact: Compaction/rebind paths now clear a dozen small handle structs instead of two. Steady-state hot path cost is unchanged; the removed dead branch eliminates misleading no-op logic and prevents stale native-handle reads after Vault relocation.

## Decision 54 - Wake Proximity Particle Alias Fence
Problem: `CS_EvaluateWakeProximity` selected the target particle slot with `eventIndex % particleCount`, while the C# dispatch count was capped only by the continuous propwash event sample budget. If active particle capacity collapsed below the event sample budget, multiple GPU threads could write the same particle slot in one dispatch.
Solution: Cap C# proximity dispatch with `math.min(_activeParticleCount, ComputePropwashEventSampleBudget(...))` and clamp HLSL `sampleBudget` by `particleCount` before the modulo write. This preserves continuous quality scaling while making one dispatch thread map to at most one particle slot.
Rejected Alternatives: Keep modulo aliasing, add atomics around particle writes, or allocate a staging scatter list. Aliasing is nondeterministic visual corruption; atomics serialize a cosmetic pass; staging lists add memory and dispatch complexity for a Dear Lie silt injection.
Scalability potential: Low quality now sheds both event samples and particle writes together when active capacity collapses. Middle/High/Ultra can still consume the higher event budget, bounded by live particle count, without changing DTO layout or authority route.
Hardware Impact: Adds one integer `min` in C# and one `min` in HLSL before the existing branch. Avoids same-slot write hazards on low-capacity frames and prevents wasted proximity SDF work beyond live particle capacity. Build gate after the patch remained blocked by CPU load 100.00 with no active compiler process.

## Decision 55 - Propwash Flow Event Sample Capacity Fence
Problem: The proximity kernel was capped by live particle capacity, but `ComputePropwashEventFlow` still sampled the event budget independently of `_MarineSnowMetaParams.x`. Under stress, active particles can collapse while quality/event count remains high enough to make every remaining particle loop more propwash events than the visual capacity can justify.
Solution: Clamp `ComputePropwashEventFlow` sample budget by live particle count before the event loop. The same helper feeds `CSMain` and `CS_IntegrateSiltParticles`, so one HLSL scalar fence covers both advection paths.
Rejected Alternatives: Leave event-loop cost independent of particle capacity, add a second quality keyword, or prefilter events on CPU. Independent cost wastes ALU during stress shedding; keywords reintroduce shader variant surface; CPU prefilter breaks the GPU-owned Dear Lie route.
Scalability potential: Low and stressed frames now shed propwash event-loop work proportionally to active visible capacity. Middle/High/Ultra still scale up to the full continuous event sample curve when particle capacity exists to show the detail.
Hardware Impact: Adds one uint read/min in HLSL per particle flow evaluation and can remove hundreds of event-distance checks per live particle on low-capacity frames. No DTO, Vault, or authority route changes.

## Decision 56 - Continuous Stagger Cadence Dither
Problem: `BuildContinuousScalabilityParams` wrote a float cadence value, but `ShouldRunStaggeredRate` cast it to a uint bitmask and used `&`. Non-power-of-two masks produce stepped, non-monotonic cadence and violate the continuous GlobalQualityWeight route for flow/fog/sonar work.
Solution: Publish flow cadence as a normalized `flowQuality` lane and route `ShouldRunStaggeredRate` through `ShouldRunQualityLane` with deterministic hash dither. Flow, fog density, and sonar accumulation now thin by a continuous probability, not an integer bitmask.
Rejected Alternatives: Keep the bitmask, quantize to power-of-two masks, or add tier keywords. Bitmasks are discrete and non-monotonic; power-of-two quantization still pops; keywords add shader variant and warmup surface.
Scalability potential: Low/stressed devices smoothly reduce staggered flow/fog/sonar work. Middle weights probabilistically add samples without a cadence snap. High/Ultra converge to full-rate lanes when `flowQuality` approaches one.
Hardware Impact: Replaces a bitwise mask with one existing hash compare. Low-tier devices shed work continuously; high-tier cost is unchanged because the dither helper returns true when gate is one. Build gate after the patch remained blocked by CPU load 100.00 with no active compiler process.
