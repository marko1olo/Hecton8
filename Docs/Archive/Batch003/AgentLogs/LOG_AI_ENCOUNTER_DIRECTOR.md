# LOG_AI_ENCOUNTER_DIRECTOR

## 2026-05-12

What was wrong:
- Encounter pressure could still rely on static/scene-facing spawn paths and did not maintain a 1024-slot headless threat pool.
- Spawn math did not buy multiple threats from a 500-credit Leviathan-scale budget.
- Death refunds were not wired through `EntityDeathSignal`.
- Headless predator positions were not exposed through the 16-slot predator AUP shader buffer.
- Director telemetry did not write `DirectorStateHash` and `ActiveThreatCount` into a 300-frame blackbox ring.

What was done:
- `EncounterDirector.cs` now owns persistent `NativeList<HeadlessEntity>` slots, fixed native telemetry arrays, and a 16-slot predator AUP upload lane.
- ColdTick spawn selection now leads player velocity by 200m, rejects behind-vector spawns while moving, rejects likely camera-cone candidates, then applies the existing frustum plane test.
- Spawn credits now scale through BuildUp and threat defaults are Drone/Crab 5, Stalker/Shark 50, Leviathan 500 with buy-until-credit-depleted behavior capped at 3 requests per cold tick.
- Headless despawn frees pool slots beyond 400m and death-signal refunds add 50% cost back to Spawn Credits.
- Biome masking uses the Data Monolith heatmap cell as a biome byte, with matrix-index fallback and class/depth gates.
- `HectonDirectorAI.cs` now creates predator GPU resources on runtime enable and drains `EntityDeathSignal` through `GlobalSignals` with a fixed 16-signal budget.
- `RECON_AI_ENCOUNTER_DIRECTOR.md` logs AI/spawner `Instantiate` offenders; none are in this director path.

Cinematic Cheats used:
- Velocity-led spawn anchor plus off-screen dot rejection instead of honest pathfinding or ray occlusion.
- Data Monolith biome byte gate instead of authored species list traversal.
- 1Hz ColdTick burst math instead of frame-by-frame behavior simulation.
- Headless native slots and predator shader globals instead of GameObject predator hydration.

Exact Microseconds saved:
- Headless slot write vs `Instantiate`: object-spawn milliseconds avoided; hot director write estimated under 2 us.
- Dot/distance cone rejection before plane tests: estimated below 1 us saved per cold tick on i3/MX350.
- Fixed death-signal drain: under 5 us at 16 drained signals, avoids callback/listener allocation.
- Biome byte masking: estimated 2 us per spawn attempt, zero managed allocation.
- 48-byte blackbox ring write: estimated 3 us/frame with aligned record stride; dump cost is NaN/crash path only.

Verification:
- `validate_script Assets/_Project/Scripts/EncounterDirector.cs`: pass, 0 diagnostics.
- `validate_script Assets/_Project/Scripts/HectonDirectorAI.cs`: validator false positive on `BuildEventOffsetDirectionLut`; `rg` shows one declaration.
- Unity console filtered by `EncounterDirector`: 0 errors.
- Unity console filtered by `HectonDirectorAI`: 0 errors.
- `dotnet build Hecton8.Core.csproj`: blocked by unrelated cross-domain errors in World/Core/Audio contracts.
- Final project status remains PENDING VERIFICATION per batch directive and compile blockers.

## 2026-05-12 Continuation Pass

What was wrong:
- The first-pass state/token structs were functional but not explicitly padded for predictable NativeArray stride.
- Low-health suppression was too blunt: it could either erase all pressure or allow a stored apex budget to survive the near-death window.
- ColdTick math still had avoidable divisions and duplicate reserved-spawn clearance checks.

What was done:
- Added sequential layout and padding to `EncounterDirectorState`, `EncounterEnemyToken`, and existing telemetry structs.
- Added `DespairModeActive` and capped critical-health credits to 20 so only cheap ambient pressure can survive while apex pressure is paused.
- Changed depth/velocity/low-stress divisions to precomputed reciprocal multiplications.
- Reused `HasReservedSpawnClearance` in candidate selection and refunded half-cost when distant despawn requests are successfully emitted.

Cinematic Cheats used:
- Despair Mode is a director fake: it protects near-death pacing by suppressing apex budget rather than simulating predator mercy.
- Distant despawn refund treats off-screen entity removal as pacing credit recovery, not biological truth.

Exact Microseconds saved:
- Reciprocal constants and helper reuse: estimated 1-2 us per cold tick in worst 32-candidate scan.
- Struct stride cleanup: estimated below 1 us, mainly cache predictability and binary dump stability.
- Despair Mode: one flag bit and one budget clamp, below 1 us.

Verification:
- `validate_script Assets/_Project/Scripts/EncounterDirector.cs`: pass, 0 diagnostics.
- Unity console filtered by `EncounterDirector`: 0 errors.
- Unity console filtered by `HectonDirectorAI`: 0 errors.
- Full Unity compile remains blocked by unrelated errors in Voxel, Player Kinematics, Audio, Fluid, World Chunk, and UI files. No compile error references `EncounterDirector.cs` or `HectonDirectorAI.cs`.

## 2026-05-12 Continuation Pass 2

What was wrong:
- The blackbox `FrameIndex` used the 1Hz cold-tick index, so a high-framerate crash trail could contain repeated indices instead of a true 300-frame sequence.
- The Burst spawn seed path converted `float3` through `Vector3` inside the job call.
- Predator AUP integration has competing owners: encounter writes `_PredatorAUPBuffer`, ecosystem also writes `_PredatorAUPBuffer`, and boid panic consumes `_PredatorAupPositions[16]` through `HectonBoidController`.

What was done:
- Added `_blackBoxFrameSequence` and reset it with director state so each blackbox ring write gets a monotonic per-frame sequence.
- Added velocity finite checks to the NaN dump trigger.
- Added a `float3` overload for `BuildDeterministicSeed`; managed `Vector3` callers still use the wrapper, Burst job code hashes `float3` directly.
- Documented the predator AUP ownership conflict and rejected direct boid-controller coupling without an Integrator-owned registry contract.

Cinematic Cheats used:
- The blackbox sequence is a deterministic director-local counter, not a dependency on Unity frame globals.
- Spawn hash still floors world position into coarse grid cells; predictable replay beats biological realism.

Exact Microseconds saved:
- `float3` seed overload: sub-microsecond per spawned request, removes a managed-type conversion from the job path.
- Blackbox sequence: one uint increment per frame, below 1 us on i3/MX350, but stronger crash evidence.
- Rejected boid direct coupling: avoids scene lookup/object traversal entirely; unresolved integration cost must be handled by a registry owner.

Verification:
- `git diff --check -- EncounterDirector.cs HectonDirectorAI.cs`: pass, CRLF warnings only.
- Unity MCP `validate_script` and `read_console`: unavailable now, both return `no_unity_session`.
- `dotnet build Hecton8.Core.csproj --no-restore`: timed out at 120s in this pass; previous run was already blocked by unrelated cross-domain compile errors.
- Final project status remains PENDING VERIFICATION.

## 2026-05-12 Continuation Pass 3

What was wrong:
- Task 6 wording required buying units until credits are depleted. The previous implementation used a three-request same-tier output lane, so apex-first selection could strand remaining credits after one Leviathan or Stalker batch.
- Authoring data could override the prompt's canonical costs.
- `HectonDirectorAI.TryGetPredatorAupGpuBuffer` called through to an encounter-director method that was missing after the first bridge pass.

What was done:
- Added persistent `NativeArray<EncounterSpawnRequest>[16]` and registered it with `NativeMemorySentinel`.
- Reworked the Burst purchase loop to reselect the best affordable tier after every buy and stop only on credits, class caps, active slots, request capacity, or visibility-safe placement failure.
- Enforced canonical costs after authoring table application: Crab/Drone/Swarm 5, Shark/Stalker 50, Leviathan 500.
- Restored `EncounterDirector.TryGetPredatorAupGpuBuffer(out GraphicsBuffer, out int)` for the existing director service bridge.
- Updated recon evidence with the current `Instantiate(` scan result.

Cinematic Cheats used:
- The director still buys data-only pressure, not physical creatures. Hydration is intentionally deferred so the ocean can feel populated without paying GameObject costs.
- Tier reselecting is pacing math, not ecology. The director spends drama credits until the scene budget says stop.

Exact Microseconds saved:
- No GameObject spawn path remains in `EncounterDirector` or `HectonDirectorAI`; this preserves millisecond-scale savings versus `Instantiate`/pool hydration spikes.
- The new 16-slot purchase loop costs an estimated extra 3 us in worst-case 1Hz cold ticks on i3/MX350.
- Canonical cost enforcement is cold authoring math only, effectively 0 us/frame.

Verification:
- `git diff --check -- EncounterDirector.cs HectonDirectorAI.cs`: pass, CRLF warnings only.
- `rg -n "Instantiate\("` in AI/spawner/director scope: no hit in `EncounterDirector.cs` or `HectonDirectorAI.cs`; one project world-scatter hit remains at `WorldProceduralScatterDirector.cs:7756`.
- `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:q /clp:ErrorsOnly`: blocked outside AI domain by `Assets/_Project/Scripts/World/AcousticOcclusionUtility.cs(1111,25)` missing `AcousticSurfaceResponse`.
- Unity MCP editor validation unavailable: `mcpforunity://instances` reports `instance_count: 0`.
- Final project status remains PENDING VERIFICATION.

## 2026-05-12 Continuation Pass 4

What was wrong:
- `EncounterDirector.Advance()` could continue into `_enemyTokens` refresh and `_backState` mutation while the cold job was still scheduled.
- `HectonDirectorAI` drained `EntityDeathSignal` before `Advance()`, so death refunds could touch director state while the job still owned `_backState`.
- Failed headless allocation after a job-requested spawn refunded credits but did not roll back the job's optimistic active-count increment.
- Job-initiated headless despawn double-decremented active count on release, while failed tracked despawn recall removed the optimistic refund but did not restore active count.

What was done:
- Added a pending-job early return in `Advance()`; while the job is active it only records blackbox telemetry and accumulates time.
- Added `CanProcessEntityDeathSignals`; death queue drains are deferred until no cold job is active.
- Replaced spawn-failure refund with `RollbackFailedSpawn()` so active count is restored for both forced and credit-bought failures.
- Split headless release active-count behavior: death-signal releases decrement, job-confirmed despawns do not.
- Failed tracked recall now restores active count and rolls back the optimistic half-cost refund.

Cinematic Cheats used:
- Deferred signal handling preserves pacing truth without forcing immediate simulation synchronization.
- Main-thread application remains optimistic and corrected only on failure; no expensive full reconciliation scan.

Exact Microseconds saved:
- Avoided per-frame job `.Complete()` stall: prevents potentially millisecond-scale main-thread serialization on i3/MX350.
- Deferred death drain gate: one branch, below 1 us.
- Failure rollback: only runs on failed allocation/recall paths, no steady-state hot cost.

Verification:
- `git diff --check -- EncounterDirector.cs HectonDirectorAI.cs`: pass, CRLF warnings only.
- Unity MCP `validate_script`: unavailable, returns `no_unity_session`.
- `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -m:1 /p:UseSharedCompilation=false`: blocked by unrelated `World/AcousticOcclusionUtility.cs(1117,25)` missing `AcousticSurfaceResponse`; no director error surfaced before the blocker.
- Final project status remains PENDING VERIFICATION.

## 2026-05-12 Continuation Pass 9

What was wrong:
- Active terrain biome sampling clamped out-of-rect candidate coordinates to edge texels, so a predictive spawn outside the active payload could inherit a false biome byte.
- Invalid native spawn request slots could fall back to legacy/default request fields if `SpawnRequestCount` was wrong, risking an incorrect default spawn.

What was done:
- `TryResolveBiomeHashFromActiveTerrain()` now rejects non-finite or out-of-rect UVs and falls back to the deterministic wrapped heatmap path.
- `ApplySpawnRequests()` now validates threat class IDs and rolls back the optimistic active-count increment through `RollbackUnappliedSpawn()` instead of spawning fallback data.

Cinematic Cheats used:
- Biome truth remains a byte-gate visual fake, but it no longer lies with clamped terrain-edge data.
- Spawn request failure is fail-closed pacing correction, not a visible repair simulation.

Exact Microseconds saved:
- Terrain bounds guard adds below 1 us on spawn allocation and prevents wrong-species retries near streaming boundaries.
- Spawn request sentinel guard is one integer range check per applied request, below 1 us, zero allocation.

Verification:
- `git diff --check -- EncounterDirector.cs HectonDirectorAI.cs`: pass, CRLF warnings only.
- Forbidden-pattern scan: only `OnDestroy`, cold retry `TryGetComponent`, and justified `NativeDisableParallelForRestriction` matches remain.
- `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:q /clp:ErrorsOnly`: pass, 0 warnings, 0 errors.
- Unity MCP `validate_script`: unavailable, returns `no_unity_session`.
- Final project status remains PENDING VERIFICATION.

## 2026-05-12 Continuation Pass 5

What was wrong:
- The headless pool token refresh still ran on ordinary `Advance()` frames, so the director could scan and age up to 1024 slots outside the 1Hz cold-tick boundary.
- The single `NativeDisableParallelForRestriction` field had no inline invariant comment, which made future scaling changes risky.

What was done:
- Moved `RefreshTrackedEnemies(frameContext.PlayerPosition)` to the exact point before `ScheduleColdTick(frameContext)`.
- Left blackbox recording per-frame, but it now reads completed front-state data without forcing a token scan.
- Added a source-level safety comment above the spawn request buffer explaining single-lane writer ownership and main-thread clear/read phases.

Cinematic Cheats used:
- The director now treats headless entities as pressure data sampled once per second, not as per-frame actors.
- Per-frame blackbox evidence remains cheap and does not try to reconstruct every headless slot every frame.

Exact Microseconds saved:
- Avoids up to one 1024-slot headless scan on every non-cold frame. On i3/MX350 this is estimated as tens of microseconds saved in dense scenes.
- Safety comment has no runtime cost but blocks unsafe future expansion of the request writer.

Verification:
- Forbidden-pattern scan on `EncounterDirector.cs` and `HectonDirectorAI.cs`: no `foreach`, LINQ, `.Complete(`, `Instantiate`, `Destroy`, `FindObject`, coroutine, hot string format, or `.ToString()` hits; remaining hits are `NativeDisableParallelForRestriction`, `OnDestroy`, and cold retry `TryGetComponent`.
- `git diff --check -- EncounterDirector.cs HectonDirectorAI.cs`: pass, CRLF warnings only.
- `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:q /clp:ErrorsOnly`: pass, 0 errors.
- `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:minimal`: pass, 0 warnings in latest incremental run.
- Unity MCP editor validation remains unavailable: `mcpforunity://instances` reports `instance_count: 0`.
- Final project status remains PENDING VERIFICATION.

## 2026-05-12 Continuation Pass 6

What was wrong:
- Biome masking used deterministic wrapped runtime coordinates before falling back to the biome matrix. That preserved zero-GC behavior, but it did not align candidate positions to the active terrain tile's Data Monolith heatmap rect.

What was done:
- Added active terrain payload sampling through `HectonMapMagicVegetationBridge.TryGetActiveHeightTexturePayload`.
- Mapped candidate XZ into the same 256x256 heatmap rect used by the GPU scatter path.
- Kept deterministic wrapped-coordinate and biome-matrix fallbacks when the active terrain payload is unavailable.

Cinematic Cheats used:
- The director still uses a folded biome byte, not expensive authored species queries.
- The heatmap sample is a cheap mask for believable pressure placement, not a full ecology simulation.

Exact Microseconds saved:
- Avoids managed species-table lookups and allocation entirely.
- Adds an estimated under 2 us per spawned request on i3/MX350 while improving terrain-biome correctness.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:q /clp:ErrorsOnly`: passed immediately after the biome hardening, 0 errors.
- `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:minimal`: latest incremental pass after biome hardening reported 0 warnings.
- Unity MCP editor validation unavailable: `mcpforunity://instances` reports `instance_count: 0`.
- Final project status remains PENDING VERIFICATION.

## 2026-05-12 Continuation Pass 7

What was wrong:
- Despawn garbage collection still had a legacy three-ID output lane, while spawn purchases already had a 16-slot native request lane.
- If that legacy lane saturated, a far entity could stop contributing to active-count accounting despite not being queued for release.

What was done:
- Added persistent `NativeArray<int>[16]` for despawn requests and registered it with `NativeMemorySentinel`.
- Passed the despawn lane into the Burst director job and cleared it before scheduling.
- Main-thread despawn application now consumes the native lane, with the old three fields retained as fallback.
- Added three-paragraph safety comments for both spawn and despawn request buffers.
- Changed far-entity accounting so saturated despawn requests remain counted active until successfully queued later.

Cinematic Cheats used:
- Cleanup is still data-only: far entities are returned to the headless pool, not destroyed.
- Conservative overflow accounting prevents the director from buying extra drama against entities that still exist.

Exact Microseconds saved:
- Reclaims up to 16 far headless entities in one cold tick instead of leaking cleanup across multiple seconds.
- Saves repeated future 1024-slot token scans for already-eligible far entities; estimated dense-scene gain is tens of microseconds on i3/MX350.
- The new duplicate check scans at most 16 ints once per 1Hz cold tick; estimated under 2 us worst case.

Verification:
- `git diff --check -- EncounterDirector.cs HectonDirectorAI.cs`: pass, CRLF warnings only.
- Forbidden-pattern scan on touched AI files: no `foreach`, LINQ, `.Complete(`, `Instantiate`, `FindObject`, `GameObject.Find`, coroutine, hot string format, or `.ToString()` hits. The only scan hit is `HectonDirectorAI.OnDestroy`, a false positive for the raw `Destroy` pattern.
- `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:q /clp:ErrorsOnly`: blocked by external untracked `Assets/_Project/Scripts/Gameplay/DataArchaeologyRuntime.cs(269,109)` missing `IOriginShiftListener.OnOriginShift(in OriginShiftEventData)`. No touched encounter-director file appears in compiler error output.
- Unity MCP editor validation unavailable: `mcpforunity://instances` reports `instance_count: 0`.
- Final project status remains PENDING VERIFICATION.

## 2026-05-12 Continuation Pass 8

What was wrong:
- Predator AUP publication used one graphics buffer. The project bandwidth rule requires double-buffered GPU data so the CPU does not write the same buffer the GPU may still be reading.

What was done:
- Replaced the single predator AUP graphics buffer with A/B `GraphicsBuffer` instances.
- `PublishPredatorAupBuffer()` now uploads into the current write buffer, publishes that buffer globally, and alternates the write side only when there is a real predator upload.
- `TryGetPredatorAupGpuBuffer()` now exposes the last published active buffer through the existing `GlobalRegistry` service bridge.
- `ReleasePredatorAupBuffer()` releases both buffers and clears the published handle.

Cinematic Cheats used:
- Predator AUP remains a compact 16-node fear/avoidance proxy, not full creature simulation.
- The saved sync risk is spent on more stable PDA/boid predator cues rather than higher-fidelity physical truth.

Exact Microseconds saved:
- Adds one extra 16-float4 graphics buffer, about 256 bytes plus driver overhead.
- Removes a possible CPU/GPU upload contention point on MX350; no per-frame upload was added.
- Upload cadence remains dirty-event only on spawn/despawn/death/reset paths.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: pass, 0 warnings, 0 errors.
- `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: pass, 0 warnings, 0 errors.
- `git diff --check -- EncounterDirector.cs HectonDirectorAI.cs`: pass, CRLF warnings only.
- Forbidden-pattern scan on touched AI files: no `foreach`, LINQ, `.Complete(`, `Instantiate`, `FindObject`, `GameObject.Find`, coroutine, hot string format, or `.ToString()` hits. Raw `Destroy` scan still hits `OnDestroy` method name only.
- Unity MCP editor validation unavailable: `mcpforunity://instances` reports `instance_count: 0`.
- Final project status remains PENDING VERIFICATION.

## 2026-05-12 Continuation Pass 11

What was wrong:
- Cold-job result handling and service teardown needed another audit against dispatcher phase discipline.
- Predator AUP publication could rescan the 1024-slot headless pool multiple times during one spawn/despawn output application.
- First build verification was blocked by transient unrelated `HectonPlayerMovement.cs` errors; a later rerun passed cleanly.

What was done:
- Verified encounter cold-job output application is now driven from `LateFrameTick()` through `CompleteReadyOutput()` instead of normal Tick result application.
- Kept disable-time `ForceStopAndReset()` and destroy-time registry cleanup so re-enable/destroy windows do not touch native state while a cold job is still live.
- Batched predator AUP publication through one dirty flag across despawn and spawn output application; headless releases report whether a predator actually changed before uploading.
- Re-read `CURRENT_BATCH.md`, status, rationale, AGENTS, domain map, and the relevant AI/Zero-GC/Native Jobs/Telemetry/AUP/Rsqrt/Registry mandates.

Cinematic Cheats used:
- Predator pressure remains a 16-slot AUP fear proxy, not direct boid controller mutation or full creature simulation.
- Output application is a deterministic late-frame handoff; no physics occlusion or visible-object hydration was added.

Exact Microseconds saved:
- Avoids up to 15 redundant 1024-slot predator scans in a saturated despawn tick; estimated tens of microseconds on i3/MX350 dense cleanup.
- Drone-only despawns no longer publish predator AUP data.
- No allocation added.

Verification:
- `git diff --check -- EncounterDirector.cs HectonDirectorAI.cs`: pass, CRLF warnings only.
- Forbidden-pattern scan on touched AI files: no `foreach`, LINQ, `.Complete(`, `Instantiate`, `FindObject`, `GameObject.Find`, coroutine, hot string format, or `.ToString()` hits. Raw `Destroy` scan still hits `OnDestroy` method name only.
- `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: first rerun was blocked by external `HectonPlayerMovement.cs` errors; later rerun passed, 0 warnings, 0 errors.
- `validate_script Assets/_Project/Scripts/EncounterDirector.cs`: pass, 0 diagnostics.
- `validate_script Assets/_Project/Scripts/HectonDirectorAI.cs`: validator heuristic false positive for duplicate `BuildEventOffsetDirectionLut`; `rg` shows one call and one declaration.
- Unity console filtered `EncounterDirector`: 0 errors.
- Unity console filtered `HectonDirectorAI`: 0 errors.
- Unity console global still shows unrelated/stale `Assets/_Project/Scripts/UI/PDAMapTab.cs` shader property ID errors outside this domain.
- Final project status remains PENDING VERIFICATION.

## 2026-05-12 Continuation Pass 9

What was wrong:
- Active-terrain biome sampling clamped out-of-rect candidate coordinates to the nearest heatmap edge, which could silently spawn the wrong species near terrain-streaming boundaries.
- The native spawn request path had rollback documentation, but request-slot validation needed another audit pass.

What was done:
- Changed active terrain biome sampling to reject non-finite or out-of-rect UVs and use the deterministic wrapped fallback.
- Added invalid request-slot rollback coverage and verified the project after the terrain-boundary hardening.

Cinematic Cheats used:
- Out-of-tile biome positions use deterministic byte fallback, not a cross-tile ecology query.
- Spawn failure remains silent and data-only; the director spends credits only when it can place believable off-screen pressure.

Exact Microseconds saved:
- Avoids incorrect edge-biome work without adding allocations.
- Adds only scalar finite/bounds checks on spawn allocation; estimated below 1 us on i3/MX350.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:q /clp:ErrorsOnly`: pass, 0 warnings, 0 errors.
- Unity MCP editor validation unavailable: `mcpforunity://instances` reports `instance_count: 0`.
- Final project status remains PENDING VERIFICATION.

## 2026-05-12 Continuation Pass 10

What was wrong:
- `GetSpawnRequest()` still fell back to legacy/default fields when the persistent native request lane existed but the requested slot was invalid. That contradicted the fail-closed rule and could spawn a wrong default threat after buffer corruption or a bad count.
- Spawn candidate normalization still used a divide inside the 16/32-candidate cold loop.

What was done:
- Replaced `GetSpawnRequest()` with `TryGetSpawnRequest()`; native slots are now authoritative, invalid slots roll back optimistic active count, and no fallback spawn occurs.
- Converted the legacy fallback position read to `float3` to avoid unnecessary `Vector3` construction on that path.
- Hoisted candidate index normalization to one `math.rcp` per cold tick and multiplied inside the candidate loop.

Cinematic Cheats used:
- Invalid spawn data fails silently and keeps pacing accounting stable instead of trying to hydrate a fallback creature.
- Candidate scoring remains a cheap directional lie: velocity lead, visibility rejection, and clearance checks, not physics occlusion.

Exact Microseconds saved:
- Prevents wrong fallback spawns and avoids follow-on cleanup cost; correctness is the main gain.
- Saves up to 32 scalar divides per cold tick; estimated below 1 us on i3/MX350.
- No allocation added.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: pass, 0 warnings, 0 errors.
- `git diff --check -- EncounterDirector.cs HectonDirectorAI.cs`: pass, CRLF warnings only.
- Forbidden-pattern scan on touched AI files: no `foreach`, LINQ, `.Complete(`, `Instantiate`, `FindObject`, `GameObject.Find`, coroutine, hot string format, or `.ToString()` hits. Raw `Destroy` scan still hits `OnDestroy` method name only.
- Unity MCP editor validation unavailable: `mcpforunity://instances` reports `instance_count: 0`.
- Final project status remains PENDING VERIFICATION.

## 2026-05-12 Continuation Pass 11

What was wrong:
- Encounter cold-job output application was still finalized from the dispatcher `Tick()` path once the handle had completed. It did not block, but it kept job result ownership in the frame solve path instead of the dispatcher swap window.
- Disable/destroy paths needed a harder guarantee that predator AUP globals and dispatcher registrations cannot outlive the encounter service.

What was done:
- `EncounterDirector.Advance()` now records blackbox state and accumulates cold time while a cold job is scheduled; it no longer applies completed output from `Tick()`.
- Added `EncounterDirector.CompleteReadyOutput()` and call it from `HectonDirectorAI.LateFrameTick()` so non-forced output application runs in the late-frame swap lane.
- Added `ForceStopAndReset()` on disable to force-complete teardown, clear headless state, and publish a zero predator AUP count.
- Added destroy-time `GlobalRegistry.UnregisterUpdatable` fallback.

Cinematic Cheats used:
- No visual work was made more honest or more expensive; the director still uses data-only headless predators and compact AUP fear points.
- The patch preserves cinematic pressure timing while making result publication phase-correct.

Exact Microseconds saved:
- Avoids a possible dev-build dispatcher warning and keeps completed job finalization out of the normal solve path.
- Adds one branch in `LateFrameTick`; estimated below 1 us on i3/MX350.
- Disable reset scans fixed 1024 headless slots only outside active gameplay.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: pass, 0 warnings, 0 errors.
- `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: pass, 0 warnings, 0 errors.
- `git diff --check -- EncounterDirector.cs HectonDirectorAI.cs`: pass, CRLF warnings only.
- Static scan: no `TryFinalizeCompleted` or non-forced `_activeJobHandle` completion remains in `Tick()` path.
- Forbidden-pattern scan on touched AI files: no `foreach`, LINQ, `.Complete(`, `Instantiate`, `FindObject`, `GameObject.Find`, coroutine, hot string format, or `.ToString()` hits. Raw `Destroy` scan still hits `OnDestroy` method name only.
- Unity MCP editor validation unavailable: `mcpforunity://instances` reports `instance_count: 0`.
- Final project status remains PENDING VERIFICATION.

## 2026-05-12 Continuation Pass 12

What was wrong:
- Predator AUP publication still treated non-predator churn as buffer work in some paths. A drone-only spawn/despawn sequence could still trigger an unnecessary 1024-slot scan and GPU global publication.
- Prior evidence still reported Unity MCP as unavailable even though the editor connection was restored.

What was done:
- Tightened `ApplyCompletedOutput()` to carry a single `predatorAupDirty` flag through spawn and despawn application.
- `TryReleaseHeadlessEntity()` now reports whether the released slot was a predator; non-predator releases do not publish the predator AUP buffer.
- Spawn application marks AUP dirty only for Stalker, Swarm, and Leviathan requests.
- Updated status and rationale with the restored Unity MCP validation result and the external `PDAMapTab.cs` compile blocker.

Cinematic Cheats used:
- Predator fear stays a 16-slot AUP proxy instead of honest creature simulation.
- Drone/Crab ambient pressure remains invisible to the predator AUP lane, so cheap ambient pacing does not wake GPU fear consumers.

Exact Microseconds saved:
- Avoids up to 15 redundant 1024-slot scans in a saturated despawn tick.
- Avoids drone-only spawn/despawn GPU buffer uploads and shader global updates; estimated tens of microseconds on i3/MX350 dense cleanup.
- No allocation added.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: pass, 0 warnings, 0 errors.
- `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: pass, 0 warnings, 0 errors.
- `git diff --check -- Assets/_Project/Scripts/EncounterDirector.cs Assets/_Project/Scripts/HectonDirectorAI.cs`: pass, CRLF warnings only.
- Unity MCP `validate_script Assets/_Project/Scripts/EncounterDirector.cs`: pass, 0 diagnostics.
- Unity MCP `validate_script Assets/_Project/Scripts/HectonDirectorAI.cs`: blocked only by known duplicate-method heuristic; `rg` shows one declaration and one call.
- Unity console filters after reconnect: 0 `EncounterDirector` errors, 0 `HectonDirectorAI` errors.
- Unity global console after reconnect: 0 errors. Earlier modified `Assets/_Project/Scripts/UI/PDAMapTab.cs` shader property ID errors were external/stale and are no longer present.

## 2026-05-12 Continuation Pass 13

What was wrong:
- Native disposal switched to deferred disposal after the first native array even when there was no active job dependency. That creates avoidable teardown jobs and makes domain-reload evidence harder to read.
- Forced threat requests only rejected negative ids. Out-of-range positive ids could reach the Burst purchase lane and fail later on the main thread after budget bookkeeping.
- Predator sight raycasts use obstruction layers, but completion treated `hit.collider != null` as line of sight. That inverted the probe and could turn terrain/base hits into false aggro.

What was done:
- Kept native array/list disposal synchronous unless `hasDependency` is already true from an active job.
- Added upper-bound forced-threat validation against `EncounterThreatClass.Leviathan`.
- Flipped predator LOS completion so a clear obstruction ray means `hasLineOfSight = true`, while an obstruction hit means blocked.
- Cleared orphan MSBuild nodes from a timed-out verification run, restored missing project assets, and reran command-line build gates.

Cinematic Cheats used:
- Predator sight remains a one-ray obstruction probe on a 0.5s cadence, not an honest sensory simulation.
- Forced squads still use data-only headless requests; invalid authoring fails closed before creature hydration.

Exact Microseconds saved:
- No-dependency teardown avoids unnecessary disposal job scheduling; cost is teardown-only but improves domain-reload determinism.
- Invalid forced ids exit before request writes and budget mutation; sub-1 us on cold tick.
- Correct LOS prevents false hunt-state churn from obstruction hits; behavioral stability gain is larger than raw CPU savings.

Verification:
- `git diff --check -- Assets/_Project/Scripts/EncounterDirector.cs Assets/_Project/Scripts/HectonDirectorAI.cs`: pass, CRLF warnings only.
- Forbidden-pattern scan on touched AI files: no forbidden hot-path hits.
- `dotnet restore Hecton8.Core.csproj --nologo -v:q`: pass.
- `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: pass, 46 warnings, 0 errors.
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: pass after dependency build, 2 warnings, 0 errors.
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies --nologo -v:minimal /clp:WarningsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: pass, 0 warnings, 0 errors.
- Unity MCP latest retry unavailable: `mcpforunity://instances` reports `instance_count: 0`.
- Final project status remains PENDING VERIFICATION.

## 2026-05-12 Continuation Pass 15

What was wrong:
- Headless pool allocation still began every free-slot search at slot 0, causing repeated occupied-prefix scans during 16-request spawn batches.
- Predator AUP publication only copied headless predators even though the file still has a tracked-predator fallback lane. If that lane is re-enabled by integration, predator AUP consumers would miss tracked Stalker/Swarm/Leviathan positions or keep stale tracked entries after death/recall.
- A few cold math helpers still used scalar division where reciprocal forms were already mandated.

What was done:
- Added `_headlessFreeSearchCursor`, wraparound free-slot probing, reset on pool clear, and release-time cursor rewind.
- Appended tracked predator entries to the existing 16-slot A/B predator AUP upload after headless predators, and marked publication dirty on tracked predator registration, death, inactive cleanup, and recall.
- Replaced 24-bit hash normalization division with a reciprocal constant and replaced both Pade exponential helper divisions with `math.rcp`.

Cinematic Cheats used:
- Predator pressure remains a cheap AUP fear proxy, not honest full-simulation coupling.
- Headless predators stay prioritized in the 16-slot buffer; tracked/hydrated fallback predators are only appended when dirty events require it.

Exact Microseconds saved:
- Avoids up to roughly 15 repeated occupied-prefix scans in a full 16-request cold tick.
- Adds at most 32 tracked Transform checks only on dirty/cold cleanup, not per frame.
- Reciprocal math savings are sub-microsecond per cold evaluation; no allocation added.

Verification:
- `git diff --check -- Assets/_Project/Scripts/EncounterDirector.cs Assets/_Project/Scripts/HectonDirectorAI.cs`: pass, no whitespace errors; CRLF warnings only.
- Forbidden-pattern scan on touched AI files: no `foreach`, LINQ, `.Complete(`, `Instantiate`, `FindObject`, `GameObject.Find`, coroutine, hot string format, or `.ToString()` hits. Raw `Destroy` scan still hits `OnDestroy`; known `NativeDisableParallelForRestriction` and cold retry `TryGetComponent` hits remain.
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: pass, 0 warnings, 0 errors.
- `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: pass, 0 warnings, 0 errors.
- Unity MCP validation blocked: HTTP transport send failure to `http://127.0.0.1:8088/mcp`.
- Final project status remains PENDING VERIFICATION.

## 2026-05-12 Continuation Pass 16

What was wrong:
- `HectonDirectorAI.OnEnable()` still had a bootstrap-order risk: if `GlobalRegistry.Dispatcher` was absent, older flow returned before encounter service publication and runtime GPU/director initialization.
- The source diff already carried the lifecycle hardening, but the evidence files did not explicitly explain the decision or latest command-line verification.

What was done:
- Audited `GlobalRegistry.TryRegisterUpdatable`, `TryRegisterLateFrameTickable`, and `TryEnsureDispatcherRegistration`; missing dispatcher is a Core bootstrap error, not a local AI object to create.
- Kept encounter service publication independent from dispatcher lane registration.
- Retained a single `Start()` retry for dispatcher lanes so normal bootstrap ordering gets a second chance without any per-frame registration polling.
- Re-ran the full and no-dependency command-line build gates after the current source diff.

Cinematic Cheats used:
- No new simulation honesty was added. The director still buys pressure through headless tokens, fixed AUP predator fear points, and off-screen spawn selection.
- Startup resilience protects the cinematic system from silently going inert; it does not add frame-cost spectacle.

Exact Microseconds saved:
- Rejected per-frame registration polling: saves one recurring branch/registry probe every frame.
- Startup-only retry cost is below 1 us on i3/MX350 in normal play and 0 B/frame.
- Latest full/no-deps builds are 0 warnings, 0 errors.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: pass, 0 warnings, 0 errors.
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies --nologo -v:q /clp:ErrorsOnly /m:1 /nr:false /p:UseSharedCompilation=false`: pass, 0 warnings, 0 errors.
- `git diff --check -- Assets/_Project/Scripts/EncounterDirector.cs Assets/_Project/Scripts/HectonDirectorAI.cs Docs/Tasks/Status_AI_ENCOUNTER_DIRECTOR.md Docs/AgentLogs/Rationale_AI_ENCOUNTER_DIRECTOR.md Docs/AgentLogs/LOG_AI_ENCOUNTER_DIRECTOR.md`: pass, CRLF warnings only.
- Forbidden-pattern scan on touched AI files: no forbidden hot-path hits.
- Unity MCP validation blocked: HTTP transport send failure to `http://127.0.0.1:8088/mcp`.
- Final project status remains PENDING VERIFICATION.
