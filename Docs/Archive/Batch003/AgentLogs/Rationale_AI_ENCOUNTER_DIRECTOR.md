# Rationale_AI_ENCOUNTER_DIRECTOR

Problem: Predictive encounter director must add L4D-style pacing without GameObject spawning or hot-path allocation.
Solution: Extend existing encounter/headless AI surfaces after contract inspection; use fixed-capacity native data, AUP-like sector/local records where available, and deterministic math.
Rejected Alternatives: New standalone manager with scene references is rejected because parallel agents own adjacent systems and AGENTS.md requires GlobalRegistry/EventBus decoupling.
Scalability potential: Low uses 1Hz cold evaluation and coarse dot/frustum rejection. Middle increases active budget. High and Ultra spend saved CPU on denser predator cue buffers and richer biome-specific threat mixes.
Hardware Impact: Expected low-end i3/MX350 gain is from replacing runtime Instantiate/Destroy with NativeList slot writes; exact profiler proof absent, status remains PENDING VERIFICATION.

Problem: Constructor-time predator GPU buffer creation would move a graphics resource into object field initialization/reset territory.
Solution: Keep native arrays persistent in the director constructor, but create the `GraphicsBuffer` only from `HectonDirectorAI.OnEnable()` and publish globals after that cold path.
Rejected Alternatives: Creating the buffer from `PublishPredatorAupBuffer()` was simpler but allowed `Reset()` to allocate graphics state before the runtime service was actually enabled.
Scalability potential: Low has a 16-slot fixed upload with zero dynamic resize. Middle/High/Ultra can increase shader use of the same global without changing director ownership.
Hardware Impact: i3/MX350 avoids surprise graphics allocation during reset/domain churn; no measured microsecond proof because project compile is blocked by external agents.

Problem: The encounter loop needed death refunds without coupling to FaunaDirector object lifetimes.
Solution: Drain `GlobalSignals.TryDequeueEntityDeath` with a fixed 16-signal budget per director tick and refund matching headless entity slots by entity hash.
Rejected Alternatives: Direct callbacks from fauna brains were rejected because 20+ agents are editing adjacent systems and GlobalSignals is the registered decoupling surface.
Scalability potential: Low drains a bounded lane. Middle/High/Ultra can increase drain budget if combat density increases without changing the NativeQueue contract.
Hardware Impact: Expected low-end gain is from avoiding listener allocation and object graph traversal; estimated under 5 microseconds at 16 drained signals.

Problem: Predictive spawning ahead of the player can become unfair if it appears in the visible cone.
Solution: Use velocity-led anchor 200m ahead, reject behind-velocity candidates, then reject likely visible candidates by dot-product before the existing plane test.
Rejected Alternatives: Full occlusion probing or physics queries were rejected as too expensive and outside the "dear lie" mandate.
Scalability potential: Low uses 16 precomputed candidate directions. High/Ultra uses 32 with the same Burst math and predator buffer upload.
Hardware Impact: Dot rejection is estimated below 1 microsecond on i3/MX350; saved cycles buy more cinematic off-screen pressure instead of visible pop-in.

Problem: Biome masking had to avoid managed collections while still querying the Data Monolith heatmap.
Solution: Hash one 2D heatmap cell into a biome byte, fall back to the current biome matrix index, and gate class-specific threat rules with byte/depth checks.
Rejected Alternatives: ScriptableObject species lists or LINQ filters were rejected because the spawn path is cold but still allocation-sensitive.
Scalability potential: Low uses byte gates. Middle/High/Ultra can map the same byte to richer authored species later without changing the director data lane.
Hardware Impact: Estimated under 2 microseconds per spawn candidate on i3/MX350; no hot allocation.

Problem: Crash evidence needed the director's last 300 high-level states.
Solution: Add a fixed `NativeArray<EncounterDirectorBlackBoxEntry>` ring with `DirectorStateHash`, `ActiveThreatCount`, stress, intensity, credits, speed, and position. Dump only on non-finite state.
Rejected Alternatives: Managed logs every tick were rejected because they allocate and destroy the evidence trail during spikes.
Scalability potential: Low records compact binary state. High/Ultra can add richer debug decode tooling without touching runtime frame cost.
Hardware Impact: Ring write is estimated below 3 microseconds on i3/MX350; dump cost is crash/NaN path only.

## OMEGA POLISH CHANGES

Problem: Blackbox entries were 44 bytes, which is legal but poor for aligned fixed-ring telemetry.
Solution: Added explicit padding and changed dump entry size to 48 bytes so each record lands on a 16-byte multiple while preserving `DirectorStateHash` and `ActiveThreatCount`.
Rejected Alternatives: Leaving a 44-byte record was rejected because the Polish mandate requires cache-aware layout, and binary dumps can carry a versioned entry-size header.
Scalability potential: Low keeps compact 48-byte evidence. Middle/High/Ultra can decode the same ring and add editor tooling without runtime allocation.
Hardware Impact: Expected low-end gain is small but deterministic: less misaligned ring stride pressure, estimated below 1 microsecond over 300-frame writes.

Problem: Candidate rejection recomputed temporary frustum extents and candidate distance during the same cold-tick loop.
Solution: Hoisted `frustumRejectExtents` to locals and reused `candidateDistSq` for scoring after the visible-cone dot rejection.
Rejected Alternatives: Full geometry occlusion or ray probes were rejected; cinematic off-screen pressure is the objective, not honest predator placement.
Scalability potential: Low uses 16 directions and the hoisted scalar path. High/Ultra uses 32 directions without adding allocations.
Hardware Impact: Estimated sub-microsecond savings per cold tick on i3/MX350; the value is predictability, not headline speed.

Problem: Omega build health required `dotnet build Hecton8.Core.csproj`.
Solution: Ran the command. It failed on pre-existing cross-domain errors: duplicate `IChunkVoxelBakeReadiness`, ambiguous `AudioEvent`, missing `MovementAcousticSignal`, and audio service interface mismatches. No errors reference `EncounterDirector.cs` or `HectonDirectorAI.cs`.
Rejected Alternatives: Fixing unrelated Core/Audio/World compile breaks was rejected as domain leakage beyond AI Encounter Director.
Scalability potential: Build unblock belongs to Integrator/Core/Audio agents; this director remains isolated through GlobalSignals and GlobalRegistry.
Hardware Impact: None measured because project compilation is blocked outside this domain.

Problem: A near-death player could carry a full apex-sized token budget across the critical-health window, causing a potential Leviathan/Shark spike immediately after recovery.
Solution: Added `DespairModeActive` and capped credits to the Swarm/Crab budget range while critical health is active. This pauses apex spawning without making the ocean fully inert.
Rejected Alternatives: Zeroing all credits was too blunt and erased low-tier ambient pressure. A separate timer field was rejected to avoid another state machine before profiler evidence demands it.
Scalability potential: Low keeps only cheap ambient pressure. Middle/High/Ultra can spend visual budget on non-lethal predator silhouettes, sonar ghosts, and audio pressure without spawning apex combat.
Hardware Impact: One flag bit and one `math.min`; estimated below 1 microsecond per cold tick on i3/MX350.

Problem: `EncounterDirectorState` and `EncounterEnemyToken` had uneven strides after the first pass.
Solution: Applied sequential struct layout and padding so native rings/tokens have predictable 16-byte-aligned stride.
Rejected Alternatives: Leaving CLR default layout was rejected because this data is copied through NativeArray/Job boundaries and blackbox evidence must stay stable.
Scalability potential: Low benefits from predictable cache stride. High/Ultra can add more data lanes later without hidden layout ambiguity.
Hardware Impact: Estimated sub-microsecond, but reduces cache and dump decoding risk.

Problem: ColdTick math still had avoidable divides and duplicated reserved-spawn clearance logic.
Solution: Added reciprocal constants for depth, velocity, safe-idle, and low-stress thresholds; reused the reserved-clearance helper for candidate rejection.
Rejected Alternatives: More accurate physical distance modeling was rejected. The director needs controllable cinematic pressure, not honest ocean ecology.
Scalability potential: Low remains cheap and deterministic. High/Ultra gets more candidate directions with the same scalar path.
Hardware Impact: Estimated 1-2 microseconds saved per cold tick on i3/MX350 in worst candidate scans.

Problem: The blackbox ring used the cold-tick index, so a 60 FPS runtime could write many entries with the same `FrameIndex` and weaken the "last 300 frames" evidence trail.
Solution: Added an independent `_blackBoxFrameSequence` that increments every ring write and expanded NaN detection to include `PlayerVelocity`.
Rejected Alternatives: `Time.frameCount` was rejected because the director already owns a deterministic runtime sequence and should not pull another Unity global into the telemetry lane.
Scalability potential: Low gets cheap crash evidence. Middle/High/Ultra can decode the sequence into editor tooling without changing runtime record size.
Hardware Impact: One uint increment and one `float4` finite check; estimated below 1 microsecond per frame on i3/MX350.

Problem: The Burst cold-tick spawn seed path converted `float3` player position through a `Vector3` constructor before hashing.
Solution: Added a `float3` overload for `BuildDeterministicSeed` and kept the `Vector3` wrapper for managed callers.
Rejected Alternatives: Duplicating seed code inside the job was rejected because seed drift would create hard-to-replay spawn placement.
Scalability potential: Low keeps deterministic replay cheap. High/Ultra can increase candidate counts without adding managed-type conversions inside the job.
Hardware Impact: Removes one struct conversion from each spawned request; estimated sub-microsecond, mainly Burst clarity and replay hygiene.

Problem: Predator AUP outputs have two existing consumers/owners: `EcosystemDirector` owns `_PredatorAUPBuffer`, while `HectonBoidController` feeds `BoidSimulation.compute` through a private `_PredatorAupPositions[16]` vector array.
Solution: Kept the encounter director's required 16-slot shader buffer publication, but refused direct mutation of `HectonBoidController` because no GlobalRegistry service contract exists for that lane.
Rejected Alternatives: Direct controller reference lookup or ad-hoc static setter was rejected as cross-domain coupling under the simultaneous-agent protocol. A new registry interface was also rejected in this pass because it changes a public Core contract outside the prompt's ownership.
Scalability potential: Low preserves isolation. Middle/High/Ultra should merge predator AUP ownership through an Integrator-owned registry contract so flora, PDA, and boid panic all consume one source.
Hardware Impact: Avoids per-frame scene search or direct object graph traversal on low-end hardware; unresolved integration means boid panic may still need a separate owner-fed bridge.

Problem: `Advance()` could refresh `_enemyTokens` and mutate `_backState` while the cold job was still scheduled, because the method only completed finished jobs and then continued through main-thread refresh code.
Solution: Added an early pending-job branch that records blackbox state and accumulates time, then returns without touching job-owned buffers until `DispatcherJobSwap.TryComplete` succeeds.
Rejected Alternatives: Forcing `.Complete()` every frame was rejected because it violates the Native Memory mandate and would serialize the job pipeline. Duplicating the enemy token buffer per in-flight job was rejected as unnecessary for a 1Hz cold tick.
Scalability potential: Low avoids race bugs without a stall. Middle/High/Ultra can raise cold-tick frequency later only if they also preserve this ownership window.
Hardware Impact: Avoids main-thread stalls and undefined native memory access; expected gain is correctness, with no extra per-frame allocation and below 1 microsecond branch overhead.

Problem: `EntityDeathSignal` drains could write director state while the same director job was still using `_backState`.
Solution: Exposed `CanProcessEntityDeathSignals` and made `HectonDirectorAI` leave the global death queue untouched while the cold job is active; signals drain on the next safe tick.
Rejected Alternatives: Dequeueing into a managed pending list was rejected for zero-GC reasons. Completing the job inside the drain was rejected as a frame-time stall.
Scalability potential: Low defers at most a few frames. Middle/High/Ultra can increase queue capacity or drain budget through the existing `GlobalSignals` contract.
Hardware Impact: Prevents data races without allocation; one branch before the bounded drain loop.

Problem: Failed main-thread spawn allocation and failed tracked despawn recall did not fully roll back the job's optimistic `ActiveEnemyCount` changes.
Solution: Added `RollbackFailedSpawn()` and split headless release active-count behavior so job-initiated despawns do not double-decrement while death-signal releases still decrement. Failed tracked recall now restores active count and removes the optimistic refund.
Rejected Alternatives: Recomputing active count by scanning all 1024 headless slots every frame was rejected as wasteful. Trusting job optimism was rejected because biome/pool/retry failures are valid runtime outcomes.
Scalability potential: Low keeps active-count authority stable. Middle/High/Ultra can expand request counts without drifting pacing budgets.
Hardware Impact: Fixes pacing-accounting correctness with one state write on failure paths only; no hot allocation.

Problem: Task 6 required buying units until credits are depleted, but a three-request same-tier output could leave credits stranded after one apex or shark batch.
Solution: Added a persistent 16-slot `NativeArray<EncounterSpawnRequest>`, reselects the best affordable tier after every purchase, and enforces Crab/Drone/Swarm 5, Shark/Stalker 50, Leviathan 500 after authoring data.
Rejected Alternatives: Sixteen manual fields in `EncounterJobOutput` were rejected as brittle. A managed request list was rejected for GC. Direct `NativeList` mutation inside the parallel-for job was rejected to avoid safety-handle risk.
Scalability potential: Low stays on a fixed 1Hz cold lane with 16 native request slots. Middle/High/Ultra can raise active caps later without changing the purchase contract.
Hardware Impact: Worst-case purchase loop adds an estimated 3 microseconds over the old three-slot path on i3/MX350, while preserving the millisecond-scale saving from no GameObject hydration.

Problem: `RefreshTrackedEnemies()` was still called by normal `Advance()` frames, so a quiet frame could scan and age the 1024-slot headless pool before the 1Hz cold job needed tokens.
Solution: Moved token refresh to the cold-tick schedule boundary. Blackbox still records every frame from the completed front state, while the expensive token snapshot only happens when scheduling a new director job.
Rejected Alternatives: Keeping per-frame refresh was rejected because the director mandate is ColdTick 1Hz. Splitting headless tokens into another live SoA was rejected until profiler proof requires it.
Scalability potential: Low reduces worst-case idle work immediately. Middle/High/Ultra can spend the saved frame time on denser cinematic cues rather than polling inactive slots.
Hardware Impact: Saves up to one 1024-slot scan per non-cold frame on i3/MX350; estimated tens of microseconds in dense scenes, with no behavior loss for the 1Hz director job.

Problem: The spawn request native buffer uses `NativeDisableParallelForRestriction` to write a compact request window from a single scheduled job lane.
Solution: Added source-level safety comments naming the invariant: `EncounterDirectorJob` is scheduled with one Execute lane, the main thread clears before scheduling and reads only after completion, and the job owns request indices `[0,16)` while active.
Rejected Alternatives: Removing the attribute breaks compact request writes in `IJobParallelFor`. Changing the job type was rejected during this pass because the existing director code and mandate are already on the single-index `IJobParallelFor` pattern.
Scalability potential: The invariant remains valid as long as the schedule count is one. Any future multi-lane director must move requests to per-lane partitions or a fixed queue.
Hardware Impact: Documentation cost only; it prevents a future unsafe scaling change from silently corrupting request memory.

Problem: Biome masking used a coarse wrapped runtime-coordinate heatmap sample before falling back to the biome matrix. That was deterministic, but not anchored to the active terrain tile's heatmap rect.
Solution: Added a terrain-payload path that reads `HectonMapMagicVegetationBridge.TryGetActiveHeightTexturePayload`, maps candidate AUP/runtime XZ into the same 256x256 Data Monolith heatmap rect used by `GPUScatterDirector`, then folds the biome hash into the existing byte gate.
Rejected Alternatives: Pulling species ScriptableObjects or asking MapMagic directly was rejected because the spawn path must remain allocation-free and decoupled. Full cross-tile candidate lookup was rejected until the world-streaming owner exposes a stable zero-GC query contract.
Scalability potential: Low uses a single heatmap sample per allocated headless entity. Middle/High/Ultra can map the same biome byte to richer authored fauna tables later.
Hardware Impact: Adds a few scalar operations and one read-only bridge payload query on spawn allocation only; estimated under 2 microseconds per spawned request on i3/MX350.

Problem: Despawn garbage collection still used the legacy three-ID output lane while spawning had already moved to a 16-slot native request buffer.
Solution: Added a persistent `NativeArray<int>` despawn lane with the same single-lane job ownership invariant as the spawn request buffer. The main thread clears before scheduling and applies up to 16 despawns after completion.
Rejected Alternatives: Keeping three fields was rejected because dense headless scenes could leak far entities across multiple cold ticks. A managed list was rejected for GC. A NativeQueue was rejected because the job has one bounded producer and fixed index writes are cheaper to audit.
Scalability potential: Low can reclaim up to 16 far entities per 1Hz cold tick without allocation. Middle/High/Ultra can raise the constant only with a matching safety-invariant update and profiler proof.
Hardware Impact: Costs one fixed int buffer and a short duplicate scan; saves repeated future 1024-slot scans for entities that should already be free. Estimated net gain is tens of microseconds in dense far-field cleanup on i3/MX350.

Problem: If the despawn lane saturated, the job could stop counting a far entity as active even though it was not successfully queued for release.
Solution: Far entities now `continue` only after a successful despawn request. If the despawn lane is full, the entity remains in active-count accounting for the current state.
Rejected Alternatives: Trusting optimistic despawn state was rejected because request-buffer saturation is a valid low-end condition. Forcing a full reconciliation scan on the main thread was rejected as unnecessary cold-path cost.
Scalability potential: Low remains conservative under overflow. High/Ultra can increase request capacity without changing the accounting rule.
Hardware Impact: One branch in the 1Hz cold token scan; prevents budget drift and surprise over-spawning after dense cleanup.

Problem: The encounter predator AUP upload lane used one `GraphicsBuffer`, which violated the project bandwidth discipline requiring CPU/GPU double-buffering for GPU data.
Solution: Split predator AUP publication into two 16-slot `GraphicsBuffer` instances. `PublishPredatorAupBuffer()` writes the inactive buffer through `GraphicsBufferUploadUtility.UploadNativeArray`, publishes that buffer globally, and alternates the write side only on real uploads.
Rejected Alternatives: Keeping one buffer was rejected because GPU read/CPU write overlap can stall or corrupt visual timing. Creating a shared owner with `EcosystemDirector` was rejected in this pass because ownership of the global flora predator AUP buffer is outside the encounter-director domain.
Scalability potential: Low keeps the same 16 predators and avoids upload contention. Middle/High/Ultra can consume the stable A/B buffer for richer PDA/boid fear visualizations without adding scene searches or concrete class coupling.
Hardware Impact: Adds one 16-float4 graphics buffer, about 256 bytes plus driver overhead, and removes a potential CPU/GPU synchronization point on MX350. Upload cadence remains dirty-event only, not per-frame.

Problem: Terrain-rect biome sampling clamped candidate coordinates outside the active payload to the nearest terrain edge, which could silently assign the wrong biome byte to ahead-of-player spawn candidates.
Solution: The active-terrain path now rejects non-finite or out-of-rect UVs and falls back to deterministic wrapped heatmap sampling.
Rejected Alternatives: Keeping clamped edge sampling was rejected because it hides world-streaming boundary errors as valid biome data. Direct cross-tile lookup was rejected until the world/streaming owner exposes a stable zero-GC query.
Scalability potential: Low keeps one scalar bounds test and one heatmap fallback. Middle/High/Ultra can add multi-tile biome lookup later without changing the director's byte-gate contract.
Hardware Impact: Adds two finite checks and four comparisons only on spawn allocation; estimated below 1 microsecond on i3/MX350 while preventing wrong-species edge spawns.

Problem: `ApplySpawnRequests()` could fall back to legacy/default request fields if a native spawn request slot was invalid despite `SpawnRequestCount`, risking a wrong default threat instead of failing closed.
Solution: Replaced the fallback read with `TryGetSpawnRequest()`. When the persistent native request lane exists, each slot must contain a valid threat class; invalid or out-of-range slots now call `RollbackUnappliedSpawn()` and spawn nothing.
Rejected Alternatives: Spawning the legacy fallback was rejected because the fixed native request lane is the authoritative path. Throwing/logging from the hot path was rejected because this director must remain zero-GC and fail closed.
Scalability potential: Low fails closed under request-buffer corruption. High/Ultra can expand request capacity without changing the invariant.
Hardware Impact: One integer range check per applied spawn request; below 1 microsecond and no allocation.

Problem: Spawn candidate scoring still computed a normalized candidate index with a divide inside the 16/32-candidate cold loop.
Solution: Hoisted the denominator to one `math.rcp` before the candidate loop and multiplied by the index inside the loop.
Rejected Alternatives: Leaving the divide was legal at 1Hz but contradicted the reciprocal-math mandate. Precomputing every radius into another native buffer was rejected as extra persistent memory for a tiny scalar expression.
Scalability potential: Low keeps candidate selection cheap on MX350-class hardware. Middle/High/Ultra can raise candidate counts later without restoring a per-candidate divide.
Hardware Impact: Saves up to 32 scalar divides per cold tick; estimated below 1 microsecond on i3/MX350, but deterministic and free of allocation.

Problem: The encounter cold job was finalized from the normal dispatcher `Tick()` path once the handle was complete. That avoided a blocking stall, but it still put job output application in the wrong dispatcher phase for the Native Jobs mandate.
Solution: Moved non-forced output completion and main-thread spawn/despawn application to `HectonDirectorAI.LateFrameTick()` through `EncounterDirector.CompleteReadyOutput()`. `Advance()` now only records telemetry and accumulates cold time while a job is scheduled.
Rejected Alternatives: Forcing completion in `Tick()` was rejected because it would serialize the job. Leaving completed-handle finalization in `Tick()` was rejected because dispatcher swap discipline should be phase-explicit, not merely non-blocking.
Scalability potential: Low keeps 1Hz output application in the swap window with no stall. Middle/High/Ultra can raise cold frequency later without moving job results back into the frame solve path.
Hardware Impact: Removes a possible dev-build dispatcher warning and keeps job output application out of the normal gameplay solve. Estimated runtime cost is one branch in `LateFrameTick`, below 1 microsecond on i3/MX350.

Problem: Disabling or destroying `HectonDirectorAI` could leave the dispatcher registration or predator AUP globals alive longer than the service.
Solution: Added `ForceStopAndReset()` on disable to force-complete teardown safely, clear headless state, and publish a zero predator count. Added destroy-time updatable unregister as a defensive fallback.
Rejected Alternatives: Trusting Unity call ordering was rejected because stale registry references and stale shader globals are hard to diagnose during scene transitions. Clearing only the shader int was rejected because the headless pool should also be reset while the service is disabled.
Scalability potential: Low-end scene transitions avoid stale predator pressure in boid/PDA consumers. High/Ultra can rely on clean service boundaries when hot-reloading richer encounter visuals.
Hardware Impact: Teardown-only forced completion has no gameplay-frame cost. Disable reset scans fixed 1024 headless slots only outside active gameplay.

Problem: Predator AUP publication could scan the fixed 1024-slot headless pool once per spawn/despawn release, and the spawn side still treated drone-only churn like predator-buffer work.
Solution: `ApplyCompletedOutput()` now carries one `predatorAupDirty` flag through despawn and spawn application, publishes the A/B predator buffer once after all completed output is applied, and marks dirty only for Stalker/Swarm/Leviathan spawns. Headless release reports whether a predator was actually removed, so non-predator cleanup does not upload.
Rejected Alternatives: Publishing inside every release helper was rejected as avoidable O(1024 * event count) work. Publishing after every spawn batch was rejected because Drone/Crab requests do not feed the predator AUP buffer. Direct boid-controller mutation was rejected again because predator AUP consumer ownership still needs an Integrator-owned registry contract.
Scalability potential: Low keeps dense cleanup cheap. Middle/High/Ultra can spend saved CPU/GPU sync budget on richer predator cues and PDA/boid fear visuals without changing this dirty-flag contract.
Hardware Impact: Saves up to 15 redundant 1024-slot scans in a full despawn tick and avoids drone-only spawn/despawn uploads; estimated tens of microseconds on i3/MX350 dense cleanup, no allocation.

Problem: A follow-up audit found three small but real correctness leaks: disposal used deferred native disposal after the first array even without an active job dependency, invalid forced threat ids could reach the Burst purchase lane, and predator obstruction raycasts treated a hit as line of sight.
Solution: Native arrays/lists now stay synchronous unless an actual job dependency already exists. Forced threat class ids are range-checked before the job consumes a forced request. Predator sight completion now treats a raycast hit against obstruction layers as blocked LOS and a clear ray as visible.
Rejected Alternatives: Leaving disposal deferred was rejected because teardown should be deterministic when no job owns the buffers. Letting invalid forced requests fail later on the main thread was rejected because it wastes forced budget bookkeeping. Raycasting against predator/player layers was rejected because this lane is designed as a cheap obstruction probe.
Scalability potential: Low avoids needless teardown jobs and false predator aggro. Middle/High/Ultra keep the same one-ray budget while improving behavior correctness before any visual overkill is added.
Hardware Impact: Synchronous no-dependency disposal removes avoidable job scheduling during teardown; invalid forced ids exit before request writes; corrected LOS prevents false hunt-state churn. Runtime savings are sub-microsecond in normal frames, with larger behavioral stability gain.

Problem: Headless allocation still searched the 1024-slot pool from slot zero for each applied spawn request, so dense spawn batches repeatedly re-scanned known occupied prefixes.
Solution: Added `_headlessFreeSearchCursor` with wraparound scanning, reset on pool clear, and rewind on release when the freed slot is earlier than the current cursor.
Rejected Alternatives: A managed stack/list of free slots was rejected for GC and extra ownership. A second persistent NativeQueue was rejected because releases happen on the main thread and the existing fixed pool only needs a single deterministic cursor.
Scalability potential: Low reduces worst-case spawn-batch scans while keeping the 1024-slot cap. Middle/High/Ultra can raise spawn request capacity later without turning allocation into repeated prefix walks.
Hardware Impact: Saves up to roughly 15 repeated occupied-prefix scans in a full 16-request cold tick; exact microsecond proof is pending profiler, no allocation added.

Problem: The file still carried a tracked-predator fallback lane, but predator AUP publication only copied headless predators. If an integrator re-enables hydrated fallback registration, boid/PDA predator AUP consumers would miss those predators or keep stale entries after tracked death/recall.
Solution: `PublishPredatorAupBuffer()` now appends tracked Stalker/Swarm/Leviathan entries after headless predators, and tracked death, inactive cleanup, recall, and registration mark the predator AUP lane dirty only when the tracked class writes predator AUP.
Rejected Alternatives: Per-frame tracked Transform publication was rejected as bandwidth waste and hot Unity object access. Direct `HectonBoidController` mutation was rejected again because no Integrator-owned registry contract exists for that consumer.
Scalability potential: Low keeps headless predators prioritized in the 16-slot buffer and only scans up to 32 tracked fallback slots on dirty/cold cleanup. Middle/High/Ultra can consume the same A/B buffer for richer fear visuals without adding concrete cross-domain references.
Hardware Impact: Adds no hot-path allocation; dirty-event scans cost at most 32 tracked Transform checks, estimated below 2 microseconds on i3/MX350 when the fallback lane is used.

Problem: A few cold math helpers still used scalar division where reciprocal forms were already the project law.
Solution: Replaced the 24-bit hash normalization divide with a reciprocal constant and changed the Pade-style exponential approximation divide to `numerator * math.rcp(max(denominator, epsilon))` in both managed and Burst helper copies.
Rejected Alternatives: `math.exp` was rejected as more expensive than the existing approximation. Leaving cold divides was rejected because the reciprocal law applies even when the gain is small and deterministic.
Scalability potential: Low keeps cold tick math cheap. Middle/High/Ultra can spend the saved scalar work on more candidate directions or richer event cues if profiler data justifies it.
Hardware Impact: Sub-microsecond expected savings per cold evaluation on i3/MX350; no behavior-level change intended.

Problem: `HectonDirectorAI.OnEnable()` previously returned before service publication when `GlobalRegistry.Dispatcher` was absent, so the encounter service and GPU resources could stay uninitialized during bootstrap ordering races.
Solution: Split service publication from dispatcher-lane registration. `OnEnable()` now registers the encounter service, initializes GPU/runtime state, and attempts dispatcher lanes; `Start()` retries the dispatcher lanes once after bootstrap without adding an `Update()` polling path.
Rejected Alternatives: Per-frame retry polling was rejected because the director should not spend hot-frame budget on bootstrap bookkeeping. Forcing dispatcher creation was rejected because `GlobalRegistry.TryEnsureDispatcherRegistration()` explicitly treats a missing dispatcher as a bootstrap error owned by Core.
Scalability potential: Low devices avoid per-frame registration checks and still publish the service as soon as the component is enabled. Middle/High/Ultra keep the same clean service boundary while richer encounter visuals consume the registered service.
Hardware Impact: Startup-only branch cost; no recurring frame cost. The avoided failure mode is behavioral: a silently inert director after dispatcher-order races.

Final Git Diff:
- `Assets/_Project/Scripts/EncounterDirector.cs`: current diff includes headless entity pool, wraparound headless free-slot cursor, 16-slot native spawn/despawn request buffers, predictive spawn math, canonical threat costs, tier-reselecting credit drain, despawn/refund handling, Despair Mode, terrain-rect biome byte gate with out-of-rect fallback, predator AUP A/B graphics upload, batched predator AUP dirty publication, tracked-predator AUP fallback publication, blackbox dump, job-ownership guard, state rollback, authoritative native spawn-request fail-closed handling, forced-threat id range validation, reciprocal hash/exp/candidate math, LateFrame output completion, force-stop reset, synchronous no-dependency disposal, and cold-tick-only token refresh.
- `Assets/_Project/Scripts/HectonDirectorAI.cs`: added death-signal drain, safe drain gating, predator AUP buffer bridge, service publication independent of dispatcher availability, `Start()` dispatcher-lane retry, cold GPU buffer creation point, LateFrame encounter-output application, corrected obstruction-ray LOS semantics, disable-time director reset, and destroy-time dispatcher unregister fallback.
- Evidence files updated: `Docs/Tasks/Status_AI_ENCOUNTER_DIRECTOR.md`, `Docs/AgentLogs/Rationale_AI_ENCOUNTER_DIRECTOR.md`, `Docs/AgentLogs/LOG_AI_ENCOUNTER_DIRECTOR.md`, and `Docs/AgentLogs/RECON_AI_ENCOUNTER_DIRECTOR.md`.
