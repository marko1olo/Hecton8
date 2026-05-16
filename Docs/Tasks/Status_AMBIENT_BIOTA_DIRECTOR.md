# Status_AMBIENT_BIOTA_DIRECTOR

Agent ID: AMBIENT_BIOTA_DIRECTOR
Domain: AI/ENVIRONMENT
Task Count: 18
Status: VERIFIED MASTER GRADE - BIOTA PULSING (DOTNET BUILD GREEN; UNITY RUNTIME PENDING)

## Prompt Extraction Evidence

- Source: `Docs/Tasks/CURRENT_BATCH.md`
- Extraction command: PowerShell `Get-Content -Raw` with regex for `<AGENT_PROMPT id="AMBIENT_BIOTA_DIRECTOR">...`
- Result: prompt recovered after injection.
- Required first line: `PROMPT IDENTIFIED: AMBIENT_BIOTA_DIRECTOR | DOMAIN: AI/ENVIRONMENT | TASK COUNT: 18`

## Relevant Mandates Read

- `AI_Flocking_Boids_Swarm_SpatialHash_Logic.txt`
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `AI_Director_Encounter_Manager.txt`
- `ARCH_Global_Registry_ServiceLocator_DI_Init.txt`
- `ARCH_Signal_Lane_Segregation.txt`
- `MATH_Deterministic_RNG_SlotMachine.txt`
- `MATH_AUP_Determinism_Sync.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`

## Loop 1: Tasks 1-5

- [x] 1. PURGE_INSTANTIATE
  - DOD practice: runtime ambient/fish scan plus new SOA director path; no `Object.Instantiate`, no `Instantiate`, no `Update`, no `Random.Range`.
  - Rejected alternative: pooled GameObject fish spawn in a loop; still pays transform/component activation churn and violates GPU stream target.
  - Microsecond estimate: avoids 2000-8000 us spawn spikes per 64-object burst versus Unity object instantiation; steady-state scan cost 0 us/frame.
- [x] 2. SINGLETON_KILL
  - DOD practice: added `IAmbientBiotaService` and `GlobalRegistry.AmbientBiota` slot; no `AmbientLifeManager.Instance` dependency found or introduced.
  - Rejected alternative: static singleton owner; couples ambient life to scene load order and blocks hot-swap/testing.
  - Microsecond estimate: removes 1-3 us/frame of singleton/null scene-path drift once consumers bind the service directly.
- [x] 3. DATA_EVICTION
  - DOD practice: reserved `BufferID.BiotaAUPs`, `BufferID.BiotaVelocities`, `BufferID.BiotaStates`; director requests fixed buffers from `GlobalDataVault`.
  - Rejected alternative: local persistent arrays hidden inside a MonoBehaviour; not visible to GPU/VFX consumers and harder to police for leaks.
  - Microsecond estimate: avoids 50-150 us cold-path realloc/resize bursts and 0 B/frame runtime GC.
- [x] 4. BIOTA_SPAWN_JOB
  - DOD practice: Burst `IJob` activates dead SOA slots by deterministic hash, biomass, carrying capacity, and bounded slow-tick spawn budget.
  - Rejected alternative: `Random.Range` plus per-fish components; nondeterministic and main-thread heavy.
  - Microsecond estimate: 15-60 us per slow tick on low tier for 2048 slots, replacing millisecond-scale object creation bursts.
- [x] 5. DETERMINISTIC_DRIFT
  - DOD practice: Burst `IJobParallelFor` drifts one modulo bucket per frame using deterministic Brownian noise plus abyssal flow input.
  - Rejected alternative: per-fish MonoBehaviour movement; scales with active objects and burns transform writes.
  - Microsecond estimate: 8-30 us/frame low tier, 40-90 us/frame high tier before GPU draw integration; visual density scales with capacity.
- [x] Compile verification after Tasks 1-5: [BLOCKED BY DEPENDENCY]
  - Command: `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false`
  - Result: failed in unrelated project-wide errors outside `AI/Ambient`: missing `JobAdmissionLane` references, missing `HectonShaderGlobalDataVaultBridge`, missing signal types in `GlobalSignals`, missing voxel-debris constants, and stale generated project references.
  - Local note: current generated `.csproj` files do not include the new `Hecton8.AI.Ambient` assembly until Unity regenerates project files. The asmdef directly references `Hecton8.Core`, `Hecton8.Core.Contracts`, and `Hecton8.Core.Memory`.

## Pending Tasks

- [x] 6. AUP_INTEGRITY
  - DOD practice: spawn/drift/dehydrate bounds now use `double3` deltas and squared distance checks against the AUP bubble.
  - Rejected alternative: `float3` world-distance checks; too jitter-prone across 5000 m AUP cells.
  - Microsecond estimate: 0-5 us/frame cost increase on active bucket; prevents centimeter/meter drift bugs at depth.
- [x] 7. MODULO_BUCKETING
  - DOD practice: drift updates only `BucketId & 15 == ActiveSlowBucket & 15`, using `ISimulationBucketer` when available.
  - Rejected alternative: scanning and integrating all biota every frame; violates low-tier frame budget.
  - Microsecond estimate: keeps 2048 low-tier slots to roughly 128 updates/frame; estimated 70-90% drift CPU avoided versus full sweep.
- [x] 8. LOW_TIER_FAKE
  - DOD practice: MX350/low profile uses billboard flags, ring spawn offsets, triangle noise, lower velocity blend, and no collision path.
  - Rejected alternative: physically simulated plankton/fish steering; visual-only background organisms do not justify physics.
  - Microsecond estimate: estimated 15-40 us/frame saved on low tier by avoiding normalization-heavy/high-tier reaction math for the common path.
- [x] 9. HIGH_END_OVERKILL
  - DOD practice: High/Ultra enables headlight-cone dot test, flee vector, panic emission ramp, larger radius/capacity, and richer spawn scale.
  - Rejected alternative: mobile-quality visuals on RTX; high tier should spend saved CPU on density/reactivity.
  - Microsecond estimate: adds estimated 20-60 us/frame only on high-tier active bucket; buys visible light-avoidance and biolume panic.
- [x] 10. REACTIVE_VFX
  - DOD practice: expired/out-of-bubble biota mark `ReservedDebrisPending`; late-frame drains bounded organic `DebrisSpawnSignal` packets through the typed lane.
  - Rejected alternative: new ambient debris signal or GameObject particle spawning; duplicate lanes and object churn were unnecessary.
  - Microsecond estimate: bounded to 16 debris signals/late frame; avoids unbounded VFX burst and managed allocation.
- [x] 11. STP_STABILIZATION
  - DOD practice: persistent `BiotaVelocities` now carries the smooth per-slot motion vector source for future quad shaders; velocity is finite-clamped and survives between buckets.
  - Rejected alternative: renderer-side previous-frame reconstruction from CPU matrices; would rebuild matrices and smear during submarine travel.
  - Microsecond estimate: renderer binding still pending in task 16; CPU-side stabilization data cost is in the existing velocity write.
- [x] 12. NAN_VACCINATION
  - DOD practice: guards delta time, velocity, target velocity, AUP offsets, distance squares, age, and every `math.rsqrt` normalization/clamp path.
  - Rejected alternative: letting NaNs reach GPU buffers; one invalid payload can poison mobile compute/render lanes.
  - Microsecond estimate: estimated 1-4 us/frame branch cost on active bucket; prevents catastrophic GPU fault.
- [x] 13. BLACKBOX_LOGGING
  - DOD practice: `BiotaTelemetryRing` and `BiotaTelemetryCursor` are vault-owned 300-frame buffers; fault sanitation dumps to `Docs/AgentLogs/Dump_AMBIENT_BIOTA_DIRECTOR.bin`.
  - Rejected alternative: managed log strings or exception-only diagnostics; neither survives crash analysis.
  - Microsecond estimate: one fixed telemetry write/late frame, estimated under 2 us; dump is fault-only.
- [x] 14. TRIPLE_STRIKE_REPAIR
  - DOD practice: used existing `BufferID.BiotaMacroHydrationCounters`, `BiotaTelemetryRing`, and `BiotaTelemetryCursor`; no new BufferID dependency was invented.
  - Rejected alternative: direct `H8Memory.Allocate` scratch counters; violates DataVault sovereignty.
  - Microsecond estimate: 0 runtime delta versus direct persistent allocation; improves leak accounting.
- [x] 15. HOMEOSTASIS_ADAPTATION
  - DOD practice: if `GlobalSignals.SystemStress01 > 0.8`, ambient radius clamps to 30 m; high-tier radius expansion is suppressed under stress.
  - Rejected alternative: fixed 100 m bubble regardless of hardware pressure.
  - Microsecond estimate: low-tier/stress path reduces active candidates outside 30 m; expected 40-70% fewer live slots after steady-state cull.
- [x] 16. INDIRECT_DRAW_CALL
  - DOD practice: added optional `Graphics.RenderMeshIndirect` path fed by GPU buffers for AUPs, velocities/motion vectors, and states; no CPU-side matrix building.
  - Rejected alternative: per-slot `Matrix4x4` construction or GameObject quads; both violate the GPU stream objective.
  - Microsecond estimate: replaces CPU matrix building with bulk buffer upload and one indirect draw; profiler blocked by foreign compile errors.
- [x] 17. BIOME_SYNC
  - DOD practice: consumes existing typed `BiomeChangedSignal` snapshot via `ReadOnlySpan<BiomeChangedSignal>` and folds biome hash into species/emission selection.
  - Rejected alternative: inventing a new ambient biome signal or direct `BiomeMatrixDirector` hard dependency.
  - Microsecond estimate: O(signal count) cold/slow path scan, typically sub-1 us when no biome transition signals are present.
- [x] 18. FINAL_VALIDATION
  - Status: DOTNET BUILD GREEN
  - Command: `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false`
  - Result: succeeded, 0 warnings, 0 errors; see `Docs/AgentLogs/Dump_AMBIENT_BIOTA_DIRECTOR_BUILD.txt`.
  - Local note: Unity runtime/profiler verification is still pending; only compile validation is green.

## Loop 2: Tasks 6-10

- [x] Prompt re-read after task group: `CURRENT_BATCH.md` contained `<AGENT_PROMPT id="AMBIENT_BIOTA_DIRECTOR">` with 18 tasks.
- [x] Static audit after Tasks 6-10: no forbidden `Instantiate`, `Object.Instantiate`, `Random.Range`, `Update(`, `foreach`, `string.Format`, `EventBus`, direct `H8Memory.Allocate`, or private `NativeArray` fields in `Assets/_Project/Scripts/AI/Ambient`.

## Loop 3: Tasks 11-15

- [x] Static audit after Tasks 11-15: `AmbientBiotaState` and `AmbientBiotaTelemetryEntry` use `[StructLayout(LayoutKind.Explicit, Pack = 1)]`; `BinaryLayoutManifest` asserts 32 B and 64 B layouts.
- [x] Compile verification after Tasks 6-15: [BLOCKED BY DEPENDENCY]
  - Command: `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false /p:OutputPath=.codexbuild\ambient_validation_core\`
  - Result: failed in unrelated files outside `AI/Ambient`: `World/SargassumMicroFaunaBoids.cs` missing `ResolveVaultBuffer` and `_leviathanNode*Native`; `RepairTool.cs` has unassigned `localPoint`.
  - Artifact: `Docs/AgentLogs/Dump_AMBIENT_BIOTA_DIRECTOR_BUILD.txt`.
  - Local note: Bee has an ambient asmdef response file, but its `Hecton8.Core.ref.dll` reference is stale/missing while the core build is blocked by other domains.

## Loop 4: Tasks 16-17

- [x] Prompt re-read after task group: `CURRENT_BATCH.md` contained `<AGENT_PROMPT id="AMBIENT_BIOTA_DIRECTOR">` with 18 tasks and the explicit indirect draw / biome sync requirements.
- [x] Static audit after Tasks 16-17: no forbidden `Instantiate`, `Object.Instantiate`, `Random.Range`, `Update(`, `foreach`, `string.Format`, `EventBus`, direct `H8Memory.Allocate`, or private `NativeArray` fields in `Assets/_Project/Scripts/AI/Ambient`.
- [x] Render path note: indirect draw is dormant when no material is assigned; when bound, it uses persistent GPU buffers and one `Graphics.RenderMeshIndirect` call.
- [x] Biome path note: no authoritative current-biome `BufferID` was found in the vault; used the existing typed `BiomeChangedSignal` lane and did not create a duplicate signal.
- [x] Compile verification after Tasks 16-17: [BLOCKED BY DEPENDENCY]
  - Command: `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false`
  - Result: still blocked by `World/SargassumMicroFaunaBoids.cs` and `RepairTool.cs`, not by `AI/Ambient`.
  - Artifact: `Docs/AgentLogs/Dump_AMBIENT_BIOTA_DIRECTOR_BUILD.txt`.

## Loop 5: Omega Polish And Final Validation

- [x] Prompt re-read before Omega: `CURRENT_BATCH.md` lines 2134-2189 contained `<AGENT_PROMPT id="AMBIENT_BIOTA_DIRECTOR" role="AI_PROGRAMMER" chat_name="The Biota Weaver">`, 18 tasks, and the Omega mandate.
- [x] Omega `foreach` audit: no `foreach` remains in `Assets/_Project/Scripts/AI/Ambient`.
- [x] Omega advection audit: `AmbientBiotaDriftJob.Execute` contains no `if (` branch source; active/bucket/dt/light/expiry/fault decisions are mask-driven with `math.select` and fixed struct selectors.
- [x] Static forbidden-pattern audit: no `private NativeArray`, direct `H8Memory.Allocate`, `Update`, `LateUpdate`, `FixedUpdate`, `foreach`, `string.Format`, `Instantiate`, `Random.Range`, `EventBus`, managed delegate patterns, `Camera.main`, scene find, coroutine, or `Resources.Load` in `Assets/_Project/Scripts/AI/Ambient`.
- [x] Diff hygiene: `git diff --check -- Assets/_Project/Scripts/AI/Ambient/AmbientBiotaDirector.cs Docs/Tasks/Status_AMBIENT_BIOTA_DIRECTOR.md Docs/AgentLogs/Rationale_AMBIENT_BIOTA_DIRECTOR.md Docs/AgentLogs/LOG_AMBIENT_BIOTA_DIRECTOR.md` passed; only CRLF normalization warnings.
- [x] Final compile: `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal /p:UseSharedCompilation=false` succeeded with 0 warnings and 0 errors, writing `Temp\bin\Debug\Hecton8.Core.dll`.
- [x] Runtime caveat: Unity Editor import, Play Mode, GCMonitor, Frame Debugger, and GPU profiler proof were not run in this shell session; measured microseconds remain absent.

## Phase 1 Audit Notes

- Runtime ambient/fish scan found no direct `AmbientLifeManager.Instance`.
- Runtime ambient/fish scan found no direct `Object.Instantiate` in an ambient-fish owner. Existing `ObjectPoolManager` and editor instantiation paths are not ambient fish scripts.
- `SargassumMicroFaunaBoids` exists under `World`; it is not edited in this phase because the prompt's authoritative write domain is `Assets/_Project/Scripts/AI/Ambient/`.
