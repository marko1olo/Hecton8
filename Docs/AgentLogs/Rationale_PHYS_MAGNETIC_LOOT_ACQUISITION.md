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
