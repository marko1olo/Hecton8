# Status_AI_ENCOUNTER_DIRECTOR

Mandates loaded: AI_Director_Encounter_Manager; MATH_Coordinate_Precision_AUP_FloatingOrigin; MATH_Rsqrt_i3_SIMD; OPT_Zero_GC_Policy_AllocFree_Mandate; OPT_Native_Memory_Collections_JobSystem_Protocol; DBG_Telemetry_Crash_Reporting_PostMortem; ARCH_Global_Registry_ServiceLocator_DI_Init; OPT_Cinematic_Cheat_Protocol_Visual_Fake_First.

Prompt extraction:
- PASS 1: `CURRENT_BATCH.md` block read before coding.
- PASS 2: `CURRENT_BATCH.md` block re-read after the 1-5 implementation pass.
- PASS 3: `CURRENT_BATCH.md` block re-read after the 6-10 implementation pass.
- PASS 4: `CURRENT_BATCH.md` block re-read before telemetry/recon closure.
- PASS 5: `CURRENT_BATCH.md` block re-read before task 6 purchase-loop hardening.
- PASS 6: `CURRENT_BATCH.md` block re-read before cold-tick/per-frame performance hardening.
- PASS 7: `CURRENT_BATCH.md` block re-read before terrain-rect biome heatmap hardening.
- PASS 8: `CURRENT_BATCH.md` block re-read before 16-slot despawn-lane hardening.
- PASS 9: `CURRENT_BATCH.md` block re-read before predator AUP GPU double-buffer hardening.
- PASS 10: `CURRENT_BATCH.md` block re-read before terrain-boundary and spawn-request sentinel hardening.
- PASS 11: `CURRENT_BATCH.md` block re-read before authoritative native spawn-request fail-closed hardening.
- PASS 12: `CURRENT_BATCH.md` block re-read before job-finalization, teardown, and predator AUP publication batching hardening.
- PASS 13: `CURRENT_BATCH.md` block re-read before dispatcher LateFrame output application hardening.
- PASS 14: `CURRENT_BATCH.md` block re-read before predator AUP dirty-event batching and Unity editor validation.
- PASS 15: `CURRENT_BATCH.md` block re-read before disposal hygiene, forced-threat validation, and predator LOS semantic correction.
- PASS 16: `CURRENT_BATCH.md` block re-read before headless free-slot cursor, tracked-predator AUP fallback, and reciprocal math hardening.
- PASS 17: `CURRENT_BATCH.md` block re-read before service-registration lifecycle audit and final verification.

Task checklist:
- [x] 1. STRESS ACCUMULATOR | DOD: existing Burst cold tick now preserves low O2, health, proximity, velocity, acoustic inputs and slow safe-idle decay | Rejected: frame-by-frame managed stress object | Estimate: 7 us/cold tick.
- [x] 2. PACING CURVE | DOD: BuildUp/Peak/Decay/Relax intensity uses sine/cosine wave functions | Rejected: triangle/smoothstep-only pacing | Estimate: 2 us/cold tick.
- [x] 3. SPAWN CREDIT ECONOMY | DOD: BuildUp adds credit regen from base rate plus intensity/stress and clamps to 1000 | Rejected: fixed one-spawn cadence | Estimate: 1 us/cold tick.
- [x] 4. PREDICTIVE AUP SPAWNING | DOD: candidate anchor leads velocity by 200m and rejects behind-vector spawns unless stationary | Rejected: static radius around player | Estimate: 6 us/cold tick at 32 candidates.
- [x] 5. HEADLESS POOL ALLOCATION | DOD: persistent `NativeList<HeadlessEntity>` with 1024 fixed slots, no `Instantiate` in director spawn path | Rejected: FaunaDirector GameObject hydration | Estimate: saves object spawn milliseconds, slot write under 2 us.
- [x] 6. THREAT BUDGETING MATH LOD | DOD: canonical costs Crab/Drone/Swarm 5, Shark/Stalker 50, Leviathan 500; non-forced buying reselects tiers and fills a fixed 16-slot native request lane until credits, class caps, active slots, or spawn visibility stop it | Rejected: 3-field same-tier output lane | Estimate: 6 us/cold tick worst-case.
- [x] 7. FRUSTUM REJECTION | DOD: dot/distance visible-cone rejection before existing frustum plane AABB rejection | Rejected: physics occlusion query | Estimate: 4 us/cold tick.
- [x] 8. DESPAWN GARBAGE COLLECTION | DOD: headless tokens beyond 400m write a fixed 16-slot native despawn lane and mark pool slots free on main-thread application; overflow remains counted active until a later tick | Rejected: managed destroy/recall-only path and 3-ID legacy-only output | Estimate: 6 us/cold tick scan.
- [x] 9. EVENT BUS LISTENER | DOD: `HectonDirectorAI` drains `EntityDeathSignal` through `GlobalSignals` with fixed 16-signal budget and refunds 50% | Rejected: direct fauna callback dependency | Estimate: under 5 us at full drain.
- [x] 10. BIOME MASKING | DOD: spawn allocation hashes Data Monolith heatmap cell into a biome byte and class-gates with depth fallback | Rejected: ScriptableObject/LINQ filtering | Estimate: 2 us/spawn attempt.
- [x] 11. PREDATOR ASSIGNMENT | DOD: active Stalker/Swarm/Leviathan slots publish into 16-slot A/B `_PredatorAUPBuffer` globals via `LockBufferForWrite` upload utility | Rejected: GameObject predator registry mutation, direct `HectonBoidController` coupling without registry ownership, and single-buffer GPU upload lane | Estimate: 4 us upload setup plus graphics upload.
- [x] 12. ZERO-GC | DOD: persistent native arrays/lists, fixed arrays, no LINQ/coroutines in modified path; file dump only on NaN crash path | Rejected: managed collections in spawn loop | Estimate: 0 B/frame target pending profiler.
- [x] 13. NO COROUTINES | DOD: Director remains dispatcher-driven and ColdTick-gated at 1Hz | Rejected: `IEnumerator` pacing state machine | Estimate: avoids coroutine allocator/state overhead.
- [x] 14. RECONNAISSANCE PROTOCOL | DOD: `Docs/AgentLogs/RECON_AI_ENCOUNTER_DIRECTOR.md` records `Instantiate` offenders in AI/spawner scripts | Rejected: hand-waved "no Instantiate" report | Estimate: N/A editor scan.
- [x] 15. TELEMETRY INTEGRATION | DOD: 300-entry native blackbox writes per-frame sequence, `DirectorStateHash`, `ActiveThreatCount`, stress, intensity, credits, speed, position; non-finite position/velocity/state dumps binary file | Rejected: managed Debug.Log telemetry | Estimate: 3 us/frame ring write.

Strict iterative loops:
- Loop 1: Reviewed constructor/reset allocation path; moved predator GPU buffer creation behind runtime `OnEnable`.
- Loop 2: Reviewed despawn path; fixed headless despawn processing when `FaunaDirector` is null.
- Loop 3: Reviewed prompt extraction and predictive spawn fairness; added dot/distance visible-cone rejection before plane tests.
- Loop 4: Reviewed zero-GC risks; confirmed no LINQ/coroutine/Instantiate in modified encounter path, crash dump remains exceptional path.
- Loop 5: Reviewed validation and console; modified scripts show no Unity console errors, project compile blocked by unrelated external-agent errors.
- Omega Polish: Read `<POLISH_MANDATE id="OMEGA_POLISH">` after all tasks were checked. Added 48-byte aligned blackbox entries, hoisted frustum extents, reused candidate distance score, and ran `dotnet build Hecton8.Core.csproj`.
- Continuation pass 2026-05-12: Re-read status/rationale, AGENTS.md, AI/Zero-GC/Telemetry mandates, and the `AI_ENCOUNTER_DIRECTOR` prompt. Hardened struct layout, reciprocal math, distant despawn refunds, and Despair Mode budget capping.
- Continuation pass 2 2026-05-12: Added independent blackbox frame sequence, velocity NaN detection, `float3` deterministic seed overload for Burst job path, and documented predator AUP integration boundary.
- Continuation pass 3 2026-05-12: Re-read prompt, replaced 3-request spawn output with a persistent 16-slot `NativeArray<EncounterSpawnRequest>`, enforced canonical threat costs after authoring data, and restored the predator AUP buffer service bridge method.
- Continuation pass 4 2026-05-12: Re-read prompt/mandates and fixed cold-job race exposure, deferred death-signal drains while a job is active, and rolled back `ActiveEnemyCount` on failed main-thread spawn/despawn application.
- Continuation pass 5 2026-05-12: Moved 1024-slot headless token refresh from per-frame `Advance()` to the 1Hz cold-tick schedule boundary and documented the single-lane spawn request safety invariant.
- Continuation pass 6 2026-05-12: Biome masking now samples the Data Monolith heatmap through the active terrain payload rect when available, falling back to deterministic wrapped coordinates only when the terrain payload is unavailable.
- Continuation pass 7 2026-05-12: Replaced legacy 3-ID despawn output with a persistent 16-slot `NativeArray<int>` despawn lane, added three-paragraph safety comments for request buffers, and fixed saturated-despawn active-count accounting.
- Continuation pass 8 2026-05-12: Replaced the single predator AUP graphics buffer with A/B `GraphicsBuffer` upload buffers and active-buffer publication through the existing registry bridge.
- Continuation pass 9 2026-05-12: Fixed terrain-rect biome sampling to reject out-of-bounds payload coordinates instead of clamping to terrain edges, added invalid native spawn-request rollback, and verified `Hecton8.Core.csproj` builds cleanly.
- Continuation pass 10 2026-05-12: Re-read prompt and fixed `ApplySpawnRequests()` so invalid native request slots fail closed instead of falling back to legacy fields; hoisted spawn-candidate normalization denominator to one reciprocal per cold tick.
- Continuation pass 11 2026-05-12: Re-read prompt/status/rationale and verified LateFrame cold-job output completion, force-stop director teardown/reset on disable, destroy-time registry cleanup, and batched predator AUP publication after spawn/despawn output application.
- Continuation pass 12 2026-05-12: Re-read prompt/status/rationale and moved non-forced cold-job output application from dispatcher `Tick()` to `LateFrameTick`, kept force completion only for teardown/reset, and added destroy-time dispatcher unregistration.
- Continuation pass 13 2026-05-12: Re-read prompt/status/rationale and tightened predator AUP publication to dirty-event batching: non-predator releases no longer publish, spawns mark dirty only for Stalker/Swarm/Leviathan, and completed output publishes once after spawn/despawn application.
- Continuation pass 14 2026-05-12: Re-read prompt/status/rationale and fixed synchronous native disposal when no job dependency exists, rejected invalid forced threat classes in the Burst job before budget consumption, and corrected predator obstruction-ray semantics so a raycast hit means blocked LOS.
- Continuation pass 15 2026-05-12: Re-read prompt/status/rationale and hardened the headless pool allocator with a wraparound free-slot cursor, extended dirty-event predator AUP publication to the tracked-predator fallback lane, and replaced remaining cold scalar divides with reciprocal forms.
- Continuation pass 16 2026-05-12: Re-read prompt/status/rationale, audited `GlobalRegistry` dispatcher registration semantics, kept encounter service publication independent of dispatcher availability, and retained a zero-allocation `Start()` retry for dispatcher lane registration.

Verification:
- `validate_script Assets/_Project/Scripts/EncounterDirector.cs`: PASS, 0 diagnostics after predator AUP dirty-event batching.
- `validate_script Assets/_Project/Scripts/HectonDirectorAI.cs`: TOOL HEURISTIC FAIL, reports duplicate `BuildEventOffsetDirectionLut`; `rg` shows one declaration and one call only, pre-existing validator false positive.
- Unity console filtered `EncounterDirector`: 0 errors.
- Unity console filtered `HectonDirectorAI`: 0 errors.
- `git diff --check -- EncounterDirector.cs HectonDirectorAI.cs`: PASS, no whitespace errors; CRLF warning only.
- Forbidden-pattern scan on `EncounterDirector.cs`/`HectonDirectorAI.cs`: PASS for no `foreach`, LINQ, `.Complete(`, `Instantiate`, `Destroy`, `FindObject`, `GameObject.Find`, coroutine, hot string format, or `.ToString()` hits; only `NativeDisableParallelForRestriction` and cold retry `TryGetComponent` hits remain.
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: PASS, 0 warnings, 0 errors after predator AUP A/B buffer hardening.
- `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: PASS, 0 warnings, 0 errors after predator AUP A/B buffer hardening.
- `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:q /clp:ErrorsOnly`: PASS, 0 warnings, 0 errors after terrain-boundary and spawn-request sentinel hardening.
- `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: PASS, 0 warnings, 0 errors after authoritative native spawn-request hardening.
- `git diff --check -- EncounterDirector.cs HectonDirectorAI.cs`: PASS after job-finalization and predator AUP publication batching hardening; CRLF warning only.
- Forbidden-pattern scan on `EncounterDirector.cs`/`HectonDirectorAI.cs`: PASS after latest hardening for no `foreach`, LINQ, `.Complete(`, `Instantiate`, `FindObject`, `GameObject.Find`, coroutine, hot string format, or `.ToString()` hits. Raw `Destroy` scan still hits `OnDestroy` method name only.
- Historical: `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false` was previously blocked by external `HectonPlayerMovement.cs` errors; a later full rerun below passed with 0 warnings and 0 errors.
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: PASS, 0 warnings, 0 errors after dispatcher LateFrame output hardening.
- `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: PASS, 0 warnings, 0 errors after dispatcher LateFrame output hardening.
- `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: PASS, 0 warnings, 0 errors after predator AUP publication batching check.
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: PASS, 0 warnings, 0 errors after predator AUP dirty-event batching.
- `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: PASS, 0 warnings, 0 errors after predator AUP dirty-event batching.
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: initially BLOCKED after build-server shutdown by missing `Temp/obj/Hecton8.Core/project.assets.json`; not a code error.
- `dotnet restore Hecton8.Core.csproj --nologo -v:q`: PASS, regenerated project assets.
- `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: PASS after disposal/LOS hardening, 46 warnings, 0 errors.
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: PASS after dependency build, 2 warnings, 0 errors.
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies --nologo -v:minimal /clp:WarningsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: PASS after dependency build, 0 warnings, 0 errors.
- Unity MCP editor validation: CONNECTED to `Hecton8@5898b2fd69afdd2d`; `EncounterDirector.cs` validates clean; `HectonDirectorAI.cs` is blocked only by the known MCP duplicate-method heuristic, and `rg` confirms one declaration plus one call for `BuildEventOffsetDirectionLut`.
- Unity console filtered `EncounterDirector`: 0 errors after reconnect.
- Unity console filtered `HectonDirectorAI`: 0 errors after reconnect.
- Unity console global: 0 errors after reconnect. Earlier `Assets/_Project/Scripts/UI/PDAMapTab.cs` shader property ID errors were external/stale and are no longer present.
- Unity MCP latest retry after disposal/LOS hardening: PENDING, `mcpforunity://instances` reports `instance_count: 0`.
- `git diff --check -- Assets/_Project/Scripts/EncounterDirector.cs Assets/_Project/Scripts/HectonDirectorAI.cs`: PASS after headless cursor/tracked AUP hardening, no whitespace errors; CRLF warning only.
- Forbidden-pattern scan on `EncounterDirector.cs`/`HectonDirectorAI.cs`: PASS after headless cursor/tracked AUP hardening for no `foreach`, LINQ, `.Complete(`, `Instantiate`, `FindObject`, `GameObject.Find`, coroutine, hot string format, or `.ToString()` hits. Raw `Destroy` scan still hits `OnDestroy`; known `NativeDisableParallelForRestriction` and cold retry `TryGetComponent` hits remain.
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: PASS after headless cursor/tracked AUP hardening, 0 warnings, 0 errors.
- `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: PASS after headless cursor/tracked AUP hardening, 0 warnings, 0 errors.
- Unity MCP latest retry after headless cursor/tracked AUP hardening: BLOCKED, HTTP transport send failure to `http://127.0.0.1:8088/mcp`; no editor validation claimed for this pass.
- `git diff --check -- Assets/_Project/Scripts/EncounterDirector.cs Assets/_Project/Scripts/HectonDirectorAI.cs Docs/Tasks/Status_AI_ENCOUNTER_DIRECTOR.md Docs/AgentLogs/Rationale_AI_ENCOUNTER_DIRECTOR.md Docs/AgentLogs/LOG_AI_ENCOUNTER_DIRECTOR.md`: PASS after service-registration lifecycle audit, CRLF warnings only.
- Forbidden-pattern scan on `EncounterDirector.cs`/`HectonDirectorAI.cs`: PASS after service-registration lifecycle audit for no forbidden hot-path hits.
- `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: PASS after service-registration lifecycle audit, 0 warnings, 0 errors.
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: PASS after service-registration lifecycle audit, 0 warnings, 0 errors.
- Unity MCP latest retry after service-registration lifecycle audit: BLOCKED, HTTP transport send failure to `http://127.0.0.1:8088/mcp`; no editor validation claimed for this pass.

Final status: PENDING VERIFICATION.
