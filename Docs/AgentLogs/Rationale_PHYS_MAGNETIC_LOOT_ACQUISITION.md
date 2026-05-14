# PHYS_MAGNETIC_LOOT_ACQUISITION Rationale

Status: PENDING VERIFICATION

## Decision 0 - Execution Boundary

Problem: Existing loot magnet implementation is unknown. Prompt demands Burst spatial pull, but AGENTS.md forbids guessing APIs and direct dependencies across parallel agents.

Solution: Scan current loot, inventory, AUP, vault, signal, audio, and VFX contracts before code. Implement only against existing contracts or create narrow local contracts under gameplay loot isolation where no contract exists.

Rejected Alternatives: Directly inventing `GlobalDataVault`, `ItemAcquiredSignal`, or `WAKE_TURBULENCE_COMPUTE` shapes would compile-risk the batch and create false dependencies.

Scalability potential: Low tier will prefer 10Hz snap or coarse pull; Middle uses scheduled spatial pull; High and Ultra can use stronger VFX/audio event density while preserving same gameplay truth.

Hardware Impact: Expected gain on i3/MX350 comes from removing PhysX trigger stay work and replacing it with contiguous SoA iteration. Exact microsecond proof is pending profiler/compile evidence.

## Decision 1 - Mandate Selection

Problem: Loot magnet touches physics avoidance, AUP math, Native jobs, inventory state, signal dispatch, debug telemetry, and presentation fakes.

Solution: Read the eight task-relevant mandate files recorded in Status before code.

Rejected Alternatives: Reading the full registry would create context noise; reading only physics would miss inventory/vault sovereignty and AUP shift safety.

Scalability potential: The selected mandates define low/middle/high/ultra behavior, especially Math LOD and fake-first VFX/audio.

Hardware Impact: Keeps MX350 path bounded by cadence and flat NativeArray access instead of Unity trigger overhead.

## Decision 2 - No Singleton / No Trigger Patch

Problem: Prompt references `LootMagnet.Instance` and loot `OnTriggerStay`, but current scans found no loot magnet singleton or loot-prefab trigger stay implementation.

Solution: Treat the purge tasks as scan-verified no-ops and add the new spatial pull path without creating replacement singleton dependencies.

Rejected Alternatives: Adding a new global singleton would preserve the architectural fault the task is trying to remove. Disabling project-wide trigger stay settings would affect construction/fluid trigger systems outside the assigned domain.

Scalability potential: Low/Middle/High/Ultra all avoid PhysX trigger fan-out for loot acquisition; higher tiers can spend saved CPU on VFX/audio density.

Hardware Impact: On i3/MX350 the expected gain is removal of per-collider trigger callbacks. Exact microseconds are pending profiler evidence.

## Decision 3 - Loot Module Boundary

Problem: Loot magnet needs new contracts but must not absorb unrelated inventory/core code or create dependencies on parallel agents.

Solution: Add `Hecton8.Gameplay.Loot.Contracts` for Burst structs/constants and `Hecton8.Gameplay.Loot` for the MonoBehaviour runtime bridge. Core expansion is limited to `SystemID.GameplayLoot`, five vault buffer IDs, and `AcousticPingSignal.ChannelLootZip`.

Rejected Alternatives: Editing `PickupItem` hot paths or adding loot contracts directly into `Hecton8.Core` would increase compile blast radius and tie gameplay to object identity.

Scalability potential: Low uses SlowTick snap; Middle uses normal pull; High/Ultra can increase authored radius/strength and add denser consumers on the same signal contracts.

Hardware Impact: Flat arrays reduce cache misses versus component walks. MX350 path only scans registry on SlowTick and never runs FastTick integration.

## Decision 4 - AUP-Space Burst Pull

Problem: Runtime float positions are unsafe across floating-origin shifts, and Rigidbody forces would reintroduce PhysX cost.

Solution: Burst job reads `AbsoluteUniversePosition` from `EntityAUPs`, computes AUP-space deltas, updates `EntityVelocities`, and writes the integrated AUP back to vault buffers. Division uses `math.rcp(math.max(distSq, 0.01f))`.

Rejected Alternatives: `Rigidbody.AddForce`, `Physics.OverlapSphere`, and transform-space distance checks all violate deterministic AUP/state ownership requirements.

Scalability potential: Low snaps instantly on radius entry; Middle integrates; High can raise radius; Ultra can pair the same AUP stream with heavier presentation.

Hardware Impact: Estimated 12-45 us for 4096 entities under Burst on desktop; MX350 avoids the 60Hz job entirely. Verification still required.

## Decision 5 - Presentation Cheats

Problem: Prompt asks for wake turbulence, audio, and GPU particle feedback without a direct `WAKE_TURBULENCE_COMPUTE` contract in the scanned code.

Solution: Emit existing `WakeGeneratedSignal` and `AcousticPingSignal` lanes from the Burst job. Presentation pings are stride-limited to 64 slots so prewarmed native signal queues do not grow under mass loot fields.

Rejected Alternatives: Direct compute-buffer writes would invent a dependency and collide with VFX ownership. Per-item AudioSource or ParticleSystem calls would allocate and scale poorly.

Scalability potential: Low gets acquisition snap with minimal presentation; Middle gets sparse zip/wake cues; High/Ultra VFX can subscribe to the same wake/acoustic lanes and overdraw deliberately.

Hardware Impact: MX350 path stays inside prewarmed native queues. Saved CPU is converted to visual/audio fake density only on higher tiers.

## Decision 6 - Black Box And Compile Wall

Problem: Critical runtime needs postmortem state, but Unity compilation could not be confirmed because MCP lost session and the generated core project already fails on unrelated assembly references.

Solution: Added a 300-entry fixed NativeArray telemetry ring and binary dump path `Docs/AgentLogs/Dump_PHYS_MAGNETIC_LOOT_ACQUISITION.bin` on non-finite detection. Status remains `PENDING VERIFICATION`; compile task is not claimed.

Rejected Alternatives: Logging strings every frame would allocate and produce noise. Claiming compile success from static inspection would be a fake report.

Scalability potential: Telemetry is constant-size across Low/Middle/High/Ultra. High-end visual overkill does not change crash evidence format.

Hardware Impact: One 300-entry persistent NativeArray is cold memory. Per-frame telemetry writes are fixed and below measurable gameplay cost; dump I/O only occurs on fault.

## OMEGA POLISH CHANGES

Problem: Polish mandate required removal of honest math and hot-path bloat after core tasks were checked/blocked.

Solution: Removed the per-signal `math.sqrt(PullRadiusSq)` dependency by keeping scheduled pull radius data on the runtime side. Confirmed no `foreach`, `string.Format`, interpolated strings, `.ToString()`, `math.sqrt`, or `math.normalize` remain under `Assets/_Project/Scripts/Gameplay/Loot`.

Rejected Alternatives: Leaving the square root in signal emission would be acceptable for correctness but not for the frame-time dictatorship. A lookup table is unnecessary because radius is already authored once per schedule.

Cinematic Cheats used: Wake uses `WakeGeneratedSignal` as the marine-snow fake instead of compute-buffer coupling; LootZip audio uses sparse stride-limited acoustic pings; low tier snaps/acquires instead of integrating.

Scalability potential: Low = 10Hz snap/acquire. Middle = Burst pull and sparse VFX/audio. High = larger authored magnet radius. Ultra = denser downstream wake/audio consumers on the same signals without changing truth state.

Hardware Impact: Removed two square roots per emitted acoustic signal. At the 64-signal stride cap, worst-case saved work is roughly 64 sqrt operations/frame during dense pull fields. Exact microseconds remain pending profiler/Burst evidence.

Final Git Diff: Historical note from the first polish pass: task-owned gameplay polish removed per-signal radius square-root work and kept status/rationale records. Current continuation diffs supersede the earlier radius implementation detail.

Build Status: `dotnet build Hecton8.Core.csproj --no-restore -v:q -clp:ErrorsOnly` still fails before loot verification on missing cross-assembly references such as `Hecton8.Environment.Fluids`, `Hecton8.Core.Scheduling`, `Hecton8.Audio.Virtualization`, `Hecton8.Physics.CCD`, and related service types. Unity MCP console remained unavailable after refresh timeout. Status remains `PENDING VERIFICATION`.

## Decision 7 - SignalBus Correctness

Problem: Burst direct writes to `GlobalSignals.ItemAcquiredSignalWriter` only enqueue into the raw NativeQueue. Several systems, including inventory repair-side effects, read `SignalBus<ItemAcquiredSignal>.GetFrameSnapshot()` and would miss loot magnet acquisitions.

Solution: The Burst job now writes compact `LootMagnetSignalEvent` records into a persistent NativeArray. Late-frame commit publishes acquisition, acoustic, and wake signals through `GlobalSignals.Publish`, after actual inventory-added quantity is known.

Rejected Alternatives: Keeping raw queue writes would be faster on paper but semantically incomplete. Publishing inventory success from inside Burst is impossible because inventory is managed and must remain off the job thread.

Scalability potential: Low/Middle/High/Ultra all use the same event lane. Higher tiers can consume SignalBus audio/wake data without adding new dependencies.

Hardware Impact: Adds one fixed NativeArray write per active slot and keeps global signal queues prewarmed. It removes false acquisition pings when inventory is full and avoids downstream repair/inventory systems missing the event.

## Decision 8 - Dense Field Budget And AUP Math Upgrade

Problem: A dense loot cloud could try to stow hundreds of items in one late-frame pass, overrunning the 128-item signal lane and spiking inventory work. The job also used two absolute AUP conversions per entity.

Solution: Added `MaxAcquisitionsPerFrame = 64`, restoring deferred vault flags for the rest. Replaced double absolute conversions with direct AUP sector-delta math using the 5000 m cell size. Added a 50 ms integration delta clamp and required `PullEnabled` in the job mask.

Rejected Alternatives: Letting NativeQueue growth handle overload violates zero-GC intent. Full spatial hashing was rejected for this pass because the prompt's concrete task list demands linear SoA iteration over `EntityAUPs` and `EntityFlags`; the acquisition budget buys predictable behavior without new shared dependencies.

Scalability potential: Low remains 10Hz snap/acquire; Middle drains 64/frame; High/Ultra can raise presentation density downstream without changing the acquisition cap.

Hardware Impact: MX350 avoids bursty inventory work. Sector-delta math saves two AUP absolute conversions per candidate and reduces per-entity math pressure before Burst verification.

## Decision 9 - Continuation Source Boundary

Problem: On the 2026-05-15 continuation, `Docs/Tasks/CURRENT_BATCH.md` no longer contains `<AGENT_PROMPT id="PHYS_MAGNETIC_LOOT_ACQUISITION">`. The batch protocol still requires re-extraction, but borrowing a neighboring prompt would corrupt scope.

Solution: Treat the missing current-batch tag as an external batch rotation. Continue from persisted `Status_PHYS_MAGNETIC_LOOT_ACQUISITION.md`, this rationale file, and the user's repeated direct assignment. Record the missing extraction rather than inventing a new prompt.

Rejected Alternatives: Using the active fauna/noise/data prompts would violate strict parsing. Stopping work would ignore the user's explicit continuation request.

Scalability potential: No runtime behavior change. It protects H-Phi process integrity by keeping domain scope stable during parallel-agent batch churn.

Hardware Impact: None.

## Decision 10 - Fault Evidence And Entity Identity

Problem: Fault dumps could be written before the current fault frame was recorded. Pickup slot identity also used a truncated `int` sidecar derived from an entity id, which creates avoidable collision risk over long sessions.

Solution: Commit acquisition/hash/fault counters before dumping, record telemetry immediately on first fault, suppress duplicate same-frame telemetry writes, and store full `ulong` pickup entity ids in the sidecar.

Rejected Alternatives: Duplicating fault frames wastes the fixed 300-entry black box. `GetInstanceID()` was rejected because engine object ids are runtime-local; truncated entity ids were rejected because collision risk is unnecessary.

Scalability potential: Low/Middle/High/Ultra all keep the same fixed dump shape. Dense pickup fields preserve per-slot velocity only for the exact same entity.

Hardware Impact: Adds one `uint` frame marker and increases the cold sidecar from 16 KB to 32 KB at 4096 slots. No FastTick allocation; SlowTick compare remains O(n).

## Decision 11 - Burst Local Broadphase

Problem: Far-sector loot still paid AUP delta math before the radius reject, and the integration path still exposed a cross-assembly AUP rebuild call to Burst.

Solution: Add a guarded adjacent-cell reject when `PullRadiusSq <= AupCellSizeSq`, then rebuild integrated AUPs with local numeric math inside `LootMagnetPullJob`.

Rejected Alternatives: A mutable spatial hash table was rejected because the prompt's architecture is vault SoA iteration and adding a hash owner creates new dependency and allocation risk. Unconditional cell rejection was rejected because oversized debug radii must still work.

Scalability potential: Low-tier 10Hz scans skip far-cell math. Middle/High/Ultra retain exact nearby behavior while reducing math pressure in scattered loot fields.

Hardware Impact: Adds three integer comparisons before double/float delta math. Saves sector-delta work for loot more than one 5 km AUP cell from the player. Exact profiler numbers remain pending Unity/Burst verification.

## Decision 12 - Idle Black-Box Continuity

Problem: The 300-frame telemetry ring only advanced after a completed pull job. Idle frames were absent, which weakens the black-box requirement for a critical gameplay system.

Solution: LateFrame now advances a telemetry frame counter and records high-level state when no pull job is scheduled. If a job is still running, idle recording is skipped to avoid reading arrays owned by the job. Completed jobs record once, and fault dumps still force a same-frame record before disk output.

Rejected Alternatives: Reading `EntityAUPs` while the job is still running was rejected because it can race with Burst writes. Logging strings per idle frame was rejected as GC/noise.

Scalability potential: Low/Middle/High/Ultra all keep a fixed 300-entry ring with consistent idle and active state. Visual overkill does not affect telemetry shape.

Hardware Impact: Adds one fixed struct write per idle late frame and no allocation. This is acceptable black-box cost and bounded to persistent NativeArray memory.

## Decision 13 - Commit Accuracy And Scalable Capacity

Problem: Late-frame acquisition reporting used the vault quantity captured during SlowTick refresh, not the pickup's live quantity at commit time. Fully consumed slots also kept stale acquired flags until the next registry refresh, and full-inventory failures could reattempt every FastTick.

Solution: Measure added quantity from the pickup's live pre/post inventory quantity, clear consumed vault slots immediately, clear PullEnabled on zero-add inventory rejections until SlowTick refresh, cache scheduled pull radius for presentation intensity, and separate the default 4096 entity capacity from an 8192 hard cap for high-density authored fields.

Rejected Alternatives: Trusting stale vault quantities was rejected because concurrent/manual pickups can overreport `ItemAcquiredSignal.Quantity`. Keeping PullEnabled on inventory rejection was rejected because it can pound managed inventory/drop overflow work every frame. Raising the default capacity was rejected because low/middle devices should not pay extra cold memory without author intent.

Scalability potential: Low remains 4096 default with 10Hz snap/acquire. Middle keeps the same default path. High and Ultra can author up to 8192 loot slots when scene density justifies the memory, while presentation still uses the same sparse signal lane.

Hardware Impact: MX350 avoids repeated full-inventory acquisition attempts between SlowTicks and avoids stale acquired slots in FastTick scans. High-end devices gain optional double-density vault capacity without changing default low-end memory.

## Decision 14 - Assembly Surface Honesty

Problem: The runtime loot asmdef still carried `Hecton8.Core.Contracts` even though `LootMagnetSystem` directly uses Core, Core.Memory, inventory, interaction, world, jobs, collections, and mathematics symbols only.

Solution: Remove the unused runtime asmdef reference while keeping `Unity.Burst` in the contracts asmdef because `LootMagnetPullJob` owns the `[BurstCompile]` attribute.

Rejected Alternatives: Leaving stale asmdef references was rejected because it widens compile coupling and contradicts the isolation task. Removing `Unity.Burst` from contracts was rejected because it would break the Burst attribute.

Scalability potential: No frame behavior change. Smaller assembly dependency surface reduces parallel-agent integration risk and keeps loot contracts isolated.

Hardware Impact: No runtime microsecond gain claimed. This is compile graph hygiene.

## Decision 15 - Presentation Budget And Shutdown Handoff

Problem: High-density authored capacity can reach 8192 loot slots, while the shared acoustic queue is prewarmed for 64 packets and wake is prewarmed for 128. A forced disable could also complete a scheduled job and then dispose the signal event lane before committing vault results.

Solution: Add tiered loot presentation budgets: acoustic Low/Mid/High/Ultra = 16/48/56/64 and wake = 32/96/112/128. Budgets are resolved once per commit pass and passed by reference through presentation publishing. Acoustic intensity math is skipped when the acoustic budget is exhausted or when the event is wake-only. `OnDisable` now force-completes a pending job and commits it when all vault/event arrays are valid.

Rejected Alternatives: Raising global signal capacities was rejected because that is a core-wide memory policy decision outside this domain. Letting NativeQueue growth absorb dense loot presentation was rejected because it violates prewarmed zero-GC intent. Dropping forced job results on disable was rejected because vault state could remain acquired/pulling without inventory or telemetry handoff.

Scalability potential: Low keeps minimal acoustic/wake feedback. Middle remains under shared lane capacity. High and Ultra spend more of the saved trigger budget on presentation without exceeding the prewarmed lane shape.

Hardware Impact: MX350 avoids acoustic queue growth, limits wake work, and stops paying radius/intensity math after cosmetic acoustic budget is gone. High-end devices keep up to 64 acoustic and 128 wake loot packets per commit pass, matching the existing global queue prewarm ceilings.

## Decision 16 - Scene Lifecycle Reinstall Hook

Problem: A scene-authored `LootMagnetSystem` can set `_bootstrapRuntime` before the runtime installer runs. The installer then skips creating the persistent fallback, and the scene-authored instance can be destroyed on a later scene load, leaving no loot magnet scheduler.

Solution: Add a static `SceneManager.sceneLoaded` hook installed from `EnsureRuntimeInstalled`. On every scene load, the hook calls the same installer. If a valid runtime exists, it returns. If the previous scene-owned runtime was destroyed, it creates a new scene-owned fallback without a scene search. Removed gameplay-owned `DontDestroyOnLoad`.

Rejected Alternatives: Scene-wide object search was rejected because runtime `Find*` calls violate hot-path/static audit policy. Gameplay-owned `DontDestroyOnLoad` was rejected because project audit policy reserves persistence ownership for bootstrap/crash systems.

Scalability potential: No frame behavior change. Low/Middle/High/Ultra all keep magnet scheduling alive across scene transitions without direct scene dependencies.

Hardware Impact: No per-frame cost. One scene-load event callback, one branch per scene load, and one scene-owned cold GameObject when a scene has no authored runtime; no FastTick allocation.

## Decision 17 - Dotnet Process Hygiene

Problem: User explicitly forbids dotnet rebuilds. Pre-existing Hecton8 `dotnet build` processes were found running in the workspace during verification hygiene.

Solution: Stop the existing processes and record that this pass did not start dotnet.

Rejected Alternatives: Leaving the process running was rejected because it violates the current session constraint and can mutate build artifacts/noise. Running another build to verify was rejected by user instruction.

Scalability potential: No runtime behavior change. This protects evidence quality during parallel-agent work.

Hardware Impact: Removes build CPU pressure from the local machine; no gameplay microseconds claimed.

## Decision 18 - Presentation Drop Telemetry

Problem: Dense loot fields can legitimately exhaust the tiered acoustic or wake presentation budgets. Before this pass, the black-box flags only distinguished non-finite fault frames, so a dump could not show whether the system clipped cosmetic output to preserve shared signal lane budgets.

Solution: `PublishPresentationSignals` now returns fixed telemetry bits for acoustic and wake budget drops. `CommitVaultResultsToManagedProxies` ORs those bits into the last committed telemetry flags while keeping the non-finite fault bit separate.

Rejected Alternatives: Raising global queue capacity was rejected because it is a core memory policy decision outside this domain. Logging each dropped presentation signal was rejected because it would allocate/noise and punish the exact dense-field scenario being protected.

Scalability potential: Low tier can show frequent cosmetic clipping without changing acquisition truth. Middle/High/Ultra can spend larger budgets on richer zip/wake presentation while black-box evidence still records when authored density exceeds the budget.

Hardware Impact: Adds two branch checks and one `uint` OR path per presentation event. MX350 gains diagnosable load shedding without queue growth; high-end devices retain visual overkill up to the explicit acoustic/wake ceilings.

## Decision 19 - H8Memory Ownership

Problem: Loot-owned persistent NativeArrays for signal events and telemetry were registered through `NativeMemorySentinel`, but current project rules require `H8Memory.Allocate` ownership with a concrete `SystemID`.

Solution: Allocate and release `_signalEvents` and `_telemetry` through `H8Memory` using `SystemID.GameplayLoot`. `OnEnable` exits before tick registration if either lane is missing, and `EnsureVaultBuffers` now fails closed unless both `_signalEvents` and `_telemetry` are created.

Rejected Alternatives: Leaving direct `new NativeArray` allocations was rejected because it bypasses owner byte accounting. Falling back to managed arrays was rejected because it would break Burst and zero-GC guarantees.

Scalability potential: Low/Middle/High/Ultra keep the same runtime behavior, but native ownership is now visible to the global memory ledger. High-density authored capacity cannot silently allocate outside the gameplay loot owner.

Hardware Impact: No per-frame cost. Cold allocation/release now has H8Memory tracking overhead only at enable/capacity changes; MX350 benefits from owner-capped failure behavior instead of unchecked persistent memory.

## Decision 20 - Respawned Dotnet Wrapper Hygiene

Problem: After the first dotnet processes were stopped, an external PowerShell wrapper respawned `dotnet build .\Assembly-CSharp.csproj` inside the workspace.

Solution: Identified the parent PowerShell command line and stopped the wrapper plus its dotnet children. A later final-check `dotnet build Hecton8.Core.csproj` process was also stopped. No dotnet build/rebuild was launched by this pass.

Rejected Alternatives: Letting the wrapper continue was rejected because it violates the user's active constraint and contaminates verification timing. Running our own dotnet command to compare output was rejected for the same reason.

Scalability potential: No gameplay behavior change. This protects process hygiene while other agents run in parallel.

Hardware Impact: Removes local build CPU pressure. No frame microseconds claimed.

## Decision 21 - Black-Box Hash Completeness

Problem: The telemetry ring stored positions and a flags hash, but the hash folded only entity flags. Two dense loot frames with identical pull/acquired flags but different item content would look identical in the dump.

Solution: Fold `_entityItemHashes[index]` into the same deterministic FNV-style telemetry hash during commit. The telemetry struct and binary dump layout stay unchanged.

Rejected Alternatives: Adding another telemetry field was rejected because it would change the dump shape without need. Writing per-slot item data was rejected because the black box must stay fixed-size and high-level.

Scalability potential: Low/Middle/High/Ultra all keep the same 300-entry dump format. High-density authored loot fields gain better postmortem discrimination without larger telemetry memory.

Hardware Impact: Adds one integer XOR/multiply per committed slot. MX350 cost is bounded by scheduled count and replaces ambiguity, not frame budget.
